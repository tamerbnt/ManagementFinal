using System;

namespace Management.Presentation.Models
{
    public enum DiagnosticSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    public enum DiagnosticCategory
    {
        Binding,
        DependencyInjection,
        Network,
        Theme,
        Runtime,
        Startup,
        Database
    }

    public class DiagnosticEntry
    {
        public DateTime Timestamp { get; set; }
        public DiagnosticCategory Category { get; set; }
        public DiagnosticSeverity Severity { get; set; }
        public string MethodName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? StackTrace { get; set; }
        public string? AdditionalInfo { get; set; }

        public string CategoryDisplay => Category.ToString();
        public string SeverityDisplay => Severity.ToString();
        public string TimeDisplay => Timestamp.ToString("HH:mm:ss.fff");
        public bool HasStackTrace => !string.IsNullOrEmpty(StackTrace);
    }

    public class DiagnosticResult
    {
        public bool Success { get; set; }
        public string TestName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Details { get; set; }
        public DiagnosticSeverity Severity { get; set; }

        public static DiagnosticResult Ok(string testName, string message)
        {
            return new DiagnosticResult
            {
                Success = true,
                TestName = testName,
                Message = message,
                Severity = DiagnosticSeverity.Info
            };
        }

        public static DiagnosticResult Fail(string testName, string message, string? details = null, DiagnosticSeverity severity = DiagnosticSeverity.Error)
        {
            return new DiagnosticResult
            {
                Success = false,
                TestName = testName,
                Message = message,
                Details = details,
                Severity = severity
            };
        }
    }
}
