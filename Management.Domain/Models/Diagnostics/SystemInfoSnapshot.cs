namespace Management.Domain.Models.Diagnostics
{
    public class SystemInfoSnapshot
    {
        public string OsVersion { get; set; } = string.Empty;
        public long AvailableRamMb { get; set; }
        public long TotalRamMb { get; set; }
        public string AppVersion { get; set; } = string.Empty;
    }
}
