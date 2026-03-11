using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Management.Application.Services;
using Management.Domain.Services;
using Microsoft.Extensions.Logging;
using Management.Application.Interfaces.App;
using Management.Presentation.Extensions;

namespace Management.Presentation.ViewModels.Scheduler
{
    public partial class SchedulerViewModel : ViewModelBase, Management.Presentation.ViewModels.Base.IParameterReceiver
    {
        [ObservableProperty]
        private ObservableCollection<string> _staffHeaders;

        [ObservableProperty]
        private ObservableCollection<AppointmentViewModel> _appointments;

        [ObservableProperty]
        private double _canvasHeight = 2880; // Default 24 hours * 120px per hour

        [ObservableProperty]
        private double _canvasWidth = 1000; // Will be calculated dynamically based on staff count

        private readonly IStaffService _staffService;
        private readonly IFacilityContextService _facilityContext;
        private readonly ISyncService _syncService;

        public SchedulerViewModel(
            IStaffService staffService,
            IFacilityContextService facilityContext,
            ILogger<SchedulerViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            ISyncService syncService)
            : base(logger, diagnosticService, toastService)
        {
            _staffService = staffService;
            _facilityContext = facilityContext;
            _syncService = syncService;

            _syncService.SyncCompleted += OnSyncCompleted;
            
            _staffHeaders = new ObservableCollection<string>();
            _appointments = new ObservableCollection<AppointmentViewModel>();

            // Load real data
            _ = LoadStaffAsync();
        }

        public void OnScreenActivated() => IsActive = true;
        public void OnScreenDeactivated() => IsActive = false;

        public void SetParameter(object parameter)
        {
            if (parameter is string param && Guid.TryParse(param, out Guid id))
            {
                var appointment = Appointments.FirstOrDefault(a => a.Id == id);
                if (appointment != null)
                {
                    // Selection logic - AppointmentChip usually handles its own visual state
                    // but we can set a property if AppointmentViewModel supports it.
                }
            }
        }

        private async Task LoadStaffAsync()
        {
            await ExecuteSafeAsync(async () =>
            {
                var result = await _staffService.GetAllStaffAsync();
                
                if (result.IsSuccess)
                {
                    StaffHeaders.Clear();
                    foreach (var staff in result.Value)
                    {
                        StaffHeaders.Add(staff.FullName);
                    }
                    
                    UpdateCanvasWidth();
                    _logger.LogInformation($"Loaded {StaffHeaders.Count} staff members for scheduler");
                }
                else
                {
                    _logger.LogError($"Failed to load staff: {result.Error}");
                    ShowError(result.Error.Message);
                }
            });
        }

        private void UpdateCanvasWidth()
        {
            // Each staff column is 200px wide
            const int columnWidth = 200;
            var calculatedWidth = StaffHeaders.Count * columnWidth;
            
            // Minimum width to prevent layout issues
            CanvasWidth = Math.Max(calculatedWidth, 1000);
            
            _logger.LogInformation($"Canvas width updated to {CanvasWidth}px for {StaffHeaders.Count} staff");
        }

        private string GetRandomHexColor(Random rand)
        {
            var colors = new[] { "#FFB6C1", "#ADD8E6", "#90EE90", "#F0E68C", "#E0FFFF", "#D3D3D3" };
            return colors[rand.Next(colors.Length)];
        }

        private void OnSyncCompleted(object? sender, EventArgs e)
        {
            if (!ShouldRefreshOnSync()) return;
            System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (IsDisposed || IsLoading) return;
                _logger?.LogInformation("[Scheduler] Sync debounce passed, refreshing staff list...");
                await LoadStaffAsync();
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_syncService != null)
                {
                    _syncService.SyncCompleted -= OnSyncCompleted;
                }
            }
            base.Dispose(disposing);
        }
    }
}
