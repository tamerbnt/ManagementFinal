using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Management.Presentation.Diagnostics;
using Management.Presentation.Models;
using Management.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Management.Presentation.Services
{
    public class DiagnosticService : IDiagnosticService
    {
        private readonly ConcurrentQueue<DiagnosticEntry> _entries = new();
        private readonly SemaphoreSlim _streamSemaphore = new(0);
        private BindingErrorTraceListener? _bindingListener;
        
        public event EventHandler<DiagnosticEntry>? EntryAdded;

        // Required theme resource keys
        private static readonly string[] RequiredThemeKeys = new[]
        {
            "WindowBg",
            "FacilityAccentBrush",
            "TextPrimary",
            "TextSecondary",
            "BorderBrush",
            "HoverBg"
        };

        // ViewModels to validate in DI container
        private static readonly Type[] ViewModelsToValidate = new[]
        {
            typeof(MainViewModel),
            typeof(DashboardViewModel),
            typeof(MembersViewModel),
            typeof(ShopViewModel),
            typeof(SettingsViewModel)
        };

        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

        public DiagnosticService(Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task StartBindingErrorListenerAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    _bindingListener = new BindingErrorTraceListener(this);
                    PresentationTraceSources.DataBindingSource.Listeners.Add(_bindingListener);
                    PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning | SourceLevels.Error;

                    LogError(
                        DiagnosticCategory.Startup,
                        nameof(StartBindingErrorListenerAsync),
                        "Binding error listener started successfully",
                        null,
                        DiagnosticSeverity.Info
                    );
                }
                catch (Exception ex)
                {
                    LogError(
                        DiagnosticCategory.Startup,
                        nameof(StartBindingErrorListenerAsync),
                        "Failed to start binding error listener",
                        ex,
                        DiagnosticSeverity.Critical
                    );
                }
            });
        }

        public async Task<DiagnosticResult> ValidateDependencyInjectionAsync(IServiceProvider services)
        {
            await Task.Delay(1); // Make async

            var failures = new List<string>();

            foreach (var viewModelType in ViewModelsToValidate)
            {
                try
                {
                    var instance = services.GetService(viewModelType);
                    if (instance == null)
                    {
                        failures.Add($"{viewModelType.Name}: Not registered in DI container");
                        
                        LogError(
                            DiagnosticCategory.DependencyInjection,
                            nameof(ValidateDependencyInjectionAsync),
                            $"Failed to resolve {viewModelType.Name}",
                            null,
                            DiagnosticSeverity.Critical
                        );
                    }
                }
                catch (Exception ex)
                {
                    var missingDependency = ExtractMissingDependency(ex);
                    failures.Add($"{viewModelType.Name}: {missingDependency}");
                    
                    LogError(
                        DiagnosticCategory.DependencyInjection,
                        nameof(ValidateDependencyInjectionAsync),
                        $"Failed to resolve {viewModelType.Name}: {missingDependency}",
                        ex,
                        DiagnosticSeverity.Critical
                    );
                }
            }

            if (failures.Any())
            {
                return DiagnosticResult.Fail(
                    "DI Validation",
                    $"{failures.Count} ViewModel(s) failed to resolve",
                    string.Join(Environment.NewLine, failures),
                    DiagnosticSeverity.Critical
                );
            }

            return DiagnosticResult.Ok("DI Validation", "All ViewModels resolved successfully");
        }


        public async Task<DiagnosticResult> TestSupabaseConnectivityAsync()
        {
            try
            {
                // Get Supabase configuration from config (appsettings.json) or environment
                var supabaseUrl = _configuration["Supabase:Url"] ?? Environment.GetEnvironmentVariable("SUPABASE_URL");
                var supabaseKey = _configuration["Supabase:Key"] ?? Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY");

                if (string.IsNullOrEmpty(supabaseUrl) || string.IsNullOrEmpty(supabaseKey))
                {
                    var message = "Supabase configuration missing";
                    var details = $"URL: {(string.IsNullOrEmpty(supabaseUrl) ? "Missing" : "Present")}, " +
                                 $"Key: {(string.IsNullOrEmpty(supabaseKey) ? "Missing" : "Present")}";

                    LogError(
                        DiagnosticCategory.Network,
                        nameof(TestSupabaseConnectivityAsync),
                        message,
                        null,
                        DiagnosticSeverity.Critical
                    );

                    return DiagnosticResult.Fail("Supabase Connectivity", message, details, DiagnosticSeverity.Critical);
                }

                // Simple connectivity test - try to create a client
                var options = new Supabase.SupabaseOptions
                {
                    AutoRefreshToken = false,
                    AutoConnectRealtime = false
                };

                var client = new Supabase.Client(supabaseUrl, supabaseKey, options);
                
                // Test basic connectivity with a timeout
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await client.InitializeAsync();

                LogError(
                    DiagnosticCategory.Network,
                    nameof(TestSupabaseConnectivityAsync),
                    "Supabase connectivity test passed",
                    null,
                    DiagnosticSeverity.Info
                );

                return DiagnosticResult.Ok("Supabase Connectivity", "Successfully connected to Supabase");
            }
            catch (TaskCanceledException)
            {
                var message = "Supabase connection timeout";
                LogError(DiagnosticCategory.Network, nameof(TestSupabaseConnectivityAsync), message, null, DiagnosticSeverity.Error);
                return DiagnosticResult.Fail("Supabase Connectivity", message, "Connection timed out after 5 seconds", DiagnosticSeverity.Error);
            }
            catch (Exception ex)
            {
                var message = "Supabase connectivity test failed";
                LogError(DiagnosticCategory.Network, nameof(TestSupabaseConnectivityAsync), message, ex, DiagnosticSeverity.Error);
                return DiagnosticResult.Fail("Supabase Connectivity", message, ex.Message, DiagnosticSeverity.Error);
            }
        }

        public async Task<DiagnosticResult> VerifyThemeResourcesAsync()
        {
            await Task.Delay(1); // Make async

            var missingKeys = new List<string>();

            try
            {
                var app = System.Windows.Application.Current;
                if (app == null)
                {
                    return DiagnosticResult.Fail("Theme Verification", "Application.Current is null", null, DiagnosticSeverity.Critical);
                }

                foreach (var key in RequiredThemeKeys)
                {
                    if (!app.Resources.Contains(key))
                    {
                        missingKeys.Add(key);
                        
                        LogError(
                            DiagnosticCategory.Theme,
                            nameof(VerifyThemeResourcesAsync),
                            $"Missing required theme resource: {key}",
                            null,
                            DiagnosticSeverity.Warning
                        );
                    }
                }

                if (missingKeys.Any())
                {
                    return DiagnosticResult.Fail(
                        "Theme Verification",
                        $"{missingKeys.Count} required resource(s) missing",
                        string.Join(", ", missingKeys),
                        DiagnosticSeverity.Warning
                    );
                }

                return DiagnosticResult.Ok("Theme Verification", "All required theme resources found");
            }
            catch (Exception ex)
            {
                LogError(DiagnosticCategory.Theme, nameof(VerifyThemeResourcesAsync), "Theme verification failed", ex, DiagnosticSeverity.Error);
                return DiagnosticResult.Fail("Theme Verification", "Verification failed", ex.Message, DiagnosticSeverity.Error);
            }
        }

        public void LogError(
            DiagnosticCategory category,
            string methodName,
            string message,
            Exception? exception = null,
            DiagnosticSeverity severity = DiagnosticSeverity.Error)
        {
            var entry = new DiagnosticEntry
            {
                Timestamp = DateTime.Now,
                Category = category,
                Severity = severity,
                MethodName = methodName,
                Message = message,
                StackTrace = exception?.StackTrace,
                AdditionalInfo = exception?.Message
            };

            _entries.Enqueue(entry);
            _streamSemaphore.Release();
            
            EntryAdded?.Invoke(this, entry);
        }

        public IReadOnlyList<DiagnosticEntry> GetAllEntries()
        {
            return _entries.ToList();
        }

        public async IAsyncEnumerable<DiagnosticEntry> GetErrorStreamAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _streamSemaphore.WaitAsync(cancellationToken);
                
                if (_entries.TryDequeue(out var entry))
                {
                    yield return entry;
                }
            }
        }

        public void ClearAll()
        {
            while (_entries.TryDequeue(out _)) { }
        }

        public int GetErrorCount(DiagnosticSeverity? severity = null)
        {
            if (severity.HasValue)
            {
                return _entries.Count(e => e.Severity == severity.Value);
            }
            return _entries.Count;
        }

        private string ExtractMissingDependency(Exception ex)
        {
            // Try to extract the missing service type from the exception message
            var message = ex.InnerException?.Message ?? ex.Message;
            
            if (message.Contains("Unable to resolve service for type"))
            {
                var startIndex = message.IndexOf("type '") + 6;
                var endIndex = message.IndexOf("'", startIndex);
                if (startIndex > 6 && endIndex > startIndex)
                {
                    var typeName = message.Substring(startIndex, endIndex - startIndex);
                    return $"Missing dependency: {typeName.Split('.').Last()}";
                }
            }

            return ex.InnerException?.Message ?? ex.Message;
        }
    }
}
