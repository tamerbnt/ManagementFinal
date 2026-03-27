using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Management.Application.Interfaces.App;
using Management.Presentation.Messages;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.ValueObjects;
using Management.Presentation.Services.State;
using Management.Presentation.Stores;
using Microsoft.Extensions.Logging;
using MediatR;
using Management.Application.DTOs;
using Management.Application.Services;
using Management.Application.Features.Finance.Commands.CreatePayrollEntry;
using Management.Domain.Services;
using Management.Presentation.ViewModels.Base;
using Management.Presentation.Extensions;
using Management.Presentation.Services;
using Management.Presentation.Services.Localization;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AsyncRelayCommand = CommunityToolkit.Mvvm.Input.AsyncRelayCommand;

namespace Management.Presentation.ViewModels.Finance
{
    public partial class PayrollViewModel : FacilityAwareViewModelBase
    {
        private readonly IPayrollRepository _payrollRepository;
        private readonly IStaffService _staffService;
        private readonly IModalNavigationService _modalNavigationService;
        private readonly IMediator _mediator;

        [ObservableProperty]
        private ObservableCollection<StaffMemberPayrollDto> _staffList = new();

        [ObservableProperty]
        private StaffMemberPayrollDto? _selectedStaff;

        [ObservableProperty]
        private decimal _amountToPay;

        [ObservableProperty]
        private int _absenceDays;

        [ObservableProperty]
        private decimal _deductionPerDay = 500; // Default deduction

        [ObservableProperty]
        private string _paymentMethod = "Cash"; // Cash, Bank Transfer

        public IAsyncRelayCommand PayCommand { get; }
        public IAsyncRelayCommand CancelCommand { get; }

        public PayrollViewModel(
            ILogger<PayrollViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            IPayrollRepository payrollRepository,
            IStaffService staffService, 
            IModalNavigationService modalNavigationService,
            IMediator mediator,
            IFacilityContextService facilityContext,
            ITerminologyService terminologyService,
            ILocalizationService localizationService)
            : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService)
        {
            _payrollRepository = payrollRepository;
            _staffService = staffService;
            _modalNavigationService = modalNavigationService;
            _mediator = mediator;

            PayCommand = new AsyncRelayCommand(ExecutePayAsync, () => SelectedStaff != null && AmountToPay > 0);
            CancelCommand = new AsyncRelayCommand(CloseAsync);
        }

        public override async Task OnModalOpenedAsync(object parameter, System.Threading.CancellationToken cancellationToken = default)
        {
            await LoadStaffPayrollAsync();

            if (parameter is Guid staffId)
            {
                var staffToSelect = StaffList.FirstOrDefault(s => s.Id == staffId);
                if (staffToSelect != null)
                {
                    SelectedStaff = staffToSelect;
                }
            }
        }

        private async Task LoadStaffPayrollAsync()
        {
            await ExecuteLoadingAsync(async () =>
            {
                _logger.LogInformation("Loading payroll for facility {FacilityId}", _facilityContext.CurrentFacilityId);
                
                var staffResult = await _staffService.GetAllStaffAsync();
                if (!staffResult.IsSuccess)
                {
                    _logger.LogError("Failed to load staff: {Error}", staffResult.Error);
                    return;
                }

                var staffData = staffResult.Value.Where(s => s.FacilityId == _facilityContext.CurrentFacilityId).ToList();
                _logger.LogInformation("Loaded {StaffCount} staff members for payroll", staffData.Count);

                var allPayroll = (await _payrollRepository.GetAllAsync()).ToList();
                
                StaffList.Clear();
                foreach (var staff in staffData)
                {
                    var entries = allPayroll.Where(p => p.StaffId == staff.Id).ToList();
                    
                    // Client-side Sum is safe
                    var totalDue = entries.Sum(e => e.Amount.Amount);
                    var totalPaid = entries.Sum(e => e.PaidAmount.Amount);
                    var remaining = totalDue - totalPaid;

                    // ALWAYS add staff to the list if they belong to the facility, 
                    // otherwise the user sees an empty dropdown and thinks it's broken.
                    StaffList.Add(new StaffMemberPayrollDto
                    {
                        Id = staff.Id,
                        Name = staff.FullName,
                        TotalEarned = totalDue,
                        AlreadyPaid = totalPaid,
                        RemainingBalance = remaining,
                        HireDate = staff.HireDate,
                        BaseSalary = staff.Salary,
                        TenantId = staff.TenantId
                    });
                }

                if (StaffList.Any() && SelectedStaff == null)
                {
                    SelectedStaff = StaffList.FirstOrDefault();
                }

            }, GetTerm("Terminology.Payroll.Loading") ?? "Loading staff payroll data...");
        }

