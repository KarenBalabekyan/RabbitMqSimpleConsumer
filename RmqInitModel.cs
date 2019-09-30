using System;

namespace MQRabbit
{
    public class RmqInitModel
    {
        public bool AutomaticRecoveryEnabled { get; set; } = true;

        public string HostName { get; set; }

        public TimeSpan NetworkRecoveryInterval { get; } = TimeSpan.FromSeconds(10);

        public string Password { get; set; }

        public int Port { get; set; }

        public ushort RequestedHeartbeat { get; set; } = 30;

        public string UserName { get; set; }
    }
}