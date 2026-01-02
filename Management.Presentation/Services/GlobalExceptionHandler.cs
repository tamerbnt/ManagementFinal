using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using Management.Domain.Services;

namespace Management.Presentation.Services
{
    public class GlobalExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;
        private readonly IDialogService _dialogService;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IDialogService dialogService)
        {
            _logger = logger;
            _dialogService = dialogService;
        }

        public void HandleException(Exception ex, string source)
        {
            _logger.LogError(ex, "Unhandled exception from {Source}", source);

            // In a production app, we might want to sanitize this message
            // or show a friendly "Something went wrong" with a generic code.
            // For this admin tool, detailed errors are acceptable but maybe wrapped.

            string title = "Unexpected Error";
            string message = $"An error occurred in {source}:\n\n{ex.Message}";

            // If it's a critical DB error, we might want to suggest restarting
            if (ex.ToString().Contains("DbContext") || ex.ToString().Contains("connection"))
            {
                message += "\n\nPlease check your database connection.";
            }

            // Use the Dialog Service to show the error
            // We use Application.Current.Dispatcher to ensure UI thread access if this comes from background
            System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                // We assume IDialogService has a simple Alert method
                 await _dialogService.ShowAlertAsync(title, message, "Error");
            });
        }
    }
}