        private void UpdateAmountToPay()
        {
            if (SelectedStaff == null) return;
            decimal totalDeduction = AbsenceDays * DeductionPerDay;
            AmountToPay = Math.Max(0, SelectedStaff.BaseSalary - totalDeduction);
        }

        private async Task ExecutePayAsync()
        {
            if (SelectedStaff == null || AmountToPay <= 0) return;

            // Senior Dev Rule: Every payment run creates a historical record
            // capturing the state of the staff (Salary, Absences) at that time.
            await ExecuteLoadingAsync(async () =>
            {
                _logger.LogInformation("Processing payment of {Amount} for {StaffName}", AmountToPay, SelectedStaff.Name);

                DateTime now = DateTime.UtcNow;
                DateTime start = new DateTime(now.Year, now.Month, 1);
                DateTime end = start.AddMonths(1).AddDays(-1);

                // 1. Create the base record via Command to ensure initial persistence
                var cmd = new CreatePayrollEntryCommand(new PayrollEntryDto
                {
                    StaffId = SelectedStaff.Id,
                    PayPeriodStart = start,
                    PayPeriodEnd = end,
                    Amount = AmountToPay,
                    BaseSalary = SelectedStaff.BaseSalary,
                    AbsenceCount = AbsenceDays,
                    AbsenceDeduction = DeductionPerDay
                });

                var idResult = await _mediator.Send(cmd);

                if (idResult.IsSuccess)
                {
                    // 2. Fetch the tracked entity and mark as paid.
                    // This two-stage process ensures EF Core observes the PaidAmount change on a tracked entity,
                    // resolving the 0-value persistence bug in SQLite for owned types.
                    var entry = await _payrollRepository.GetByIdAsync(idResult.Value);
                    if (entry != null)
                    {
                        entry.FacilityId = _facilityContext.CurrentFacilityId;
                        entry.TenantId = SelectedStaff.TenantId;

                        entry.MarkAsPaid();
                        await _payrollRepository.UpdateAsync(entry);

                        _logger.LogInformation("Payroll entry {EntryId} created and paid for staff {StaffId}", entry.Id, SelectedStaff.Id);
                    }
                }

                var successMsg = string.Format(GetTerm("Terminology.Payroll.Success") ?? "Successfully processed payroll of {0} DA for {1}", AmountToPay.ToString("N2"), SelectedStaff.Name);
                
                if (idResult.IsSuccess)
                {
                    _toastService.ShowSuccess(successMsg, async () =>
                    {
                        await _payrollRepository.DeleteAsync(idResult.Value);
                        WeakReferenceMessenger.Default.Send(new RefreshRequiredMessage<PayrollEntry>(_facilityContext.CurrentFacilityId));
                    }, "Undo");
                }
                else
                {
                    _toastService.ShowSuccess(successMsg);
                }

                // Send refresh message to update Dashboard Expenses card
                WeakReferenceMessenger.Default.Send(new RefreshRequiredMessage<PayrollEntry>(_facilityContext.CurrentFacilityId));

                _modalNavigationService.CloseModal();
            }, GetTerm("Terminology.Payroll.Processing") ?? "Processing payment...");
        }

        private async Task CloseAsync()
        {
            _modalNavigationService.CloseModal();
            await Task.CompletedTask;
        }

        partial void OnSelectedStaffChanged(StaffMemberPayrollDto? value)
        {
            if (value != null)
            {
                UpdateAmountToPay();
            }
            PayCommand.NotifyCanExecuteChanged();
        }

        partial void OnAbsenceDaysChanged(int value)
        {
            UpdateAmountToPay();
        }

        partial void OnDeductionPerDayChanged(decimal value)
        {
            UpdateAmountToPay();
        }

        partial void OnAmountToPayChanged(decimal value)
        {
            PayCommand.NotifyCanExecuteChanged();
        }
    }

    public class StaffMemberPayrollDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal TotalEarned { get; set; }
        public decimal AlreadyPaid { get; set; }
        public decimal RemainingBalance { get; set; }
        public DateTime HireDate { get; set; }
        public decimal BaseSalary { get; set; }
        public Guid TenantId { get; set; }
    }
}
