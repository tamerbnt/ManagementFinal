using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Management.Application.Services; // For IDialogService, INavigationService
using Management.Domain.DTOs;
using Management.Domain.Enums;
using Management.Domain.Services;
using Management.Presentation.Extensions;// Using custom RelayCommand/AsyncRelayCommand
using Management.Presentation.Services;

namespace Management.Presentation.ViewModels
{
    public class MembersViewModel : ViewModelBase
    {
        private readonly IMemberService _memberService;
        private readonly INavigationService _navigationService;
        private readonly IDialogService _dialogService; // 1. Added DialogService

        // View List (Bound to Grid)
        public ObservableCollection<MemberListItemViewModel> FilteredMembers { get; }
            = new ObservableCollection<MemberListItemViewModel>();

        // --- 1. FILTER STATE ---

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    _ = RefreshDataAsync();
            }
        }

        private MemberFilterType _currentFilter = MemberFilterType.All;

        public bool FilterAll
        {
            get => _currentFilter == MemberFilterType.All;
            set { if (value) SetFilter(MemberFilterType.All); }
        }

        public bool FilterActive
        {
            get => _currentFilter == MemberFilterType.Active;
            set { if (value) SetFilter(MemberFilterType.Active); }
        }

        public bool FilterExpiring
        {
            get => _currentFilter == MemberFilterType.Expiring;
            set { if (value) SetFilter(MemberFilterType.Expiring); }
        }

        public bool FilterExpired
        {
            get => _currentFilter == MemberFilterType.Expired;
            set { if (value) SetFilter(MemberFilterType.Expired); }
        }

        private void SetFilter(MemberFilterType type)
        {
            if (_currentFilter != type)
            {
                _currentFilter = type;
                OnPropertyChanged(nameof(FilterAll));
                OnPropertyChanged(nameof(FilterActive));
                OnPropertyChanged(nameof(FilterExpiring));
                OnPropertyChanged(nameof(FilterExpired));
                _ = RefreshDataAsync();
            }
        }

        // --- 2. SELECTION STATE ---

        private int _selectedCount;
        public int SelectedCount
        {
            get => _selectedCount;
            set
            {
                if (SetProperty(ref _selectedCount, value))
                {
                    IsSelectionMode = value > 0;
                }
            }
        }

        private bool _isSelectionMode;
        public bool IsSelectionMode
        {
            get => _isSelectionMode;
            set => SetProperty(ref _isSelectionMode, value);
        }

        // --- 3. COMMANDS ---

        public ICommand ClearSelectionCommand { get; }
        public ICommand RenewSelectedCommand { get; }
        public ICommand DeleteSelectedCommand { get; }
        public ICommand PrintReportCommand { get; }

        // --- 4. CONSTRUCTOR ---

        public MembersViewModel(
            IMemberService memberService,
            INavigationService navigationService,
            IDialogService dialogService) // 2. Injected here
        {
            _memberService = memberService;
            _navigationService = navigationService;
            _dialogService = dialogService;

            // Initialize Commands (Using local Extensions)
            ClearSelectionCommand = new RelayCommand(ExecuteClearSelection);
            RenewSelectedCommand = new AsyncRelayCommand(ExecuteRenewSelectedAsync);
            DeleteSelectedCommand = new AsyncRelayCommand(ExecuteDeleteSelectedAsync);
            PrintReportCommand = new RelayCommand(ExecutePrintReport);

            _ = RefreshDataAsync();
        }

        // --- 5. LOGIC COORDINATION ---

        private async Task RefreshDataAsync()
        {
            try
            {
                var pagedResult = await _memberService.SearchMembersAsync(new MemberSearchRequest
                {
                    SearchTerm = SearchText,
                    FilterType = _currentFilter
                });

                FilteredMembers.Clear();

                // 3. Updated Loop Logic
                foreach (var dto in pagedResult.Items)
                {
                    var vm = new MemberListItemViewModel(dto);
                    vm.SelectionChanged += OnMemberSelectionChanged;

                    // FIX: Use DialogService to open the Detail Modal passing the ID
                    vm.ViewDetailsCommand = new AsyncRelayCommand(async () =>
                        await _dialogService.ShowCustomDialogAsync<MemberDetailViewModel>(dto.Id));

                    FilteredMembers.Add(vm);
                }

                RecalculateSelection();
            }
            catch (Exception ex)
            {
                // Log error
            }
        }

        private void OnMemberSelectionChanged(object sender, EventArgs e) => RecalculateSelection();

        private void RecalculateSelection() => SelectedCount = FilteredMembers.Count(m => m.IsSelected);

        private void ExecuteClearSelection()
        {
            foreach (var member in FilteredMembers) member.IsSelected = false;
            RecalculateSelection();
        }

        private async Task ExecuteRenewSelectedAsync()
        {
            var selectedIds = FilteredMembers.Where(m => m.IsSelected).Select(m => m.Id).ToList();
            if (!selectedIds.Any()) return;

            await _memberService.RenewMembersAsync(selectedIds);
            await RefreshDataAsync();
            IsSelectionMode = false;
        }

        private async Task ExecuteDeleteSelectedAsync()
        {
            var selectedIds = FilteredMembers.Where(m => m.IsSelected).Select(m => m.Id).ToList();
            if (!selectedIds.Any()) return;

            await _memberService.DeleteMembersAsync(selectedIds);
            await RefreshDataAsync();
            IsSelectionMode = false;
        }

        private void ExecutePrintReport()
        {
            // TODO: Integrate PrintService
        }
    }

    // --- HELPER VIEW MODEL ---

    public class MemberListItemViewModel : ViewModelBase
    {
        public Guid Id { get; }
        public string FullName { get; }
        public string CardId { get; }
        public string Status { get; }
        public DateTime ExpirationDate { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public ICommand ViewDetailsCommand { get; set; }
        public event EventHandler SelectionChanged;

        public MemberListItemViewModel(MemberDto dto)
        {
            Id = dto.Id;
            FullName = dto.FullName;
            CardId = dto.CardId;
            Status = dto.Status.ToString();
            ExpirationDate = dto.ExpirationDate;
        }
    }
}