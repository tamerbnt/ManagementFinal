using System;
using System.Diagnostics;
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

        public override void WriteLine(string? message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            // Filter for actual binding errors
            if (message.Contains("System.Windows.Data Error") || 
                message.Contains("BindingExpression") ||
                message.Contains("Cannot find"))
            {
                // Extract method name from stack trace if available
                var stackTrace = new StackTrace(true);
                var methodName = "XAML Binding";

                // Try to extract the binding path from the error message
                var bindingPath = ExtractBindingPath(message);
                if (!string.IsNullOrEmpty(bindingPath))
                {
                    methodName = $"Binding: {bindingPath}";
                }

                _diagnosticService.LogError(
                    Models.DiagnosticCategory.Binding,
                    methodName,
                    message,
                    null,
                    Models.DiagnosticSeverity.Warning
                );
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
