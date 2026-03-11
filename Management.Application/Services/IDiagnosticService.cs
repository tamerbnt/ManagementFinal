using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Services
{
    public enum DiagnosticSeverity
    {
        Info,
        Warning,
        Error,
        Critical,
        Fatal
    }

    public enum DiagnosticCategory
    {
        Binding,
        DependencyInjection,
        Network,
        Theme,
        Runtime,
        Startup,
        Database,
        System,
        UI,
        Security,
        Integration,
        Unexpected,
        Application
    }

    public class DiagnosticEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public DateTime Timestamp { get => TimestampUtc.ToLocalTime(); set => TimestampUtc = value.ToUniversalTime(); }
        public DiagnosticCategory Category { get; set; }
        public DiagnosticSeverity Severity { get; set; }
        public string? MethodName { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ExceptionType { get; set; }
        public string? StackTrace { get; set; }
        public string? Context { get; set; }
        public string? AdditionalInfo { get => Context; set => Context = value; }
        public Dictionary<string, string> Metadata { get; set; } = new();

        public string ToCopyableString()
        {
            return $"[{TimestampUtc:u}] {Category} {Severity}: {MethodName}\n{Message}\n{StackTrace}\nContext: {Context}";
        }
    }


    public class DiagnosticResult
    {
        public bool IsSuccess { get; set; }
        public string TestName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Details { get; set; }
        public DiagnosticSeverity Severity { get; set; }

        public static DiagnosticResult Ok(string testName, string message) => new() 
        { 
            IsSuccess = true, 
            TestName = testName, 
            Message = message, 
            Severity = DiagnosticSeverity.Info 
        };

        public static DiagnosticResult Fail(string testName, string message, string? details = null, DiagnosticSeverity severity = DiagnosticSeverity.Error) => new() 
        { 
            IsSuccess = false, 
            TestName = testName, 
            Message = message, 
            Details = details, 
            Severity = severity 
        };
    }


    public interface IDiagnosticService
    {
        Task StartBindingErrorListenerAsync();
        Task<DiagnosticResult> ValidateDependencyInjectionAsync(IServiceProvider services);
        Task<DiagnosticResult> TestSupabaseConnectivityAsync();
        Task<DiagnosticResult> VerifyThemeResourcesAsync();
        void LogError(DiagnosticCategory category, string methodName, string message, Exception? exception = null, DiagnosticSeverity severity = DiagnosticSeverity.Error);
        IReadOnlyList<DiagnosticEntry> GetAllEntries();
        IAsyncEnumerable<DiagnosticEntry> GetErrorStreamAsync(CancellationToken cancellationToken = default);
        void ClearAll();
        int GetErrorCount(DiagnosticSeverity? severity = null);
        event EventHandler<DiagnosticEntry>? EntryAdded;
        
        // Legacy/Infrastructure compatibility
        void Track(Exception ex, Management.Domain.Models.Diagnostics.DiagnosticCategory category = Management.Domain.Models.Diagnostics.DiagnosticCategory.Unexpected, string? context = null, Dictionary<string, string>? metadata = null);
        void TrackFatal(Exception ex, string? context = null);
    }
}
