using System;
using System.Collections.Generic;
using Management.Domain.Common;

namespace Management.Domain.Models.Diagnostics
{
    public class DiagnosticEntry : BaseEntity
    {
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public DiagnosticCategory Category { get; set; }
        public DiagnosticSeverity Severity { get; set; }
        
        public string Message { get; set; } = string.Empty;
        public string? ExceptionType { get; set; }
        public string? StackTrace { get; set; }
        public string? Context { get; set; } // e.g., method name or component

        public bool IsSanitized { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();

        // V2 Enhancements
        public List<Breadcrumb> Breadcrumbs { get; set; } = new();
        public SystemInfoSnapshot? SystemSnapshot { get; set; }
        public string? DeviceId { get; set; }

        /// <summary>
        /// Formats the diagnostic entry into a clean string for copying to clipboard/AI prompting.
        /// </summary>
        public string ToCopyableString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("--- DIAGNOSTIC ERROR REPORT ---");
            sb.AppendLine($"Timestamp: {TimestampUtc:u}");
            sb.AppendLine($"Category: {Category}");
            sb.AppendLine($"Severity: {Severity}");
            sb.AppendLine($"Context: {Context ?? "Unknown"}");
            sb.AppendLine($"Device ID: {DeviceId ?? "Unknown"}");
            sb.AppendLine($"Message: {Message}");
            sb.AppendLine($"Exception: {ExceptionType ?? "N/A"}");

            if (SystemSnapshot != null)
            {
                sb.AppendLine("\n[SYSTEM SNAPSHOT]");
                sb.AppendLine($"OS: {SystemSnapshot.OsVersion}");
                sb.AppendLine($"RAM (Avail/Total): {SystemSnapshot.AvailableRamMb}/{SystemSnapshot.TotalRamMb} MB");
                sb.AppendLine($"App Version: {SystemSnapshot.AppVersion}");
            }

            sb.AppendLine("\n[STACK TRACE]");
            sb.AppendLine(StackTrace ?? "No stack trace available");

            if (Breadcrumbs.Any())
            {
                sb.AppendLine("\n[RECENT ACTIVITY HISTORY]");
                foreach (var b in Breadcrumbs.OrderByDescending(x => x.TimestampUtc).Take(10))
                {
                    sb.AppendLine($"[{b.TimestampUtc:HH:mm:ss}] {b.Category}: {b.Message}");
                }
            }

            sb.AppendLine("\n-------------------------------");
            return sb.ToString();
        }
    }
}
