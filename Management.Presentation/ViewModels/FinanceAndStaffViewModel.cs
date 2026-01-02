using System;
using Management.Application.Services;
using System.Collections.ObjectModel;
using Management.Application.Services;
using System.Linq;
using Management.Application.Services;
using System.Threading.Tasks;
using Management.Application.Services;
using System.Windows;
using Management.Application.Services;
using System.Windows.Input;
using Management.Application.Services;
using System.Windows.Media;
using Management.Application.Services;
// using Management.Presentation.Services; (Already below)
using Management.Application.DTOs;
using Management.Application.Services;
using Management.Domain.Enums;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Application.Services;
using Management.Presentation.Extensions; // Using our custom extensions
using Management.Presentation.Services;
using Management.Application.Services;
using Clipboard = System.Windows.Clipboard;
using Management.Application.Services;
using Point = System.Windows.Point;
using Management.Application.Services;
using PointCollection = System.Windows.Media.PointCollection;
using Management.Application.Services;

// Removed CommunityToolkit.Mvvm.Input

namespace Management.Presentation.ViewModels
{
    public class FinanceAndStaffViewModel : ViewModelBase
    {
        private readonly IFinanceService _financeService;
        private readonly IStaffService _staffService;
        private readonly IDialogService _dialogService;
        private readonly INotificationService _notificationService;
        private readonly Management.Domain.Services.IFacilityContextService _facilityContext;

        // --- 1. SHELL STATE ---

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        private bool _isDetailOpen;
        public bool IsDetailOpen
        {
            get => _isDetailOpen;
            set => SetProperty(ref _isDetailOpen, value);
        }

        private StaffItemViewModel? _selectedStaffViewModel;
        public StaffItemViewModel? SelectedStaffViewModel
        {
            get => _selectedStaffViewModel;
            set => SetProperty(ref _selectedStaffViewModel, value);
        }

        public ICommand CloseDetailCommand { get; }

        // --- 2. FINANCE DATA ---
        private decimal _monthlyRevenue;
        public decimal MonthlyRevenue { get => _monthlyRevenue; set => SetProperty(ref _monthlyRevenue, value); }
        public double RevenueGrowthPercentage { get; set; }
        public TrendDirection RevenueTrend { get; set; }
        public decimal MRR { get; set; }
        public decimal ARPU { get; set; }
        public double ChurnRate { get; set; }
        public int NewMembersCount { get; set; }
        public int TotalMembersCount { get; set; }
        public double PaymentSuccessRate { get; set; }

        private PointCollection _revenueHistoryPoints = new();
        public PointCollection RevenueHistoryPoints { get => _revenueHistoryPoints; set => SetProperty(ref _revenueHistoryPoints, value); }

        public ObservableCollection<BarChartItemViewModel> MonthlyRevenueChartData { get; } = new ObservableCollection<BarChartItemViewModel>();
        public ObservableCollection<PieChartItemViewModel> PaymentMethodsChartData { get; } = new ObservableCollection<PieChartItemViewModel>();
        public ObservableCollection<FailedPaymentItemViewModel> FailedPayments { get; } = new ObservableCollection<FailedPaymentItemViewModel>();

        public ICommand RetryPaymentCommand { get; }

        // --- 3. STAFF DATA ---

        public ObservableCollection<StaffItemViewModel> StaffMembers { get; } = new ObservableCollection<StaffItemViewModel>();

        // --- 4. CONSTRUCTOR ---

        public FinanceAndStaffViewModel(
            IFinanceService financeService,
            IStaffService staffService,
            IDialogService dialogService,
            INotificationService notificationService,
            Management.Domain.Services.IFacilityContextService facilityContext)
        {
            _financeService = financeService;
            _staffService = staffService;
            _dialogService = dialogService;
            _notificationService = notificationService;
            _facilityContext = facilityContext;

            // Using Project Extensions RelayCommand
            CloseDetailCommand = new RelayCommand(() => IsDetailOpen = false);
            RetryPaymentCommand = new AsyncRelayCommand<FailedPaymentItemViewModel>(ExecuteRetryPaymentAsync);

            _ = LoadDataAsync();
        }

        // --- 5. DATA LOADING ---

        private async Task LoadDataAsync()
        {
            try
            {
                var t1 = LoadFinanceDataAsync();
                var t2 = LoadStaffDataAsync();
                await Task.WhenAll(t1, t2);
            }
            catch (Exception) { /* Handle error */ }
        }

