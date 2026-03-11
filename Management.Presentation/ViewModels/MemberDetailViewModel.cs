using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Management.Application.Services;
using Management.Application.Interfaces.App;
using Management.Application.Stores;
using Management.Domain.Enums;
using Management.Domain.Services;
using Management.Application.DTOs;
using Management.Presentation.Services;
using Management.Presentation.Extensions;

namespace Management.Presentation.ViewModels
{
    public class MemberDetailViewModel : ViewModelBase, IModalViewModel, IInitializable<object?>
    {
        private readonly IMemberService _memberService;
        private readonly IMembershipPlanService _planService;
        private readonly IAccessEventService _accessEventService;
        private readonly IModalNavigationService _modalService;
        private readonly INotificationService _notificationService;
        private readonly Management.Domain.Services.IFacilityContextService _facilityContext;
        private readonly ITerminologyService _terminologyService;

        public ModalSize PreferredSize => ModalSize.Medium;

        public Task<bool> CanCloseAsync() => Task.FromResult(true);

        // --- STATE ---
        private Guid? _memberId;
        public bool IsEditMode => _memberId.HasValue;
        public string Title => IsEditMode ? "Edit Member" : "Add New Member";

        private string _fullName = string.Empty;
        public string FullName { get => _fullName; set => SetProperty(ref _fullName, value); }

        private string _email = string.Empty;
        public string Email { get => _email; set => SetProperty(ref _email, value); }

        private string _phoneNumber = string.Empty;
        public string PhoneNumber { get => _phoneNumber; set => SetProperty(ref _phoneNumber, value); }

        private string _cardId = string.Empty;
        public string CardId { get => _cardId; set => SetProperty(ref _cardId, value); }

        private MemberStatus _status = MemberStatus.Active;
        public MemberStatus Status { get => _status; set => SetProperty(ref _status, value); }

        private string _notes = string.Empty;
        public string Notes { get => _notes; set => SetProperty(ref _notes, value); }

        private MembershipPlanDto? _selectedPlan;
        public MembershipPlanDto? SelectedPlan { get => _selectedPlan; set => SetProperty(ref _selectedPlan, value); }

        public ObservableCollection<MembershipPlanDto> AvailablePlans { get; } = new();
        public ObservableCollection<AccessEventDto> RecentHistory { get; } = new();

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        // --- COMMANDS ---
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand RenewCommand { get; }

        public MemberDetailViewModel(
            IMemberService memberService,
            IMembershipPlanService planService,
            IAccessEventService accessEventService,
            IModalNavigationService modalService,
            INotificationService notificationService,
            ITerminologyService terminologyService,
            Management.Domain.Services.IFacilityContextService facilityContext)
        {
            _memberService = memberService;
            _planService = planService;
            _accessEventService = accessEventService;
            _modalService = modalService;
            _notificationService = notificationService;
            _terminologyService = terminologyService;
            _facilityContext = facilityContext;

            SaveCommand = new AsyncRelayCommand(ExecuteSaveAsync, CanExecuteSave);
            CancelCommand = new RelayCommand(async () => await _modalService.CloseCurrentModalAsync());
            RenewCommand = new AsyncRelayCommand(ExecuteRenewAsync, () => IsEditMode);
        }

        public async Task InitializeAsync(object? parameter, CancellationToken cancellationToken = default)
        {
            IsBusy = true;
            try
            {
                // 1. Load Plans
                var plansResult = await _planService.GetAllPlansAsync(_facilityContext.CurrentFacilityId);
                if (plansResult.IsSuccess)
                {
                    AvailablePlans.Clear();
                    foreach (var plan in plansResult.Value) AvailablePlans.Add(plan);
                }

                // 2. Load Member if Editing
                if (parameter is Guid memberId)
                {
                    _memberId = memberId;
                    var result = await _memberService.GetMemberAsync(_facilityContext.CurrentFacilityId, memberId);
                    if (result.IsSuccess)
                    {
                        var m = result.Value;
                        FullName = m.FullName;
                        Email = m.Email;
                        PhoneNumber = m.PhoneNumber;
                        CardId = m.CardId;
                        Status = m.Status;
                        Notes = m.Notes;
                        SelectedPlan = AvailablePlans.FirstOrDefault(p => p.Id == m.MembershipPlanId);
                        
                        // Load History
                        var facilityId = _facilityContext.CurrentFacilityId;
                        var historyResult = await _accessEventService.GetRecentEventsAsync(facilityId, 20);
                        if (historyResult.IsSuccess)
                        {
                            RecentHistory.Clear();
                            foreach (var item in historyResult.Value) RecentHistory.Add(item);
                        }
                    }
                }
                else
                {
                    _memberId = null;
                    // Defaults for new member
                    SelectedPlan = AvailablePlans.FirstOrDefault();
                }

                OnPropertyChanged(nameof(IsEditMode));
                OnPropertyChanged(nameof(Title));
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanExecuteSave() => !string.IsNullOrWhiteSpace(FullName) && !string.IsNullOrWhiteSpace(CardId);

        private async Task ExecuteSaveAsync()
        {
            var dto = new MemberDto
            {
                Id = _memberId ?? Guid.Empty,
                FullName = FullName,
                Email = Email,
                PhoneNumber = PhoneNumber,
                CardId = CardId,
                Status = Status,
                Notes = Notes,
                MembershipPlanId = SelectedPlan?.Id
            };

            IsBusy = true;
            try
            {
                var facilityId = _facilityContext.CurrentFacilityId;
                var result = IsEditMode 
                    ? await _memberService.UpdateMemberAsync(facilityId, dto) 
                    : await _memberService.CreateMemberAsync(facilityId, dto);

                if (result.IsSuccess)
                {
                    _notificationService.ShowSuccess($"Member {(IsEditMode ? "updated" : "created")} successfully.");
                    await _modalService.CloseCurrentModalAsync();
                }
                else
                {
                    _notificationService.ShowError(result.Error.Message);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteRenewAsync()
        {
            if (!_memberId.HasValue) return;

            var renewResult = await _memberService.RenewMembersAsync(_facilityContext.CurrentFacilityId, new List<Guid> { _memberId.Value });
            if (renewResult.IsSuccess)
            {
                _notificationService.ShowSuccess(_terminologyService.GetTerm("Strings.Global.Membershiprenewed"));
                // Update local status/expiration if needed (or just close and refresh list)
                await InitializeAsync(_memberId.Value);
            }
            else
            {
                _notificationService.ShowError(renewResult.Error.Message);
            }
        }
    }
}