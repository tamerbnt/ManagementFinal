using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Management.Domain.DTOs;
using Management.Domain.Enums;
using Management.Domain.Services;
using Management.Presentation.Extensions;
using Management.Presentation.Services;
using MediatR;
using Management.Application.Features.Registrations.Queries.SearchRegistrations;
using Management.Application.Features.Registrations.Commands.ApproveRegistrations;

namespace Management.Presentation.ViewModels
{
    public class RegistrationsViewModel : ViewModelBase
    {
        private readonly IMediator _mediator;
        private readonly IRegistrationService _registrationService; // Still needed for decline for now
        private readonly INavigationService _navigationService;
        private readonly IDialogService _dialogService; 
        private readonly INotificationService _notificationService;

        // View List (Bound to UI)
        public ObservableCollection<RegistrationListItemViewModel> Registrations { get; }
            = new ObservableCollection<RegistrationListItemViewModel>();

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

        private RegistrationFilterType _currentFilter = RegistrationFilterType.All;

        public bool FilterAll
        {
            get => _currentFilter == RegistrationFilterType.All;
            set { if (value) SetFilter(RegistrationFilterType.All); }
        }

        public bool FilterNew
        {
            get => _currentFilter == RegistrationFilterType.New;
            set { if (value) SetFilter(RegistrationFilterType.New); }
        }

        public bool FilterPriority
        {
            get => _currentFilter == RegistrationFilterType.Priority;
            set { if (value) SetFilter(RegistrationFilterType.Priority); }
        }

        private void SetFilter(RegistrationFilterType type)
        {
            if (_currentFilter != type)
            {
                _currentFilter = type;
                OnPropertyChanged(nameof(FilterAll));
                OnPropertyChanged(nameof(FilterNew));
                OnPropertyChanged(nameof(FilterPriority));
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
                    IsSelectionMode = value > 0;
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
        public ICommand ApproveSelectedCommand { get; }
        public ICommand DeclineSelectedCommand { get; }
        public ICommand PrintReportCommand { get; }

        // --- 4. CONSTRUCTOR ---

        public RegistrationsViewModel(
            IMediator mediator,
            IRegistrationService registrationService,
            INavigationService navigationService,
            IDialogService dialogService,
            INotificationService notificationService)
        {
            _mediator = mediator;
            _registrationService = registrationService;
            _navigationService = navigationService;
            _dialogService = dialogService;
            _notificationService = notificationService;

            // Use Custom Extensions (Not CommunityToolkit)
            ClearSelectionCommand = new RelayCommand(ExecuteClearSelection);
            ApproveSelectedCommand = new AsyncRelayCommand(ExecuteApproveSelectedAsync);
            DeclineSelectedCommand = new AsyncRelayCommand(ExecuteDeclineSelectedAsync);
            PrintReportCommand = new RelayCommand(ExecutePrintReport);

            // Initial Load
            _ = RefreshDataAsync();
        }

        // --- 5. LOGIC COORDINATION ---

        private async Task RefreshDataAsync()
        {
            try
            {
                // Delegate all logic to Application Layer
                var result = await _registrationService.SearchAsync(new RegistrationSearchRequest(SearchText, _currentFilter));
                
                if (result.IsFailure)
                {
                    _notificationService.ShowError($"Error loading registrations: {result.Error.Message}");
                    return;
                }

                // Update UI Collection
                Registrations.Clear();

                foreach (var dto in result.Value.Items)
                {
                    var vm = new RegistrationListItemViewModel(dto);

                    // Wire up Events & Commands
                    vm.SelectionChanged += OnItemSelectionChanged;
                    vm.ApproveCommand = new AsyncRelayCommand(async () => await ExecuteApproveSingleAsync(vm));
                    vm.DeclineCommand = new AsyncRelayCommand(async () => await ExecuteDeclineSingleAsync(vm));

                    // FIX 3: Use DialogService for Details (Popup Architecture)
                    // Assuming RegistrationDetailViewModel exists
                    vm.ViewDetailsCommand = new AsyncRelayCommand(async () =>
                        await _dialogService.ShowCustomDialogAsync<RegistrationDetailViewModel>(dto.Id));

                    Registrations.Add(vm);
                }

                RecalculateSelection();
            }
            catch (Exception)
            {
                // _notificationService.ShowError("Failed to load registrations");
            }
        }

        // --- 6. SELECTION HANDLING ---

        private void OnItemSelectionChanged(object? sender, EventArgs e) => RecalculateSelection();

        private void RecalculateSelection()
        {
            SelectedCount = Registrations.Count(x => x.IsSelected);
        }

        private void ExecuteClearSelection()
        {
            foreach (var item in Registrations) item.IsSelected = false;
            RecalculateSelection();
        }

        // --- 7. ACTIONS (Service Calls + Refresh) ---

        private async Task ExecuteApproveSingleAsync(RegistrationListItemViewModel item)
        {
            await _mediator.Send(new ApproveRegistrationsCommand(new List<Guid> { item.Id }));
            _notificationService.ShowSuccess($"✓ {item.FullName} approved");
            await RefreshDataAsync();
        }

        private async Task ExecuteDeclineSingleAsync(RegistrationListItemViewModel item)
        {
            await _registrationService.DeclineRegistrationAsync(item.Id);
            _notificationService.ShowInfo($"{item.FullName} declined");
            await RefreshDataAsync();
        }

        private async Task ExecuteApproveSelectedAsync()
        {
            var selectedIds = Registrations.Where(x => x.IsSelected).Select(x => x.Id).ToList();
            if (!selectedIds.Any()) return;

            await _mediator.Send(new ApproveRegistrationsCommand(selectedIds));

            _notificationService.ShowSuccess($"✓ {selectedIds.Count} registrations approved");

            IsSelectionMode = false;
            await RefreshDataAsync();
        }

        private async Task ExecuteDeclineSelectedAsync()
        {
            var selectedIds = Registrations.Where(x => x.IsSelected).Select(x => x.Id).ToList();
            if (!selectedIds.Any()) return;

            var confirmed = await _dialogService.ShowConfirmationAsync(
                "Decline Registrations?",
                $"Are you sure you want to decline {selectedIds.Count} leads? This cannot be undone.",
                "Decline All",
                "Cancel");

            if (!confirmed) return;

            await _registrationService.DeclineBatchAsync(selectedIds);

            _notificationService.ShowInfo($"{selectedIds.Count} registrations declined");

            IsSelectionMode = false;
            await RefreshDataAsync();
        }

        private void ExecutePrintReport()
        {
            // _printService.PrintRegistrations();
        }
    }

    // --- SUB-VIEWMODEL ---

    public class RegistrationListItemViewModel : ViewModelBase
    {
        public Guid Id { get; }
        public string FullName { get; }
        public string Source { get; }
        public string PhoneNumber { get; }
        public DateTime CreatedAt { get; }

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

        public ICommand ApproveCommand { get; set; } = null!;
        public ICommand DeclineCommand { get; set; } = null!;
        public ICommand ViewDetailsCommand { get; set; } = null!;
 
        public event EventHandler? SelectionChanged;

        public RegistrationListItemViewModel(RegistrationDto dto)
        {
            Id = dto.Id;
            FullName = dto.FullName;
            Source = dto.Source;
            PhoneNumber = dto.PhoneNumber;
            CreatedAt = dto.CreatedAt;
        }
    }
}