using System;
using System.Configuration;

namespace MQRabbit
{
    internal class Program
    {
        private static void Main()
        {
            var mqManager = new MqManager(new RmqInitModel
            {
                HostName = ConfigurationManager.AppSettings["RMQ_Host"],
                Port = int.Parse(ConfigurationManager.AppSettings["RMQ_Port"]),
                UserName = ConfigurationManager.AppSettings["RMQ_UserName"],
                Password = ConfigurationManager.AppSettings["RMQ_Password"]
            });

            mqManager.Consume(Exchanges.Parse, (o, s1) => OnGetParse(s1), "#");

            Console.ReadKey();
        }

        private static void OnGetParse(string s)
        {
            Console.WriteLine(s);
        }
    }
}