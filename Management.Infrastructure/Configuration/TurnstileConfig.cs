namespace Management.Infrastructure.Configuration
{
    public class TurnstileConfig
    {
        public string IpAddress { get; set; } = "192.168.1.201";
        public int Port { get; set; } = 4370;
        public int TimeoutMs { get; set; } = 5000;
        public bool AutoReconnect { get; set; } = true;
        public int ReconnectIntervalMs { get; set; } = 10000;
        public int GateOpenDurationMs { get; set; } = 3000;
        public int MachineNumber { get; set; } = 1;
        public bool UseMock { get; set; } = false;
        public System.Guid FacilityId { get; set; } = System.Guid.Empty;
    }
}
