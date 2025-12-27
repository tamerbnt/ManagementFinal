using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Management.Application.Services;
using Management.Application.Stores;
using Management.Application.Stores;
using Management.Domain.DTOs;
using Management.Domain.DTOs;
using Management.Domain.Services;
using Management.Domain.Services;
using Management.Presentation.Extensions;
using Management.Presentation.Services;
using Management.Presentation.ViewModels;

namespace Management.Presentation.ViewModels
{
    public class MemberDetailViewModel : ViewModelBase, INavigationAware
    {
        private readonly IMemberService _memberService;
        private readonly IAccessEventService _accessEventService;
        private readonly ModalNavigationStore _modalStore;
        private readonly INotificationService _notificationService;

        // --- STATE ---
        private MemberDto _member;
        public MemberDto Member { get => _member; set => SetProperty(ref _member, value); }

        public ObservableCollection<AccessEventDto> RecentHistory { get; }
            = new ObservableCollection<AccessEventDto>();

        // --- COMMANDS ---
        public ICommand CloseCommand { get; }
        public ICommand RenewCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeactivateCommand { get; }

        public MemberDetailViewModel(
            IMemberService memberService,
            IAccessEventService accessEventService,
            ModalNavigationStore modalStore,
            INotificationService notificationService)
        {
            _memberService = memberService;
            _accessEventService = accessEventService;
            _modalStore = modalStore;
            _notificationService = notificationService;

            CloseCommand = new RelayCommand(() => _modalStore.Close());
            RenewCommand = new AsyncRelayCommand(ExecuteRenewAsync);
            EditCommand = new RelayCommand(() => _notificationService.ShowInfo("Edit feature coming in V1.1"));
            DeactivateCommand = new RelayCommand(() => _notificationService.ShowWarning("Deactivate feature coming in V1.1"));
        }

        // --- LIFECYCLE ---
        public async Task OnNavigatedToAsync(object parameter, CancellationToken cancellationToken = default)
        {
            if (parameter is Guid memberId)
            {
                // 1. Load Profile
                Member = await _memberService.GetMemberAsync(memberId);

                // 2. Load History (Parallel)
                // In a real app, this might be triggered only when the History tab is clicked
                // For simplicity, we load recent 20 logs now.
                var history = await _accessEventService.GetRecentEventsAsync(20);
                // Filter for this member specifically if service supports it, otherwise mock logic
                // real implementation: _accessEventService.GetHistoryForMember(id)

                RecentHistory.Clear();
                foreach (var item in history) RecentHistory.Add(item);
            }
        }

        private async Task ExecuteRenewAsync()
        {
            if (Member == null) return;

            // Renew for default duration (e.g. 1 month)
            await _memberService.RenewMembersAsync(new System.Collections.Generic.List<Guid> { Member.Id });

            // Refresh local data
            Member = await _memberService.GetMemberAsync(Member.Id);
            _notificationService.ShowSuccess("Membership renewed successfully.");
        }
    }
}