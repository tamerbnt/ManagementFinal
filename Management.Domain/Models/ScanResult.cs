using Management.Domain.Enums;
using Management.Domain.Models;

namespace Management.Domain.Models
{
    public class ScanResult
    {
        public AccessResult Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public Member? Member { get; set; }

        public ScanResult(AccessResult status, string message, Member? member = null)
        {
            Status = status;
            Message = message;
            Member = member;
        }

        public static ScanResult Denied(string message, Member? member = null) => new ScanResult(AccessResult.Denied, message, member);
        public static ScanResult Granted(string message, Member member) => new ScanResult(AccessResult.Granted, message, member);
        public static ScanResult Warning(string message, Member member) => new ScanResult(AccessResult.Warning, message, member);
    }
}
