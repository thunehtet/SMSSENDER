using System;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SMSSENDER
{
    public class SmsSender
    {
        private int baudRate { get; set; }
        private int listenerPort { get; set; }
        private string comPort { get; set; }

        public SmsSender(int ListenerPort, int BaudRate, string ComPort)
        {
            baudRate = BaudRate;
            listenerPort = ListenerPort;
            comPort = ComPort;
        }

        public int DefaultDatRetention { get; set; } = 720;

        public async Task StartProcess()
        {
            UdpClient listener = new UdpClient(listenerPort);
            IPEndPoint groupEP = new IPEndPoint(IPAddress.Any, listenerPort);

            try
            {
                while (true)
                {
                    byte[] bytes = listener.Receive(ref groupEP);
                    string incomeMessage = $" {Encoding.ASCII.GetString(bytes, 0, bytes.Length)}";
                    await SendSms(baudRate, comPort, incomeMessage);
                    await WriteLog("[RECEIVE] [IP :" + groupEP.Address + "][PORT :" + groupEP.Port + "][ Message ] [ " + $" {Encoding.ASCII.GetString(bytes, 0, bytes.Length)}" + " ]");
                }
            }
            catch (SocketException e)
            {
                await WriteLog("[RECEIVE] [ERROR] [IP :" + groupEP.Address + "][PORT :" + groupEP.Port + "] [Message:" + e.Message + "]");
            }
            finally
            {
                listener.Close();
            }
        }

        private async Task SendSms(int BaudRate, string ComPort, string Message)
        {
            try
            {
                //"<OTP-" + Otp + ">|" + MobileNo + "|" + (smsDB.NoOfSms + 1));
                //"<OTP-12345>|12345678|1";
                string[] splitMessage = Message.Split("|");
                string pattern = @"<OTP-(?<OTPNO>\d.*)>$";
                var match = Regex.Match(splitMessage[0], pattern);


                //<"ALM-"+status,deviceID,alarmname+ ">|" + MobileNo + "|" + (smsDB.NoOfSms + 1));
                //<ALM-1,VMS_NICOLLHIGHWAY ,HighTempAlarm>|12345678|1"

                string ptnAlm = @"<ALM-(?<ALARMINFO>\w+.*)>$";
                var matchAlm = Regex.Match(splitMessage[0], ptnAlm);
                if (match.Success)
                {
                    string otpNo = match.Groups["OTPNO"].ToString();
                    string moblieNo = splitMessage[1];
                    string smsSerialNo = splitMessage[2];

                    using SerialPort serialPort = new SerialPort(ComPort, BaudRate, Parity.None, 8, StopBits.One);

                    try
                    {
                        serialPort.Open();
                        serialPort.WriteLine(@"AT" + (char)(13));
                        Thread.Sleep(200);
                        serialPort.WriteLine("AT+CMGF=1" + (char)(13));
                        Thread.Sleep(200);
                        serialPort.WriteLine(@"AT+CMGS=""" + moblieNo + @"""" + (char)(13));
                        Thread.Sleep(200);
                        string smsMessage = "Your OTP is < " + otpNo + " >for your LTA PGS system log in account. #SMSCOUNT" + smsSerialNo + (char)(26);
                        serialPort.WriteLine(smsMessage);
                        Thread.Sleep(200);
                        serialPort.Close();
                        await WriteSmsSend("[ SEND SMS ] [SUCCESS ] [Mobile No :" + moblieNo + "][Message :" + smsMessage + "]");
                    }
                    catch (Exception ex)
                    {
                        await WriteSmsSend("[ SEND SMS ] [ ERROR ] " + ex.Message);
                    }
                }
                else if (matchAlm.Success)
                {
                    string[] type = matchAlm.Groups["ALARMINFO"].ToString().Split(","); //status , alarm name // 1=>ALARM ACTIVATED ,0=>ALARM CLEARED
                    string moblieNo = splitMessage[1];
                    string smsSerialNo = splitMessage[2];

                    using SerialPort serialPort = new SerialPort(ComPort, BaudRate, Parity.None, 8, StopBits.One);

                    try
                    {
                        
                        if (type[0] == "1")
                        {
                            serialPort.Open();
                            serialPort.WriteLine(@"AT" + (char)(13));
                            Thread.Sleep(200);
                            serialPort.WriteLine("AT+CMGF=1" + (char)(13));
                            Thread.Sleep(200);
                            serialPort.WriteLine(@"AT+CMGS=""" + moblieNo + @"""" + (char)(13));
                            Thread.Sleep(200);
                            string smsMessage = "#ALARM-DETECT#PGS/" + type[1].ToUpper()+"/"+ type[2].ToUpper()+"/" +DateTime.Now.ToString("")+ "#SMSCOUNT" + smsSerialNo + (char)(26);
                            serialPort.WriteLine(smsMessage);
                            Thread.Sleep(200);
                            serialPort.Close();
                            await WriteSmsSend("[ SEND SMS ] [SUCCESS ] [Mobile No :" + moblieNo + "][Message :" + smsMessage + "]");
                        }
                        if (type[0] == "0")
                        {
                            serialPort.Open();
                            serialPort.WriteLine(@"AT" + (char)(13));
                            Thread.Sleep(200);
                            serialPort.WriteLine("AT+CMGF=1" + (char)(13));
                            Thread.Sleep(200);
                            serialPort.WriteLine(@"AT+CMGS=""" + moblieNo + @"""" + (char)(13));
                            Thread.Sleep(200);
                            string smsMessage = "#ALARM-CLEAR#PGS/" + type[1].ToUpper() + "/" + type[2].ToUpper() + "/" + DateTime.Now.ToString("") + "#SMSCOUNT" + smsSerialNo + (char)(26);
                            serialPort.WriteLine(smsMessage);
                            Thread.Sleep(200);
                            serialPort.Close();
                            await WriteSmsSend("[ SEND SMS ] [SUCCESS ] [Mobile No :" + moblieNo + "][Message :" + smsMessage + "]");
                        }
                        else
                        {
                            await WriteSmsSend("[ SEND SMS ] [ INVALID DATA ] ");
                        }


                    }
                    catch (Exception ex)
                    {
                        await WriteSmsSend("[ SEND SMS ] [ ERROR ] " + ex.Message);
                    }

                }
                else
                {
                    await WriteLog("[ RECEIVE ][ MESSAGE DISCARD ] DO NOT NOT COMFORM FORMAT ::" + Message);
                }
            }
            catch (Exception e)
            {
                await WriteLog("[ SEND SMS ] [ ERROR ] " + e.Message);
            }
        }

        public Task WriteLog(string logMessage)
        {
            Log("COM", logMessage, DefaultDatRetention);
            return Task.CompletedTask;
        }

        public Task WriteSmsSend(string logMessage)
        {
            Log("SMS_SEND", logMessage, DefaultDatRetention);
            return Task.CompletedTask;
        }

        private Task Log(string logName, string logMessage, int DataRetention)
        {
            try
            {
                string orPath = Path.GetDirectoryName(Environment.CurrentDirectory) + @"\SMSLOG";
                string subDirecory = orPath + @"\" + logName;

                if (!Directory.Exists(orPath))
                {
                    Directory.CreateDirectory(orPath);
                }
                if (!Directory.Exists(subDirecory))
                {
                    Directory.CreateDirectory(subDirecory);
                }

                string path = Path.Combine(subDirecory, logName).ToString() + DateTime.Now.ToString("yyyyMM") + ".log";

                if (!File.Exists(path))
                {
                    File.Create(path).Dispose();
                    StreamWriter sw = File.AppendText(path);
                    sw.WriteLine("[ " + DateTime.Now.ToString("") + "]  Log is Started");
                    sw.Close();
                    DirectoryInfo yourRootDir = new DirectoryInfo(subDirecory);
                    foreach (FileInfo file in yourRootDir.GetFiles())
                        if (file.LastWriteTime < DateTime.Now.AddDays(-DataRetention))
                            file.Delete();
                }
                else
                {
                    StreamWriter sw = File.AppendText(path);
                    sw.WriteLineAsync("[ " + DateTime.Now.ToString("") + " ] [ " + logName + " LOG ] " + logMessage);
                    sw.Close();
                }
            }
            catch
            {
                return Task.CompletedTask;
            }

            return Task.CompletedTask;
        }
    }
}