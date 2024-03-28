using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SMSSENDER
{
    public class SmsService : BackgroundService
    {
        private readonly ILogger<SmsService> _logger;
        private readonly IConfiguration _configuration;

        public SmsService(ILogger<SmsService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                int baudRate = getBaudRate();
                string comPort = getComPort();
                int listenPort = messageListenerPort();
                int queueInterval = getQueueInterval();
                SmsSender sms = new SmsSender(listenPort, baudRate, comPort,queueInterval);
                await sms.StartProcess();
            }
        }

        private string getComPort()
        {
            string com = _configuration.GetSection("SmsGateway").GetSection("COM").Value;
            return com;
        }

        private int getBaudRate()
        {
            return Convert.ToInt32(_configuration.GetSection("SmsGateway").GetSection("BaudRate").Value);
        }
        private int getQueueInterval()
        {
            int queueInverval= Convert.ToInt32(_configuration.GetSection("SmsGateway").GetSection("QueueInterval").Value);
            return queueInverval;
        }

        private int messageListenerPort()
        {
            return Convert.ToInt32(_configuration.GetSection("Message").GetSection("ListenPort").Value);
        }
    }
}