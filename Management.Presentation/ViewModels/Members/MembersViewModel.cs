using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Input;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Management.Presentation.Extensions;
using Management.Presentation.ViewModels.Base;
using Management.Domain.Models;
using Management.Application.Services;
using Management.Application.Interfaces.App;
using Management.Domain.Services;
using Management.Presentation.Services;
using Management.Presentation.Services.Localization;
using Management.Application.DTOs;
using Management.Domain.Enums;
using Management.Presentation.Helpers;
using Management.Application.Interfaces.ViewModels;

namespace Management.Presentation.ViewModels.Members
{
    public enum MemberFilterStatus
    {
        All,
        Active,
        Expiring,
        Expired
    }

    public enum MemberViewMode
    {
        List,
        Grid
    }

    public enum MemberGenderFilter
    {
        All,
        Male,
        Female
    }

    public partial class MembersViewModel : FacilityAwareViewModelBase, IParameterReceiver, INavigationalLifecycle,
        CommunityToolkit.Mvvm.Messaging.IRecipient<Management.Presentation.Messages.RefreshRequiredMessage<Management.Domain.Models.Member>>
    {
        public async Task SetParameterAsync(object parameter)
        {
            if (parameter is string param)
            {
                if (param == "Add")
                {
                    OpenAddMemberCommand.Execute(null);
                }
                else if (Guid.TryParse(param, out Guid memberId))
                {
                    // Ensure data is loaded
                    if (FilteredMembers.Count == 0 && !IsLoading)
                    {
                        await LoadMembersAsync();
                    }

                    var member = FilteredMembers.FirstOrDefault(m => m.Id == memberId);
                    if (member != null)
                    {
                        SelectedMember = member;
                        IsDetailPanelOpen = true;
                        
                        // Highlighting (optional but recommended)
                        foreach (var m in FilteredMembers) m.IsActive = false;
                        member.IsActive = true;
                    }
                }
                else
                {
                    SearchText = param;
                }
            }
        }
        [ObservableProperty]
        private string _terminologyPluralLabel = "Members";

        [ObservableProperty]
        private string _terminologyLabel = "Member";

        protected override void OnLanguageChanged()
        {
            Title = GetTerm("Terminology.Members.Header") ?? "Members";
            TerminologyPluralLabel = GetTerm("Terminology.Members.Plural") ?? "Members";
            TerminologyLabel = GetTerm("Terminology.Members.Single") ?? "Member";
        }

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private MemberFilterStatus _selectedFilter = MemberFilterStatus.All;

        [ObservableProperty]
        private bool _isSelectionMode;

        [ObservableProperty]
        private int _selectedCount;

        // Advanced Filters
        [ObservableProperty]
        private MemberGenderFilter _genderFilter = MemberGenderFilter.All;

        [ObservableProperty]
        private DateTime? _filterStartDate;
        [ObservableProperty]
        private DateTime? _filterEndDate;

        [ObservableProperty]
        private bool _isAnyFilterActive;

        [ObservableProperty]
        private MemberViewMode _viewMode = MemberViewMode.List;

        [ObservableProperty]
        private bool _isDeleteConfirmationVisible;

        [ObservableProperty]
        private bool _selectAll;

        [ObservableProperty]
        private int _currentPage = 1;

        [ObservableProperty]
        private int _pageSize = 25;

        [ObservableProperty]
        private bool _hasMoreItems;

        [ObservableProperty]
        private int _totalCount;

        partial void OnSelectedFilterChanged(MemberFilterStatus value) { LoadMembersCommand.Execute(null); }
        partial void OnGenderFilterChanged(MemberGenderFilter value) { LoadMembersCommand.Execute(null); }
        partial void OnFilterStartDateChanged(DateTime? value) { RefreshFilters(); }
        partial void OnFilterEndDateChanged(DateTime? value) { RefreshFilters(); }

        private CancellationTokenSource? _searchCts;
        private bool _isDirty = true;

        partial void OnSearchTextChanged(string value)
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(400, token);
                    await RefreshFiltersAsync();
                }
                catch (OperationCanceledException) { }
            }, token);
        }

        private async Task RefreshFiltersAsync()
        {
            UpdateIsAnyFilterActive();
            await LoadMembersAsync();
        }

        private void RefreshFilters()
        {
            UpdateIsAnyFilterActive();
            CurrentPage = 1;
            LoadMembersCommand.Execute(null);
        }

        private void UpdateIsAnyFilterActive()
        {
            IsAnyFilterActive = SelectedFilter != MemberFilterStatus.All || 
                               GenderFilter != MemberGenderFilter.All || 
                               FilterStartDate.HasValue || 
                               FilterEndDate.HasValue || 
                               !string.IsNullOrEmpty(SearchText);
        }

        partial void OnSelectAllChanged(bool value)
        {
            // Prevent re-entrancy if updating from individual item change
            if (_isUpdatingSelection) return;

            _isUpdatingSelection = true;
            foreach (var member in FilteredMembers)
            {
                member.IsSelected = value;
            }
            UpdateSelectedCount();
            IsSelectionMode = value || SelectedCount > 0;
            _isUpdatingSelection = false;
        }

        private bool _isUpdatingSelection;

        private void UpdateSelectedCount()
        {
            SelectedCount = FilteredMembers.Count(m => m.IsSelected);
            
            // Sync "Select All" check state
            if (!_isUpdatingSelection)
            {
                _isUpdatingSelection = true;
                if (SelectedCount == 0) SelectAll = false;
                else if (SelectedCount == FilteredMembers.Count) SelectAll = true;
                // Optional: indeterminate state could handle "some selected"
                _isUpdatingSelection = false;
            }
        }

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private MemberDto? _selectedMember;

        [ObservableProperty]
        private bool _isDetailPanelOpen;

        [ObservableProperty]
        private bool _isEditing;

        partial void OnIsDetailPanelOpenChanged(bool value)
        {
            if (!value) IsEditing = false;
        }

        partial void OnSelectedMemberChanged(MemberDto? oldValue, MemberDto? newValue)
        {
            if (oldValue != null) oldValue.IsActive = false;
            if (newValue != null) newValue.IsActive = true;
        }

        private readonly object _membersLock = new();
        private MemberDto? _memberToDelete;

        public ObservableRangeCollection<MemberDto> FilteredMembers { get; } = new();

        public IAsyncRelayCommand PrintReportCommand { get; }
        public IAsyncRelayCommand RenewSelectedCommand { get; }
        public IRelayCommand<MemberDto> DeleteSingleMemberCommand { get; }

        public IRelayCommand DeleteSelectedCommand { get; }
        public IAsyncRelayCommand DeleteConfirmedCommand { get; }
        public IRelayCommand CancelDeleteCommand { get; }
        public IAsyncRelayCommand ExportCommand { get; }
        public IRelayCommand ClearSelectionCommand { get; }
        public IRelayCommand OpenAddMemberCommand { get; }
        public IRelayCommand<MemberDto> OpenDetailCommand { get; }
        public IRelayCommand<MemberDto> EditMemberCommand { get; }
        public IRelayCommand CloseDetailCommand { get; }
        public IRelayCommand SaveMemberCommand { get; }
        public IRelayCommand ToggleSelectionCommand { get; }
        public IRelayCommand<MemberViewMode> SwitchViewModeCommand { get; }
        public IRelayCommand ResetFiltersCommand { get; }

        public IAsyncRelayCommand LoadMembersCommand { get; }
        public IAsyncRelayCommand LoadMoreCommand { get; }

        private readonly IMemberService _memberService;
        private readonly Management.Domain.Services.IDialogService _dialogService;
        private readonly ISyncService _syncService;

        public Task PreInitializeAsync()
        {
            Title = GetTerm("Terminology.Members.Header") ?? "Members";
            return Task.CompletedTask;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task LoadDeferredAsync()
        {
            IsActive = true;
            if (!_isDirty && !IsLoading) return;
            
            await ExecuteLoadingAsync(async () =>
            {
                _isDirty = false;
                await LoadMembersAsync();
            });
        }

        public MembersViewModel(
            ILogger<MembersViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            IMemberService memberService,
            Management.Domain.Services.IDialogService dialogService,
            IFacilityContextService facilityContext,
            ISyncService syncService,
            ITerminologyService terminologyService,
            ILocalizationService localizationService)
            : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService)
        {
            _memberService = memberService;
            _dialogService = dialogService;
            _syncService = syncService;

            _syncService.SyncCompleted += OnSyncCompleted;
            
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Register<Management.Presentation.Messages.RefreshRequiredMessage<Management.Domain.Models.Member>>(this);

            PrintReportCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(() => Task.CompletedTask);
            RenewSelectedCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(() => Task.CompletedTask);
            
            // Delete Flow: Show Confirmation -> Execute
            DeleteSingleMemberCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<MemberDto>(member => 
            {
                if (member == null) return;
                _memberToDelete = member;
                IsDeleteConfirmationVisible = true;
            });

            DeleteSelectedCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => 
            {
                if (SelectedCount > 0) 
                {
                    _memberToDelete = null; 
                    IsDeleteConfirmationVisible = true;
                }
            });

            DeleteConfirmedCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(async () => 
            {
                var idsToDelete = new List<Guid>();
                if (_memberToDelete != null)
                {
                    idsToDelete.Add(_memberToDelete.Id);
                }
                else
                {
                    idsToDelete.AddRange(FilteredMembers.Where(m => m.IsSelected).Select(m => m.Id));
                }

                if (!idsToDelete.Any())
                {
                    IsDeleteConfirmationVisible = false;
                    return;
                }

                await ExecuteLoadingAsync(async () => 
                {
                    var result = await _memberService.DeleteMembersAsync(_facilityContext.CurrentFacilityId, idsToDelete);
                    if (result.IsSuccess)
                    {
                        var toRemove = FilteredMembers.Where(m => idsToDelete.Contains(m.Id)).ToList();
                        foreach(var item in toRemove)
                        {
                            FilteredMembers.Remove(item);
                        }
                        
                        toastService.ShowSuccess($"Deleted {idsToDelete.Count} member(s) successfully.");
                        IsSelectionMode = false;
                        SelectedCount = 0;
                        SelectAll = false;
                        _memberToDelete = null;
                        IsDeleteConfirmationVisible = false;
                    }
                    else
                    {
                        toastService.ShowError($"Failed to delete member(s): {result.Error.Message}");
                    }
                });
            });

            CancelDeleteCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => 
            {
                IsDeleteConfirmationVisible = false;
                _memberToDelete = null;
            });
            
            ExportCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(() => Task.CompletedTask);
            ClearSelectionCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => IsSelectionMode = false);
            OpenAddMemberCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(async () => 
            {
                await _dialogService.ShowCustomDialogAsync<QuickRegistrationViewModel>();
            });
            OpenDetailCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand<MemberDto>(async member => 
            {
                if (member != null)
                {
                    await ExecuteLoadingAsync(async () => {
                        var result = await _memberService.GetMemberAsync(_facilityContext.CurrentFacilityId, member.Id);
                        if (result.IsSuccess)
                        {
                            // Update the existing DTO with fresh data instead of replacing it,
                            // to keep the selection and UI state stable.
                            member.VisitCount = result.Value.VisitCount;
                            member.AccessEvents = result.Value.AccessEvents;
                            member.Balance = result.Value.Balance;
                            member.MembershipPlanName = result.Value.MembershipPlanName;
                            // Add any other fields that might be updated
                            
                            SelectedMember = member;
                            IsDetailPanelOpen = true;
                        }
                    });
                }
            });
            EditMemberCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand<MemberDto>(async member => 
            {
                if (member != null)
                {
                    await _dialogService.ShowCustomDialogAsync<QuickRegistrationViewModel>(member.Id);
                }
            });

            SaveMemberCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => 
            {
                if (SelectedMember != null)
                {
                    // Logic to save changes would go here
                    _toastService.ShowSuccess($"Saved {SelectedMember.FullName}");
                    IsEditing = false;
                }
            });
            CloseDetailCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => 
            {
                IsDetailPanelOpen = false;
                SelectedMember = null;
                IsEditing = false;
            });
            ToggleSelectionCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => IsSelectionMode = !IsSelectionMode);
            SwitchViewModeCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<MemberViewMode>(mode => ViewMode = mode);
            ResetFiltersCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ResetFilters);
            
            LoadMembersCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(async () => 
            {
                CurrentPage = 1;
                await LoadMembersAsync(false);
            });

            LoadMoreCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(async () => 
            {
                if (!HasMoreItems || IsLoading) return;
                CurrentPage++;
                await LoadMembersAsync(true);
            });

            BindingOperations.EnableCollectionSynchronization(FilteredMembers, _membersLock);

            // Hook into collection changes to manage individual item observers
            FilteredMembers.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (MemberDto m in e.NewItems)
                        m.PropertyChanged += OnMemberPropertyChanged;
                }
                if (e.OldItems != null)
                {
                    foreach (MemberDto m in e.OldItems)
                        m.PropertyChanged -= OnMemberPropertyChanged;
                }
            };
        }

        private void ResetFilters()
        {
            // Reset status filters
            SelectedFilter = MemberFilterStatus.All;

            // Reset gender filters
            GenderFilter = MemberGenderFilter.All;

            // Reset date filters
            FilterStartDate = null;
            FilterEndDate = null;

            // Reset search
            SearchText = string.Empty;

            UpdateIsAnyFilterActive();
            LoadMembersCommand.Execute(null);
        }

        private void OnMemberPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MemberDto.IsSelected))
            {
                UpdateSelectedCount();
                IsSelectionMode = SelectedCount > 0;
            }
        }


        private async Task LoadMembersAsync()
        {
            await LoadMembersAsync(false);
        }

        private async Task LoadMembersAsync(bool isLoadMore)
        {
            if (IsLoading) return;
            IsLoading = true;

            try
            {
                // Mapping UI Enums to Search Request
                var filterType = SelectedFilter switch
                {
                    MemberFilterStatus.Active => MemberFilterType.Active,
                    MemberFilterStatus.Expiring => MemberFilterType.Expiring,
                    MemberFilterStatus.Expired => MemberFilterType.Expired,
                    _ => MemberFilterType.All
                };

                Gender? gender = GenderFilter switch
                {
                    MemberGenderFilter.Male => Gender.Male,
                    MemberGenderFilter.Female => Gender.Female,
                    _ => null
                };

                var request = new MemberSearchRequest(
                    SearchText,
                    filterType,
                    gender,
                    FilterStartDate,
                    FilterEndDate);

                var result = await _memberService.SearchMembersAsync(_facilityContext.CurrentFacilityId, request, CurrentPage, PageSize);

                if (result.IsSuccess)
                {
                    var selectedId = SelectedMember?.Id;
                    
                    if (isLoadMore)
                    {
                        FilteredMembers.AddRange(result.Value.Items);
                    }
                    else
                    {
                        FilteredMembers.ReplaceRange(result.Value.Items);
                    }

                    TotalCount = result.Value.TotalCount;
                    HasMoreItems = FilteredMembers.Count < TotalCount;
                    
                    if (selectedId != null)
                    {
                        var newSelected = FilteredMembers.FirstOrDefault(m => m.Id == selectedId);
                        if (newSelected != null)
                        {
                            SelectedMember = newSelected;
                        }
                    }
                }
                else
                {
                    _toastService.ShowError("Failed to load members: " + result.Error.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading members");
                _toastService.ShowError("An unexpected error occurred while loading members.");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void OnSyncCompleted(object? sender, EventArgs e)
        {
            if (!ShouldRefreshOnSync()) return;
            System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (IsDisposed || IsLoading) return;
                _logger?.LogInformation("[Members] Sync debounce passed, refreshing member list...");
                _isDirty = true;
                await LoadDeferredAsync();
            });
        }

        public void Receive(Management.Presentation.Messages.RefreshRequiredMessage<Management.Domain.Models.Member> message)
        {
            if (message.Value == _facilityContext.CurrentFacilityId)
            {
                _isDirty = true;
                _logger.LogInformation("[Members] Marked dirty due to Member change.");
                // Trigger immediate reload so new members appear without needing navigation
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(async () =>
                {
                    if (!IsDisposed) await LoadMembersAsync();
                });
            }
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
