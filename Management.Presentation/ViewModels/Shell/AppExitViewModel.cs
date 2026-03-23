using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.Interfaces;
using Management.Application.Interfaces.App;
using Management.Application.Services;
using Management.Domain.Interfaces;
using Management.Presentation.Services;
using Management.Presentation.ViewModels.Base;
using Microsoft.Extensions.Logging;

namespace Management.Presentation.ViewModels.Shell
{
    public partial class AppExitViewModel : FacilityAwareViewModelBase, IModalViewModel, IModalResult<ExitModalResult>
    {
        private readonly IModalNavigationService _modalNavigationService;
        private readonly IReportingService _reportingService;

        [ObservableProperty]
        private ExitModalResult _result = ExitModalResult.Cancel;

        [ObservableProperty]
        private bool _isGeneratingReport;

        public bool HasResult => true; // Always has a default result of Cancel

        public ModalSize PreferredSize => ModalSize.Small;

        public AppExitViewModel(
            ITerminologyService terminologyService,
            IFacilityContextService facilityContext,
            ILogger<AppExitViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            IModalNavigationService modalNavigationService,
            IReportingService reportingService)
            : base(terminologyService, facilityContext, logger, diagnosticService, toastService)
        {
            _modalNavigationService = modalNavigationService;
            _reportingService = reportingService;

            Title = GetTerm("Terminology.Shell.Exit.Title") ?? "Quit Luxurya?";
        }

        public Task<bool> CanCloseAsync() => Task.FromResult(true);

        [RelayCommand]
        private async Task StayAsync()
        {
            Result = ExitModalResult.Cancel;
            await _modalNavigationService.CloseCurrentModalAsync();
        }

        [RelayCommand]
        private async Task ExitAsync()
        {
            Result = ExitModalResult.Close;
            await _modalNavigationService.CloseCurrentModalAsync();
        }

        [RelayCommand]
        private async Task CloseAndReportAsync()
        {
            try
            {
                IsGeneratingReport = true;
                _toastService?.ShowInfo("Generating final report...", "Close Day");

                var facilityId = _facilityContext.CurrentFacilityId;
                var snapshot = await _reportingService.GetDailySnapshotAsync(facilityId, DateTime.Today);
                var pdfBytes = await _reportingService.GenerateDailyPdfReportAsync(snapshot);

                var reportsFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Luxurya", "Reports");
                Directory.CreateDirectory(reportsFolder);

                var fileName = $"CloseDay_{DateTime.Today:yyyy_MM_dd}_{DateTime.Now:HHmmss}.pdf";
                var filePath = Path.Combine(reportsFolder, fileName);

                await File.WriteAllBytesAsync(filePath, pdfBytes);

                // Optional: Open it? The user said "responsible for the feature close day report".
                // In Dashboard, it opens. Let's open it here too, before exiting.
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });

                _toastService?.ShowSuccess($"Close Day report saved to Documents\\Luxurya\\Reports.", "Report Generated");
                
                Result = ExitModalResult.CloseAndReport;
                await _modalNavigationService.CloseCurrentModalAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to generate close day report during exit.");
                _toastService?.ShowError("Failed to generate report: " + ex.Message, "Error");
                
                // If it fails, we still might want to allow the user to exit, but let's let them decide via the UI again
                IsGeneratingReport = false;
            }
        }
    }
}
