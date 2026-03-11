using System;
using System.Diagnostics;
using System.Windows;
using Management.Application.Services;
using Management.Presentation.Services;

namespace Management.Presentation.Diagnostics
{
    /// <summary>
    /// Custom TraceListener that captures WPF binding errors and forwards them to the diagnostic service.
    /// </summary>
    public class BindingErrorTraceListener : TraceListener
    {
        private readonly IDiagnosticService _diagnosticService;

        public BindingErrorTraceListener(IDiagnosticService diagnosticService)
        {
            _diagnosticService = diagnosticService ?? throw new ArgumentNullException(nameof(diagnosticService));
        }

        public override void Write(string? message)
        {
            // Binding errors come through WriteLine, not Write
        }

        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> _recentErrors = new();
        private readonly TimeSpan _throttleWindow = TimeSpan.FromSeconds(5);

        public override void WriteLine(string? message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            // Filter for actual binding errors
            if (message.Contains("System.Windows.Data Error") || 
                message.Contains("BindingExpression") ||
                message.Contains("Cannot find"))
            {
                // Throttling: Avoid flooding logs with the same error
                var now = DateTime.UtcNow;
                if (_recentErrors.TryGetValue(message, out var lastSeen))
                {
                    if (now - lastSeen < _throttleWindow)
                    {
                        return; // Throttle
                    }
                }
                _recentErrors[message] = now;

                // Cleanup periodically (simple implementation: clear if it gets too big)
                if (_recentErrors.Count > 100) _recentErrors.Clear();

                // Note: We removed the expensive StackTrace(true) call here as it causes UI hangs
                // when a flood of binding errors occurs.
                
                var bindingPath = ExtractBindingPath(message);
                var context = !string.IsNullOrEmpty(bindingPath) ? $"Binding: {bindingPath}" : "XAML Binding";

                _diagnosticService.LogError(
                    Management.Application.Services.DiagnosticCategory.Binding, 
                    context, 
                    message, 
                    null, 
                    Management.Application.Services.DiagnosticSeverity.Warning);
            }
        }

        private string? ExtractBindingPath(string message)
        {
            // Try to extract binding path from common error patterns
            // Example: "System.Windows.Data Error: 40 : BindingExpression path error: 'PropertyName' property not found"
            
            var pathIndex = message.IndexOf("path error:", StringComparison.OrdinalIgnoreCase);
            if (pathIndex > 0)
            {
                var afterPath = message.Substring(pathIndex + 11).Trim();
                var quoteStart = afterPath.IndexOf('\'');
                var quoteEnd = afterPath.IndexOf('\'', quoteStart + 1);
                
                if (quoteStart >= 0 && quoteEnd > quoteStart)
                {
                    return afterPath.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
                }
            }

            return null;
        }
    }
}
