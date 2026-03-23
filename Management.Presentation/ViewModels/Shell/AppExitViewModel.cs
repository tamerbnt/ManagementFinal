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
            System.Diagnostics.Debug.WriteLine("[EXIT-VM] StayAsync called");
            Result = ExitModalResult.Cancel;
            System.Diagnostics.Debug.WriteLine($"[EXIT-VM] Result set to {Result}");
            await _modalNavigationService.CloseCurrentModalAsync();
            System.Diagnostics.Debug.WriteLine("[EXIT-VM] CloseCurrentModalAsync completed");
        }

        [RelayCommand]
        private async Task ExitAsync()
        {
            System.Diagnostics.Debug.WriteLine("[EXIT-VM] ExitAsync called");
            Result = ExitModalResult.Close;
            System.Diagnostics.Debug.WriteLine($"[EXIT-VM] Result set to {Result}");
            await _modalNavigationService.CloseCurrentModalAsync();
            System.Diagnostics.Debug.WriteLine("[EXIT-VM] CloseCurrentModalAsync completed");
        }

        [RelayCommand]
        private async Task CloseAndReportAsync()
        {
            System.Diagnostics.Debug.WriteLine("[EXIT-VM] CloseAndReportAsync called");
            try
            {
                IsGeneratingReport = true;
                _toastService?.ShowInfo("Generating final report...", "Close Day");

                var facilityId = _facilityContext.CurrentFacilityId;
                System.Diagnostics.Debug.WriteLine($"[EXIT-VM] Generating report for facility: {facilityId}");
                
                var snapshot = await _reportingService.GetDailySnapshotAsync(facilityId, DateTime.Today);
                System.Diagnostics.Debug.WriteLine("[EXIT-VM] Snapshot retrieved");
                
                var pdfBytes = await _reportingService.GenerateDailyPdfReportAsync(snapshot);
                System.Diagnostics.Debug.WriteLine($"[EXIT-VM] PDF generated: {pdfBytes?.Length ?? 0} bytes");

                var reportsFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Luxurya", "Reports");
                Directory.CreateDirectory(reportsFolder);

                var fileName = $"CloseDay_{DateTime.Today:yyyy_MM_dd}_{DateTime.Now:HHmmss}.pdf";
                var filePath = Path.Combine(reportsFolder, fileName);

                await File.WriteAllBytesAsync(filePath, pdfBytes);
                System.Diagnostics.Debug.WriteLine($"[EXIT-VM] Report saved to: {filePath}");

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });

                _toastService?.ShowSuccess($"Close Day report saved to Documents\\Luxurya\\Reports.", "Report Generated");
                
                Result = ExitModalResult.CloseAndReport;
                System.Diagnostics.Debug.WriteLine($"[EXIT-VM] Result set to {Result}");
                await _modalNavigationService.CloseCurrentModalAsync();
                System.Diagnostics.Debug.WriteLine("[EXIT-VM] CloseCurrentModalAsync completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EXIT-VM] EXCEPTION in CloseAndReportAsync: {ex}");
                _logger?.LogError(ex, "Failed to generate close day report during exit.");
                _toastService?.ShowError("Failed to generate report: " + ex.Message, "Error");
                
                IsGeneratingReport = false;
            }
        }
    }
}
