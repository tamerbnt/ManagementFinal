using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Management.Presentation.Models;

namespace Management.Presentation.Services
{
    public interface IDiagnosticService
    {
        /// <summary>
        /// Starts listening for WPF binding errors.
        /// </summary>
        Task StartBindingErrorListenerAsync();

        /// <summary>
        /// Validates that all registered ViewModels can be resolved from the DI container.
        /// </summary>
        Task<DiagnosticResult> ValidateDependencyInjectionAsync(IServiceProvider services);

        /// <summary>
        /// Tests connectivity to Supabase backend.
        /// </summary>
        Task<DiagnosticResult> TestSupabaseConnectivityAsync();

        /// <summary>
        /// Verifies that all required theme resources are present.
        /// </summary>
        Task<DiagnosticResult> VerifyThemeResourcesAsync();

        /// <summary>
        /// Logs a diagnostic error.
        /// </summary>
        void LogError(DiagnosticCategory category, string methodName, string message, Exception? exception = null, DiagnosticSeverity severity = DiagnosticSeverity.Error);

        /// <summary>
        /// Gets all diagnostic entries.
        /// </summary>
        IReadOnlyList<DiagnosticEntry> GetAllEntries();

        /// <summary>
        /// Gets a stream of diagnostic entries as they are added.
        /// </summary>
        IAsyncEnumerable<DiagnosticEntry> GetErrorStreamAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears all diagnostic entries.
        /// </summary>
        void ClearAll();

        /// <summary>
        /// Gets the count of errors by severity.
        /// </summary>
        int GetErrorCount(DiagnosticSeverity? severity = null);

        /// <summary>
        /// Event raised when a new diagnostic entry is added.
        /// </summary>
        event EventHandler<DiagnosticEntry>? EntryAdded;
    }
}