        private async Task LoadFinanceDataAsync()
        {
            var facilityId = _facilityContext.CurrentFacilityId;
            var metricsResult = await _financeService.GetDashboardMetricsAsync(facilityId);
            var failedResult = await _financeService.GetFailedPaymentsAsync(facilityId);

            if (metricsResult.IsSuccess)
            {
                var metrics = metricsResult.Value;
                MonthlyRevenue = metrics.MonthlyRevenue;
                // ... Map other KPIs ...

                // Mock Point Collection Mapping
                var points = new PointCollection();
                foreach (var p in metrics.RevenueSparkline) points.Add(new Point(p.X, p.Y));
                RevenueHistoryPoints = points;
            }

            if (failedResult.IsSuccess)
            {
                FailedPayments.Clear();
                foreach (var fp in failedResult.Value) FailedPayments.Add(new FailedPaymentItemViewModel(fp));
            }
        }

        private async Task LoadStaffDataAsync()
        {
            var result = await _staffService.GetAllStaffAsync();
            if (result.IsFailure) return;

            StaffMembers.Clear();
            foreach (var dto in result.Value)
            {
                var vm = new StaffItemViewModel(dto, _staffService, _dialogService, _notificationService);

                // Wire up Selection
                vm.SelectStaffCommand = new RelayCommand(() => OpenStaffDetail(vm));

                // Wire up Removal Event
                vm.StaffRemoved += OnStaffRemoved;

                StaffMembers.Add(vm);
            }
        }

        // --- 6. INTERACTION LOGIC ---

        private void OpenStaffDetail(StaffItemViewModel staff)
        {
            SelectedStaffViewModel = staff;
            IsDetailOpen = true;
        }

        private void OnStaffRemoved(object? sender, EventArgs e)
        {
            if (sender is StaffItemViewModel vm)
            {
                vm.StaffRemoved -= OnStaffRemoved;
                StaffMembers.Remove(vm);

                if (SelectedStaffViewModel == vm)
                {
                    SelectedStaffViewModel = null;
                    IsDetailOpen = false;
                }
            }
        }

        private async Task ExecuteRetryPaymentAsync(FailedPaymentItemViewModel payment)
        {
            if (payment == null) return;
            try
            {
                var result = await _financeService.RetryPaymentAsync(_facilityContext.CurrentFacilityId, payment.Id);
                if (result.IsSuccess)
                {
                    FailedPayments.Remove(payment);
                    _notificationService.ShowSuccess("Payment retried");
                }
            }
            catch (Exception) { _notificationService.ShowError("Retry failed"); }
        }
    }

    // --- SUB-VIEWMODELS ---

    public class StaffItemViewModel : ViewModelBase
    {
        private readonly StaffDto _dto;
        private readonly IStaffService _service;
        private readonly IDialogService _dialog;
        private readonly INotificationService _notify;

        public Guid Id => _dto.Id;
        public string FullName => _dto.FullName;
        public string Role => _dto.Role.ToString();
        public string Email => _dto.Email;
        public string PhoneNumber => _dto.PhoneNumber;
        public DateTime HireDate => _dto.HireDate;
        public string EmploymentStatus => _dto.Status;

        public ObservableCollection<PermissionItemViewModel> Permissions { get; }

        public ICommand? SelectStaffCommand { get; set; }
        public ICommand? EditCommand { get; }
        public ICommand? RemoveCommand { get; }
        public ICommand? CloseCommand { get; }
 
        public event EventHandler? StaffRemoved;

        public StaffItemViewModel(
            StaffDto dto,
            IStaffService service,
            IDialogService dialog,
            INotificationService notify)
        {
            _dto = dto;
            _service = service;
            _dialog = dialog;
            _notify = notify;

            Permissions = new ObservableCollection<PermissionItemViewModel>(
                dto.Permissions.Select(p => new PermissionItemViewModel { Name = p.Name, IsGranted = p.IsGranted })
            );

            EditCommand = new RelayCommand(() => { /* Edit Modal */ });
            RemoveCommand = new AsyncRelayCommand(ExecuteRemoveAsync);
        }

        private async Task ExecuteRemoveAsync()
        {
            var confirmed = await _dialog.ShowConfirmationAsync(
                "Remove Staff Member?",
                $"Are you sure you want to remove {FullName}?",
                "Remove", "Cancel");

            if (confirmed)
            {
                var result = await _service.RemoveStaffAsync(Id);
                if (result.IsSuccess)
                {
                    _notify.ShowSuccess($"{FullName} removed.");
                    StaffRemoved?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }

    public class FailedPaymentItemViewModel : ViewModelBase
    {
        public Guid Id { get; }
        public string MemberName { get; }
        public decimal Amount { get; }
        public string FailureReason { get; }

        public FailedPaymentItemViewModel(FailedPaymentDto dto)
        {
            Id = dto.Id;
            MemberName = dto.MemberName;
            Amount = dto.Amount;
            FailureReason = dto.Reason;
        }
    }

    public class PermissionItemViewModel { public string Name { get; set; } = string.Empty; public bool IsGranted { get; set; } }
    public class BarChartItemViewModel { /* Label, Value */ }
    public class PieChartItemViewModel { /* Label, Percentage */ }
}