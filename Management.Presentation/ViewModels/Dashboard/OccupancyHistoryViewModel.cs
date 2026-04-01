using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.DTOs;
using Management.Application.Interfaces;
using Management.Domain.Interfaces;
using Management.Presentation.Services;
using Management.Presentation.ViewModels.Base;
using Microsoft.Extensions.Logging;
using Management.Application.Interfaces.App;
using Management.Application.Services;

namespace Management.Presentation.ViewModels.Dashboard
{
    public partial class OccupancyHistoryViewModel : FacilityAwareViewModelBase
    {
        private readonly IDashboardService _dashboardService;
        private readonly IReportingService _reportingService;
        private readonly IModalNavigationService _modalNavigationService;

        [ObservableProperty]
        private string _analyzedPeriodLabel = "Lifetime Analysis";

        [ObservableProperty]
        private string _selectedPeriod = "Lifetime";

        [ObservableProperty]
        private OccupancyHistoryDto _data = new();

        public ObservableCollection<string> AvailablePeriods { get; } = new() { "Last 30 Days", "Last 90 Days", "Last Year", "Lifetime" };

        public IAsyncRelayCommand CloseCommand { get; }
        public IAsyncRelayCommand ExportPdfCommand { get; }

        public OccupancyHistoryViewModel(
            IDashboardService dashboardService,
            IReportingService reportingService,
            IModalNavigationService modalNavigationService,
            IFacilityContextService facilityContext,
            ILogger<OccupancyHistoryViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            ITerminologyService terminologyService,
            ILocalizationService localizationService) 
            : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService)
        {
            _dashboardService = dashboardService;
            _reportingService = reportingService;
            _modalNavigationService = modalNavigationService;

            CloseCommand = new AsyncRelayCommand(CloseAsync);
            ExportPdfCommand = new AsyncRelayCommand(ExecuteExportPdfAsync);
        }

        partial void OnSelectedPeriodChanged(string value)
        {
            _ = LoadDataAsync();
        }

        public override async Task OnModalOpenedAsync(object parameter, System.Threading.CancellationToken cancellationToken = default)
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            await ExecuteLoadingAsync(async () =>
            {
                var (start, end) = GetDateRange(SelectedPeriod);
                Data = await _dashboardService.GetOccupancyHistoryAsync(CurrentFacilityId, start, end);
                AnalyzedPeriodLabel = Data.AnalysisPeriod;
            }, GetTerm("Terminology.Dashboard.History.LoadingOccupancy") ?? "Loading historical occupancy analytics...");
        }

        private (DateTime? start, DateTime? end) GetDateRange(string period)
        {
            var end = DateTime.UtcNow;
            return period switch
            {
                "Last 30 Days" => (end.AddDays(-30), end),
                "Last 90 Days" => (end.AddDays(-90), end),
                "Last Year" => (end.AddYears(-1), end),
                _ => (null, null)
            };
        }

        private async Task ExecuteExportPdfAsync()
        {
            await ExecuteLoadingAsync(async () =>
            {
                var pdfBytes = await _reportingService.GenerateOccupancyHistoryPdfAsync(Data, CurrentFacility.ToString());
                
                var fileName = $"Occupancy_History_{CurrentFacility.ToString()}_{DateTime.Now:yyyyMMdd}.pdf";
                var filePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);
                await System.IO.File.WriteAllBytesAsync(filePath, pdfBytes);
                
                _toastService.ShowSuccess(GetTerm("Terminology.Dashboard.History.ExportSuccess") ?? "Report exported successfully to Documents.");
                
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = filePath, UseShellExecute = true });
            });
        }

        private async Task CloseAsync()
        {
            _modalNavigationService.CloseModal();
            await Task.CompletedTask;
        }
    }
}
