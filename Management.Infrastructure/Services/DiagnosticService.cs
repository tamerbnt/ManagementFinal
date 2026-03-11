using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Management.Application.Services;
using Management.Domain.Models.Diagnostics;

namespace Management.Infrastructure.Services
{
    public class DiagnosticService : Management.Application.Services.IDiagnosticService
    {
        public event EventHandler<Management.Application.Services.DiagnosticEntry>? EntryAdded;

        public async Task StartBindingErrorListenerAsync() => await Task.CompletedTask;
        public async Task<Management.Application.Services.DiagnosticResult> ValidateDependencyInjectionAsync(IServiceProvider services) => new Management.Application.Services.DiagnosticResult { IsSuccess = true };
        public async Task<Management.Application.Services.DiagnosticResult> TestSupabaseConnectivityAsync() => new Management.Application.Services.DiagnosticResult { IsSuccess = true };
        public async Task<Management.Application.Services.DiagnosticResult> VerifyThemeResourcesAsync() => new Management.Application.Services.DiagnosticResult { IsSuccess = true };
        public void LogError(Management.Application.Services.DiagnosticCategory category, string methodName, string message, Exception? exception = null, Management.Application.Services.DiagnosticSeverity severity = Management.Application.Services.DiagnosticSeverity.Error) { }
        public IReadOnlyList<Management.Application.Services.DiagnosticEntry> GetAllEntries() => new List<Management.Application.Services.DiagnosticEntry>();
        public async IAsyncEnumerable<Management.Application.Services.DiagnosticEntry> GetErrorStreamAsync([System.Runtime.CompilerServices.EnumeratorCancellation] System.Threading.CancellationToken cancellationToken = default) { yield break; }
        public void ClearAll() { }
        public int GetErrorCount(Management.Application.Services.DiagnosticSeverity? severity = null) => 0;

        public void Track(Exception ex, Management.Domain.Models.Diagnostics.DiagnosticCategory category = Management.Domain.Models.Diagnostics.DiagnosticCategory.Unexpected, string? context = null, Dictionary<string, string>? metadata = null)
        {
            var entry = CreateEntry(ex, (Management.Application.Services.DiagnosticCategory)(int)category, Management.Application.Services.DiagnosticSeverity.Error, context, metadata);
            AddAndPersist(entry);
        }

        public void TrackFatal(Exception ex, string? context = null)
        {
            var entry = CreateEntry(ex, Management.Application.Services.DiagnosticCategory.System, Management.Application.Services.DiagnosticSeverity.Fatal, context);
            AddAndPersist(entry);
        }



        private readonly List<Management.Application.Services.DiagnosticEntry> _memoryBuffer = new();
        private readonly string _reportsPath;
        private readonly object _lock = new();

        public DiagnosticService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _reportsPath = Path.Combine(appData, "Management", "Diagnostics");
            Directory.CreateDirectory(_reportsPath);
        }

        public Management.Application.Services.DiagnosticEntry? LastEntry 
        {
            get
            {
                lock (_lock)
                {
                    return _memoryBuffer.LastOrDefault();
                }
            }
        }


        public Task<IEnumerable<Management.Application.Services.DiagnosticEntry>> GetPendingReportsAsync()
        {
            return Task.Run(() =>
            {
                var reports = new List<Management.Application.Services.DiagnosticEntry>();
                if (!Directory.Exists(_reportsPath)) return Enumerable.Empty<Management.Application.Services.DiagnosticEntry>();

                foreach (var file in Directory.GetFiles(_reportsPath, "*.json"))
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var entry = JsonSerializer.Deserialize<Management.Application.Services.DiagnosticEntry>(json);
                        if (entry != null) reports.Add(entry);
                    }
                    catch
                    {
                        // Ignore corrupt files
                    }
                }
                return reports.OrderByDescending(x => x.TimestampUtc).AsEnumerable();
            });
        }

        public Task AcknowledgeReportAsync(Guid id)
        {
            return Task.Run(() =>
            {
                var path = Path.Combine(_reportsPath, $"{id}.json");
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            });
        }

        public void Clear()
        {
            lock (_lock)
            {
                _memoryBuffer.Clear();
            }
        }

        private void AddAndPersist(Management.Application.Services.DiagnosticEntry entry)
        {
            lock (_lock)
            {
                _memoryBuffer.Add(entry);
            }

            try
            {
                var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions { WriteIndented = true });
                var path = Path.Combine(_reportsPath, $"{entry.Id}.json");
                File.WriteAllText(path, json);
            }
            catch
            {
                // Last resort: fail silently or log to debug
                System.Diagnostics.Debug.WriteLine($"Failed to persist diagnostic entry: {entry.Id}");
            }
        }

        private Management.Application.Services.DiagnosticEntry CreateEntry(Exception ex, Management.Application.Services.DiagnosticCategory category, Management.Application.Services.DiagnosticSeverity severity, string? context, Dictionary<string, string>? metadata = null)
        {
            return new Management.Application.Services.DiagnosticEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = DateTime.UtcNow,
                Category = category,
                Severity = severity,
                Message = ex.Message,
                ExceptionType = ex.GetType().FullName,
                StackTrace = ex.StackTrace,
                Context = context,
                Metadata = metadata ?? new Dictionary<string, string>()
            };
        }
    }
}
