using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Management.Application.Interfaces.App;
using Management.Application.Services;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Presentation.Services;
using Management.Presentation.ViewModels.Base;
using Management.Presentation.Services.Localization;
using Management.Presentation.Extensions;
using Microsoft.Extensions.Logging;
using Management.Domain.Services;
using Management.Presentation.Messages;

namespace Management.Presentation.ViewModels.Finance
{
    public partial class PayrollHistoryViewModel : FacilityAwareViewModelBase, 
        CommunityToolkit.Mvvm.Messaging.IRecipient<RefreshRequiredMessage<PayrollEntry>>
    {
        private readonly IPayrollRepository _payrollRepository;
        private readonly IStaffRepository _staffRepository;
        private readonly IModalNavigationService _modalNavigationService;

        [ObservableProperty]
        private ObservableCollection<PayrollHistoryItemDto> _history = new();

        [ObservableProperty]
        private ObservableCollection<StaffPaymentAlertDto> _todayAlerts = new();

        public IAsyncRelayCommand CloseCommand { get; }
        public IAsyncRelayCommand<Guid> PayCommand { get; }

        public PayrollHistoryViewModel(
            IPayrollRepository payrollRepository,
            IStaffRepository staffRepository,
            IModalNavigationService modalNavigationService,
            ILogger<PayrollHistoryViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            IFacilityContextService facilityContext,
            ITerminologyService terminologyService,
            ILocalizationService localizationService) 
            : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService)
        {
            _payrollRepository = payrollRepository;
            _staffRepository = staffRepository;
            _modalNavigationService = modalNavigationService;
            
            CloseCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(CloseAsync);
            PayCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand<Guid>(ExecutePayAsync);

            // Register for refresh messages
            WeakReferenceMessenger.Default.Register<RefreshRequiredMessage<PayrollEntry>>(this);
        }

        public override async Task OnModalOpenedAsync(object parameter, System.Threading.CancellationToken cancellationToken = default)
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            await ExecuteLoadingAsync(async () =>
            {
                // 1. Load Alerts (Staff whose PaymentDay is Today)
                var activeStaff = (await _staffRepository.GetAllActiveAsync()).ToList();
                int todayDay = DateTime.Now.Day;
                
                var alerts = activeStaff
                    .Where(s => s.PaymentDay == todayDay)
                    .Select(s => new StaffPaymentAlertDto
                    {
                        StaffId = s.Id,
                        Name = s.FullName,
                        Salary = s.Salary
                    }).ToList();

                TodayAlerts = new ObservableCollection<StaffPaymentAlertDto>(alerts);

                // 2. Load History
                var allEntries = await _payrollRepository.GetAllAsync();
                var historyItems = allEntries
                    .OrderByDescending(e => e.CreatedAt)
                    .Select(e => new PayrollHistoryItemDto
                    {
                        Id = e.Id,
                        StaffName = "Unknown Staff",
                        StaffId = e.StaffId,
                        Date = e.CreatedAt,
                        TotalAmount = e.Amount.Amount,
                        PaidAmount = e.PaidAmount.Amount,
                        Status = e.IsPaid ? GetTerm("Terminology.Payroll.Status.Paid") ?? "Paid" : (e.PaidAmount.Amount > 0 ? GetTerm("Terminology.Payroll.Status.Partial") ?? "Partial" : GetTerm("Terminology.Payroll.Status.Pending") ?? "Pending")
                    }).ToList();

                // Simple name mapping for history
                foreach (var item in historyItems)
                {
                    var staff = activeStaff.FirstOrDefault(s => s.Id == item.StaffId);
                    if (staff != null) item.StaffName = staff.FullName;
                }

                History = new ObservableCollection<PayrollHistoryItemDto>(historyItems);

            }, GetTerm("Terminology.Payroll.History.Loading") ?? "Loading payroll history...");
        }

        private async Task ExecutePayAsync(Guid staffId)
        {
            // Open the processor modal we built previously, passing the staff ID
            await _modalNavigationService.OpenModalAsync<PayrollViewModel>(parameter: staffId);
        }

        public void Receive(RefreshRequiredMessage<PayrollEntry> message)
        {
            if (message.Value == CurrentFacilityId)
            {
                _logger.LogInformation("Refreshing payroll history due to RefreshRequiredMessage");
                _ = LoadDataAsync();
            }
        }

        private async Task CloseAsync()
        {
            _modalNavigationService.CloseModal();
            await Task.CompletedTask;
        }
    }

    public class PayrollHistoryItemDto
    {
        public Guid Id { get; set; }
        public Guid StaffId { get; set; }
        public string StaffName { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal RemainingBalance => TotalAmount - PaidAmount;
        public bool IsPartial => PaidAmount > 0 && PaidAmount < TotalAmount;
    }

    public class StaffPaymentAlertDto
    {
        public Guid StaffId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Salary { get; set; }
    }
}
