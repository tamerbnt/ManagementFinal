using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Input;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Presentation.Extensions;
using Microsoft.Extensions.Logging;
using Management.Application.Services;
using Management.Application.Interfaces.App;
using Management.Presentation.Services;
using Management.Domain.Services;
using Management.Presentation.Helpers;
using Management.Application.DTOs;

namespace Management.Presentation.ViewModels.Registrations
{
    public enum RegistrationFilterStatus
    {
        All,
        Pending,
        Confirmed,
        Declined
    }

    public enum RegistrationViewMode
    {
        List,
        Grid
    }

    public partial class RegistrationsViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _terminologyPluralLabel = "Registrations";

        public Guid FacilityId => _facilityContext.CurrentFacilityId;

        [ObservableProperty]
        private string _terminologyLabel = "Registration";

        [ObservableProperty]
        private bool _isSelectionMode;

        [ObservableProperty]
        private int _selectedCount;

        [ObservableProperty]
        private bool _isDetailOpen;

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private RegistrationItemViewModel? _selectedRegistration;

        [ObservableProperty]
        private RegistrationFilterStatus _selectedFilter = RegistrationFilterStatus.All;

        [ObservableProperty]
        private RegistrationViewMode _viewMode = RegistrationViewMode.List;

        [ObservableProperty]
        private bool _isAnyFilterActive;

        [ObservableProperty]
        private int _pageNumber = 1;

        [ObservableProperty]
        private int _pageSize = 20;

        [ObservableProperty]
        private int _totalCount;
        
        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _selectAll;

        private bool _isUpdatingSelection;

        public ObservableRangeCollection<RegistrationItemViewModel> Registrations { get; } = new();
        public ObservableRangeCollection<RegistrationItemViewModel> FilteredRegistrations { get; } = new();

        public IAsyncRelayCommand LoadRegistrationsCommand { get; }
        public IAsyncRelayCommand NextPageCommand { get; }
        public IAsyncRelayCommand PreviousPageCommand { get; }
        public IAsyncRelayCommand PrintReportCommand { get; }
        public IAsyncRelayCommand ApproveSelectedCommand { get; }
        public IAsyncRelayCommand DeclineSelectedCommand { get; }
        public IRelayCommand ClearSelectionCommand { get; }
        public IRelayCommand CloseDetailCommand { get; }
        public IRelayCommand EditRegistrationCommand { get; }
        public IRelayCommand SaveRegistrationCommand { get; }
        public IRelayCommand CancelEditCommand { get; }
        public IRelayCommand<RegistrationViewMode> SwitchViewModeCommand { get; }
        public IRelayCommand ResetFiltersCommand { get; }

        private readonly IRegistrationService _registrationService;
        private readonly IFacilityContextService _facilityContext;

        public RegistrationsViewModel(
            ILogger<RegistrationsViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            IRegistrationService registrationService,
            IFacilityContextService facilityContext)
            : base(logger, diagnosticService, toastService)
        {
            _registrationService = registrationService;
            _facilityContext = facilityContext;
            Title = "Registrations";
            
            LoadRegistrationsCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(LoadRegistrationsAsync);
            NextPageCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(async () => { PageNumber++; await LoadRegistrationsAsync(); });
            PreviousPageCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(async () => { if (PageNumber > 1) { PageNumber--; await LoadRegistrationsAsync(); } });
            PrintReportCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(() => Task.CompletedTask);
            
            ApproveSelectedCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(async () => 
            {
                var selectedIds = FilteredRegistrations.Where(r => r.IsSelected).Select(r => r.Id).ToList();
                if (selectedIds.Count == 0) return;

                var result = await _registrationService.ApproveBatchAsync(selectedIds, _facilityContext.CurrentFacilityId);
                if (result.IsSuccess)
                {
                    _toastService.ShowSuccess($"Approved {selectedIds.Count} registrations.");
                    await LoadRegistrationsAsync();
                }
                else
                {
                    _toastService.ShowError("Failed to approve registrations: " + result.Error.Message);
                }
            });

            DeclineSelectedCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(async () => 
            {
                var selectedIds = FilteredRegistrations.Where(r => r.IsSelected).Select(r => r.Id).ToList();
                if (selectedIds.Count == 0) return;

                var result = await _registrationService.DeclineBatchAsync(selectedIds, _facilityContext.CurrentFacilityId);
                if (result.IsSuccess)
                {
                    _toastService.ShowSuccess($"Declined {selectedIds.Count} registrations.");
                    await LoadRegistrationsAsync();
                }
                else
                {
                    _toastService.ShowError("Failed to decline registrations: " + result.Error.Message);
                }
            });
            
            ClearSelectionCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => {
                foreach (var r in Registrations) r.IsSelected = false;
                IsSelectionMode = false;
            });

            CloseDetailCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => {
                IsDetailOpen = false;
                SelectedRegistration = null;
                IsEditing = false;
            });

            EditRegistrationCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => IsEditing = true);
            
            SaveRegistrationCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => {
                _toastService.ShowSuccess("Registration updated");
                IsEditing = false;
            });

            CancelEditCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => IsEditing = false);
            SwitchViewModeCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<RegistrationViewMode>(mode => ViewMode = mode);
            ResetFiltersCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ResetFilters);

            FilteredRegistrations.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (RegistrationItemViewModel r in e.NewItems)
                        r.PropertyChanged += OnRegistrationPropertyChanged;
                }
                if (e.OldItems != null)
                {
                    foreach (RegistrationItemViewModel r in e.OldItems)
                        r.PropertyChanged -= OnRegistrationPropertyChanged;
                }
            };

            _ = LoadRegistrationsAsync();
        }

        partial void OnSelectedFilterChanged(RegistrationFilterStatus value) => _ = LoadRegistrationsAsync();
        private CancellationTokenSource? _searchCts;

        partial void OnSearchTextChanged(string value)
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(300, token);
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => 
                    {
                        PageNumber = 1;
                        await LoadRegistrationsAsync();
                    });
                }
                catch (OperationCanceledException) { }
            }, token);
        }

        partial void OnSelectAllChanged(bool value)
        {
            if (_isUpdatingSelection) return;
            _isUpdatingSelection = true;
            foreach (var r in FilteredRegistrations) r.IsSelected = value;
            UpdateSelectedCount();
            _isUpdatingSelection = false;
        }

        partial void OnSelectedRegistrationChanged(RegistrationItemViewModel? oldValue, RegistrationItemViewModel? newValue)
        {
            if (oldValue != null) oldValue.IsActive = false;
            if (newValue != null) newValue.IsActive = true;
        }

        private async Task LoadRegistrationsAsync()
        {
            if (IsLoading) return;
            IsLoading = true;

            try
            {
                var request = new RegistrationSearchRequest(
                    SearchText,
                    Domain.Enums.RegistrationFilterType.All,
                    MapFilterStatus(SelectedFilter));

                var result = await _registrationService.SearchAsync(request, _facilityContext.CurrentFacilityId, PageNumber, PageSize);

                if (result.IsSuccess)
                {
                    var pagedResult = result.Value;
                    TotalCount = pagedResult.TotalCount;

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                    {
                        var viewModels = pagedResult.Items.Select(dto => new RegistrationItemViewModel(this, _registrationService, _toastService)
                        {
                            Id = dto.Id,
                            FullName = dto.FullName,
                            Email = dto.Email ?? string.Empty,
                            PhoneNumber = dto.PhoneNumber ?? string.Empty,
                            Source = dto.Source ?? string.Empty,
                            CreatedAt = dto.CreatedAt,
                            PlanName = dto.PreferredPlanName ?? "None",
                            Message = dto.Notes ?? string.Empty,
                            Status = dto.Status.ToString()
                        }).ToList();

                        Registrations.ReplaceRange(viewModels);
                        FilteredRegistrations.ReplaceRange(viewModels);
                        IsAnyFilterActive = SelectedFilter != RegistrationFilterStatus.All || !string.IsNullOrEmpty(SearchText);
                        UpdateSelectedCount();
                    });
                }
                else
                {
                    _toastService.ShowError("Failed to load registrations: " + result.Error.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading registrations");
                _toastService.ShowError("An unexpected error occurred while loading registrations.");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private Domain.Enums.RegistrationStatus? MapFilterStatus(RegistrationFilterStatus status)
        {
            return status switch
            {
                RegistrationFilterStatus.Pending => Domain.Enums.RegistrationStatus.Pending,
                RegistrationFilterStatus.Confirmed => Domain.Enums.RegistrationStatus.Approved,
                RegistrationFilterStatus.Declined => Domain.Enums.RegistrationStatus.Declined,
                _ => null
            };
        }

        private void OnRegistrationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RegistrationItemViewModel.IsSelected))
            {
                UpdateSelectedCount();
                IsSelectionMode = SelectedCount > 0;
            }
        }

        private void UpdateSelectedCount()
        {
            SelectedCount = FilteredRegistrations.Count(r => r.IsSelected);
            if (!_isUpdatingSelection)
            {
                _isUpdatingSelection = true;
                if (SelectedCount == 0) SelectAll = false;
                else if (SelectedCount > 0 && SelectedCount == FilteredRegistrations.Count) SelectAll = true;
                _isUpdatingSelection = false;
            }
        }

        private void ResetFilters()
        {
            SelectedFilter = RegistrationFilterStatus.All;
            SearchText = string.Empty;
            PageNumber = 1;
            _ = LoadRegistrationsAsync();
        }
    }

    public partial class RegistrationItemViewModel : ObservableObject
    {
        private readonly RegistrationsViewModel _parent;
        private readonly IRegistrationService _registrationService;
        private readonly IToastService _toastService;

        [ObservableProperty] private Guid _id;
        [ObservableProperty] private bool _isSelected;
        [ObservableProperty] private bool _isActive;
        [ObservableProperty] private string _fullName = string.Empty;
        [ObservableProperty] private string _email = string.Empty;
        [ObservableProperty] private string _phoneNumber = string.Empty;
        [ObservableProperty] private string _source = string.Empty;
        [ObservableProperty] private DateTime _createdAt;
        [ObservableProperty] private string _planName = string.Empty;
        [ObservableProperty] private string _message = string.Empty;
        [ObservableProperty] private string _status = "Pending";

        public IRelayCommand ViewDetailsCommand { get; }
        public IAsyncRelayCommand ApproveCommand { get; }
        public IAsyncRelayCommand DeclineCommand { get; }

        public RegistrationItemViewModel(RegistrationsViewModel parent, IRegistrationService registrationService, IToastService toastService)
        {
            _parent = parent;
            _registrationService = registrationService;
            _toastService = toastService;

            ViewDetailsCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => {
                _parent.SelectedRegistration = this;
                _parent.IsDetailOpen = true;
                _parent.IsEditing = false;
            });

            ApproveCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(async () => {
                var result = await _registrationService.ApproveRegistrationAsync(_parent.FacilityId, Id);
                if (result.IsSuccess)
                {
                    Status = "Confirmed";
                    _toastService.ShowSuccess($"Approved {FullName}");
                    // Optionally refresh the whole list if we want to remove it from "Pending" view
                    _ = _parent.LoadRegistrationsCommand.ExecuteAsync(null);
                }
                else
                {
                    _toastService.ShowError($"Failed to approve: {result.Error.Message}");
                }
            });

            DeclineCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(async () => {
                var result = await _registrationService.DeclineRegistrationAsync(_parent.FacilityId, Id);
                if (result.IsSuccess)
                {
                    Status = "Declined";
                    _toastService.ShowSuccess($"Declined {FullName}");
                    _ = _parent.LoadRegistrationsCommand.ExecuteAsync(null);
                }
                else
                {
                    _toastService.ShowError($"Failed to decline: {result.Error.Message}");
                }
            });
        }
    }
}
