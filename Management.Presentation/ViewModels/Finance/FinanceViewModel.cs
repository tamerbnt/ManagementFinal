using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.Interfaces;
using Management.Application.Services;
using Microsoft.Extensions.Logging;
using Management.Presentation.ViewModels.Base;
using Management.Presentation.Services.Localization;
using Management.Application.Interfaces.App;
using Management.Domain.Services;
using Management.Application.Interfaces.ViewModels;

namespace Management.Presentation.ViewModels.Finance
{
    public partial class FinanceViewModel : FacilityAwareViewModelBase, INavigationalLifecycle
    {
        private readonly IFinanceService _financeService;

        [ObservableProperty]
        private decimal _expectedCash;

        [ObservableProperty]
        private decimal _actualCash;

        [ObservableProperty]
        private string _reconciliationNotes = string.Empty;

        [ObservableProperty]
        private bool _isReconciliationOpen;

        [ObservableProperty]
        private decimal _netRevenue;

        [ObservableProperty]
        private int _pendingInvoices;

        [ObservableProperty]
        private System.Windows.Media.PointCollection _forecastPoints = new();

        [ObservableProperty]
        private int _currentStep = 1;

        [ObservableProperty]
        private int _dataMaturityScore;

        [ObservableProperty]
        private string _confidenceLevel = "Unknown";

        public decimal Variance => ActualCash - ExpectedCash;

        private string DraftPath => System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Titan",
            "reconciliation_draft.bin");

        public IAsyncRelayCommand LoadFinanceDataCommand { get; }
        public IAsyncRelayCommand OpenReconciliationCommand { get; }
        public IAsyncRelayCommand SubmitReconciliationCommand { get; }
        public IRelayCommand CloseReconciliationCommand { get; }
        public IRelayCommand NextStepCommand { get; }
        public IRelayCommand PreviousStepCommand { get; }

        public FinanceViewModel(
            IFinanceService financeService,
            IFacilityContextService facilityContextService,
            ITerminologyService terminologyService,
            ILocalizationService localizationService,
            ILogger<FinanceViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService)
            : base(terminologyService, facilityContextService, logger, diagnosticService, toastService, localizationService)
        {
            _financeService = financeService;
            Title = GetTerm("Strings.Finance.Title");

            LoadFinanceDataCommand = new AsyncRelayCommand(LoadFinanceDataAsync);
            OpenReconciliationCommand = new AsyncRelayCommand(OpenReconciliationAsync);
            OpenReconciliationCommand = new AsyncRelayCommand(OpenReconciliationAsync);
            SubmitReconciliationCommand = new AsyncRelayCommand(SubmitReconciliationAsync);
            CloseReconciliationCommand = new RelayCommand(() => IsReconciliationOpen = false);
            NextStepCommand = new RelayCommand(() => CurrentStep++);
            PreviousStepCommand = new RelayCommand(() => CurrentStep--);
            
            LoadDraft();
        }

        public Task PreInitializeAsync()
        {
            Title = GetTerm("Strings.Finance.Title");
            return Task.CompletedTask;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task LoadDeferredAsync()
        {
            IsActive = true;
            await LoadFinanceDataAsync();
        }

        partial void OnActualCashChanged(decimal value) => SaveDraft();
        partial void OnReconciliationNotesChanged(string value) => SaveDraft();

        private void SaveDraft()
        {
            try 
            { 
                var directory = System.IO.Path.GetDirectoryName(DraftPath);
                if (!System.IO.Directory.Exists(directory))
                    System.IO.Directory.CreateDirectory(directory);

                System.IO.File.WriteAllText(DraftPath, $"{ActualCash}|{ReconciliationNotes}"); 
            } 
            catch { }
        }

        private void LoadDraft()
        {
            try
            {
                if (System.IO.File.Exists(DraftPath))
                {
                    var draft = System.IO.File.ReadAllText(DraftPath).Split('|');
                    if (draft.Length == 2)
                    {
                        ActualCash = decimal.Parse(draft[0]);
                        ReconciliationNotes = draft[1];
                    }
                }
            }
            catch { }
        }

        public ObservableCollection<FacilityZone> FacilityZones { get; } = new();

        private async Task LoadFinanceDataAsync()
        {
            await ExecuteLoadingAsync(async () =>
            {
                await Task.Delay(500); // Simulate network
                ExpectedCash = 1250.50m;
                NetRevenue = 4250.00m;
                PendingInvoices = 12;

                // Generate Forecast Points (mock data)
                var points = new System.Windows.Media.PointCollection();
                points.Add(new System.Windows.Point(0, 150));
                points.Add(new System.Windows.Point(100, 120));
                points.Add(new System.Windows.Point(200, 130));
                points.Add(new System.Windows.Point(300, 90));
                points.Add(new System.Windows.Point(400, 100));
                points.Add(new System.Windows.Point(500, 40));
                points.Add(new System.Windows.Point(600, 20));
                points.Add(new System.Windows.Point(700, 50));
                points.Add(new System.Windows.Point(800, 10));
                ForecastPoints = points;

                // Load Facility Zones
                FacilityZones.Clear();
                FacilityZones.Add(new FacilityZone("Main Gym", true, "Active"));
                FacilityZones.Add(new FacilityZone("Cardio Hub", true, "Active"));
                FacilityZones.Add(new FacilityZone("Sauna/Steam", false, "Off-Hours"));
                FacilityZones.Add(new FacilityZone("Staff Lounge", true, "Restricted"));

                DataMaturityScore = 85;
                ConfidenceLevel = "High";
            }, _localizationService.GetString("Strings.Finance.Error.LoadMetrics"));
        }

        public record FacilityZone(string Name, bool IsActive, string Status);

        private async Task OpenReconciliationAsync()
        {
            await ExecuteSafeAsync(async () =>
            {
                CurrentStep = 1;
                IsReconciliationOpen = true;
                await Task.CompletedTask;
            });
        }

        private async Task SubmitReconciliationAsync()
        {
            if (IsReconciliationOpen == false) return;
            
            await ExecuteSafeAsync(async () =>
            {
                // TODO: result = await _financeService.SubmitReconciliationAsync(...);
                await Task.Delay(300);
                IsReconciliationOpen = false;
                
                try { if (System.IO.File.Exists(DraftPath)) System.IO.File.Delete(DraftPath); } catch { }
                ActualCash = 0;
                ReconciliationNotes = string.Empty;
            }, _localizationService.GetString("Strings.Finance.Error.SubmissionFailed"));
        }

        protected override void OnLanguageChanged()
        {
            base.OnLanguageChanged();
            Title = GetTerm("Strings.Finance.Title");
        }
    }
}
