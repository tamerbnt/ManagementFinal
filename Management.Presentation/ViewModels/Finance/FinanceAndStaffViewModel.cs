using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Presentation.ViewModels.Base;
using Management.Presentation.Extensions;
using Management.Application.Services;
using Microsoft.Extensions.Logging;
using Management.Application.Interfaces.App;
using Management.Presentation.Stores;
using Management.Presentation.Services;
using Management.Presentation.Services.Localization;
using Management.Application.DTOs;
using System.Linq;
using Management.Domain.Services;
using Management.Presentation.Models.History;

namespace Management.Presentation.ViewModels.Finance
{
    public partial class FinanceAndStaffViewModel : FacilityAwareViewModelBase, Management.Application.Interfaces.ViewModels.INavigationalLifecycle, Management.Presentation.ViewModels.Base.IParameterReceiver
    {
        [ObservableProperty]
        private object? _selectedTransaction;

        [ObservableProperty]
        private int _selectedTabIndex;

        [ObservableProperty]
        private bool _isDetailOpen;

        [ObservableProperty]
        private bool _isEditing;

        public CommunityToolkit.Mvvm.Input.IAsyncRelayCommand LoadHistoryCommand { get; }
        public CommunityToolkit.Mvvm.Input.IAsyncRelayCommand<HistoryTransaction> ViewDetailsCommand { get; }
        public CommunityToolkit.Mvvm.Input.IAsyncRelayCommand SaveAuditNoteCommand { get; }
        public CommunityToolkit.Mvvm.Input.IRelayCommand CloseDetailCommand { get; }
        public CommunityToolkit.Mvvm.Input.IRelayCommand<HistoryTransaction> GoToMemberProfileCommand { get; }
        [ObservableProperty]
        private ObservableCollection<StaffMemberViewModel> _staffMembers = new();

        [ObservableProperty]
        private string _viewMode = "List";

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private string _selectedFilter = "All";

        [ObservableProperty]
        private ObservableCollection<StaffMemberViewModel> _filteredStaffMembers = new();

        protected override void OnLanguageChanged()
        {
            Title = GetTerm("Terminology.Staff.Header") ?? "Staff";
        }

        partial void OnSearchQueryChanged(string value) => ApplyFilters();
        partial void OnSelectedFilterChanged(string value) => ApplyFilters();
        
        private void ApplyFilters()
        {
            var filtered = StaffMembers.AsEnumerable();
            System.Diagnostics.Debug.WriteLine($"[STAFF_DIAGNOSTICS] ApplyFilters Start. StaffMembers count: {StaffMembers.Count}, SelectedFilter: {SelectedFilter}, SearchQuery: {SearchQuery}");


            // 1. Filter by Status
            if (!string.IsNullOrEmpty(SelectedFilter) && !SelectedFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(s => !string.IsNullOrEmpty(s.EmploymentStatus) && s.EmploymentStatus.Equals(SelectedFilter, StringComparison.OrdinalIgnoreCase));
            }

            // 2. Filter by Search Query
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var query = SearchQuery.ToLower();
                filtered = filtered.Where(s => 
                    (s.FullName?.ToLower().Contains(query) ?? false) || 
                    (s.Email?.ToLower().Contains(query) ?? false) ||
                    (s.Role?.ToLower().Contains(query) ?? false));
            }

            FilteredStaffMembers.Clear();
            foreach (var staff in filtered)
            {
                FilteredStaffMembers.Add(staff);
            }
            System.Diagnostics.Debug.WriteLine($"[STAFF_DIAGNOSTICS] ApplyFilters End. FilteredStaffMembers count: {FilteredStaffMembers.Count}");
        }

        [ObservableProperty]
        private StaffMemberViewModel? _selectedStaff;

        public async Task SetParameterAsync(object parameter)
        {
            if (parameter is string param && Guid.TryParse(param, out Guid staffId))
            {
                // Ensure data is loaded
                if (StaffMembers.Count == 0 && !IsLoading)
                {
                    await LoadStaffAsync();
                }

                var staffIdStr = staffId.ToString();
                var staff = StaffMembers.FirstOrDefault(s => s.Id == staffIdStr);
                if (staff != null)
                {
                    SelectedTabIndex = 0; // Staff tab
                    SelectedStaff = staff;
                    IsDetailOpen = true;

                    foreach (var s in StaffMembers) s.IsActive = false;
                    staff.IsActive = true;
                }
            }
        }



        public IRelayCommand<string> SwitchViewModeCommand { get; }
        public CommunityToolkit.Mvvm.Input.IAsyncRelayCommand LoadStaffCommand { get; }
        public CommunityToolkit.Mvvm.Input.IAsyncRelayCommand OpenAddStaffCommand { get; } 
        public CommunityToolkit.Mvvm.Input.IRelayCommand PrintStaffListCommand { get; }
        public CommunityToolkit.Mvvm.Input.IRelayCommand<StaffMemberViewModel> OpenStaffDetailCommand { get; }
        public CommunityToolkit.Mvvm.Input.IRelayCommand<StaffMemberViewModel> EditStaffCommand { get; }
        public CommunityToolkit.Mvvm.Input.IAsyncRelayCommand SaveStaffCommand { get; }
        public CommunityToolkit.Mvvm.Input.IAsyncRelayCommand<StaffMemberViewModel> DeleteStaffCommand { get; }
        public CommunityToolkit.Mvvm.Input.IRelayCommand CancelEditCommand { get; }

        private readonly Management.Domain.Services.IDialogService _dialogService;
        private readonly IStaffService _staffService;
        private readonly ISyncService _syncService;
        private readonly ITenantService _tenantService;

        public FinanceAndStaffViewModel(
            ILogger<FinanceAndStaffViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            Management.Domain.Services.IDialogService dialogService,
            ITerminologyService terminologyService,
            IFacilityContextService facilityContext,
            IStaffService staffService,
            ISyncService syncService,
            ITenantService tenantService,
            ILocalizationService localizationService)
            : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService)
        {
            _dialogService = dialogService;
            _staffService = staffService;
            _syncService = syncService;
            _tenantService = tenantService;

            _syncService.SyncCompleted += OnSyncCompleted;
            Title = GetTerm("Terminology.Staff.Header") ?? "Staff";

            LoadStaffCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(() => LoadStaffAsync());
            
            CloseDetailCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => 
            {
                IsDetailOpen = false;
                SelectedStaff = null;
                IsEditing = false;
            });
            GoToMemberProfileCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<HistoryTransaction>(transaction => { /* Navigate to member */ });
            LoadHistoryCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(() => Task.CompletedTask);
            ViewDetailsCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand<HistoryTransaction>(_ => Task.CompletedTask);
            SaveAuditNoteCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(() => Task.CompletedTask);

            SwitchViewModeCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<string>(mode => ViewMode = mode ?? "List");
            
            OpenAddStaffCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(async () => 
            {
               var result = await _dialogService.ShowCustomDialogAsync<AddStaffViewModel>();
               if (result is StaffMemberViewModel newStaff)
               {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                    {
                        StaffMembers.Add(newStaff);
                        ApplyFilters();
                    });
                    _toastService?.ShowSuccess(string.Format(_localizationService?.GetString("Terminology.Staff.Toast.AddSuccess") ?? "Added {0}", newStaff.FullName));
               }
            });

            PrintStaffListCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => _toastService?.ShowSuccess(_localizationService?.GetString("Strings.Finance.Toast.PreparingPrint") ?? "Preparing staff list for printing..."));
            
            OpenStaffDetailCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<StaffMemberViewModel>(staff => 
            {
                if (staff != null) 
                {
                    // If clicking the same staff member while drawer is open, toggle it closed
                    if (IsDetailOpen && SelectedStaff == staff)
                    {
                        IsDetailOpen = false;
                        SelectedStaff = null;
                        IsEditing = false;
                    }
                    else
                    {
                        SelectedStaff = staff;
                        IsDetailOpen = true;
                        IsEditing = false;
                    }
                }
            });

            EditStaffCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<StaffMemberViewModel>(async staff => 
            {
                if (staff != null)
                {
                    var result = await _dialogService.ShowCustomDialogAsync<AddStaffViewModel>(staff);
                    if (result is StaffMemberViewModel updatedStaff)
                    {
                        // Update the original staff member with new values
                        staff.FullName = updatedStaff.FullName;
                        staff.Email = updatedStaff.Email;
                        staff.Phone = updatedStaff.Phone;
                        staff.Role = updatedStaff.Role;
                        staff.Salary = updatedStaff.Salary;
                        staff.PaymentDay = updatedStaff.PaymentDay;
                        staff.EmploymentStatus = updatedStaff.EmploymentStatus;
                        
                        // Sync permissions
                        staff.Permissions.Clear();
                        foreach (var p in updatedStaff.Permissions)
                        {
                            staff.Permissions.Add(new StaffPermission { Name = p.Name, IsGranted = p.IsGranted });
                        }

                        toastService.ShowSuccess(string.Format(_terminologyService.GetTerm("Terminology.Staff.Toast.UpdateSuccess"), updatedStaff.FullName));
                        ApplyFilters();
                    }
                }
            });

            SaveStaffCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(async () => 
            {
                if (SelectedStaff != null)
                {
                    var dto = new StaffDto
                    {
                        Id = Guid.TryParse(SelectedStaff.Id, out var gid) ? gid : Guid.Empty,
                        FullName = SelectedStaff.FullName,
                        Email = SelectedStaff.Email,
                        PhoneNumber = SelectedStaff.Phone,
                        Status = SelectedStaff.EmploymentStatus,
                        Salary = SelectedStaff.Salary,
                        PaymentDay = SelectedStaff.PaymentDay,
                        Role = Enum.TryParse<Management.Domain.Enums.StaffRole>(SelectedStaff.Role, out var role) ? role : Management.Domain.Enums.StaffRole.Staff
                    };

                    var result = await _staffService.UpdateStaffAsync(dto);
                    if (result.IsSuccess)
                    {
                        _toastService?.ShowSuccess(string.Format(_localizationService?.GetString("Terminology.Staff.Toast.UpdateSuccess") ?? "Updated details for {0}", SelectedStaff.FullName));
                        IsEditing = false;
                    }
                    else
                    {
                        _toastService?.ShowError(string.Format(_localizationService?.GetString("Strings.Finance.Toast.UpdateStaffError") ?? "Failed to update staff: {0}", result.Error.Message));
                    }
                }
            });

            DeleteStaffCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand<StaffMemberViewModel>(async staff => 
            {
                if (staff == null) return;

                // Atomic Pattern: Delete -> Save (Service handles) -> Notify with Undo
                var staffId = Guid.Parse(staff.Id);
                var result = await _staffService.RemoveStaffAsync(staffId);
                if (result.IsSuccess)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                    {
                        if (SelectedStaff == staff)
                        {
                            IsDetailOpen = false;
                            SelectedStaff = null;
                        }
                        StaffMembers.Remove(staff);
                        ApplyFilters();
                    });

                    _toastService?.ShowSuccess(
                        $"Staff member '{staff.FullName}' removed.",
                        undoAction: async () => 
                        {
                            var restoreResult = await _staffService.RestoreStaffAsync(staffId);
                            if (restoreResult.IsSuccess)
                            {
                                await LoadStaffAsync(force: true); // Refresh collection
                            }
                        });
                }
                else
                {
                    _toastService?.ShowError(string.Format(_localizationService?.GetString("Terminology.Staff.Toast.DeleteError") ?? "Failed to delete staff: {0}", result.Error.Message));
                }
            });

            CancelEditCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => 
            {
                IsEditing = false;
            });

            _ = LoadStaffAsync();
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public Task PreInitializeAsync() => Task.CompletedTask;

        public async Task LoadDeferredAsync()
        {
            IsActive = true;
            await LoadStaffAsync();
        }

        private async Task LoadStaffAsync(bool force = false)
        {
            if (IsLoading && !force) return;
            
            // --- CONTEXT DIAGNOSTICS ---
            var currentTenant = _tenantService.GetTenantId();
            var currentFacility = _facilityContext.CurrentFacilityId;
            _logger.LogInformation("[Staff] Loading staff list. Context - Tenant: {TenantId}, Facility: {FacilityId}, IsActive: {IsActive}", currentTenant, currentFacility, IsActive);
            System.Diagnostics.Debug.WriteLine($"[STAFF_DIAGNOSTICS] Context before load - Tenant: {currentTenant}, Facility: {currentFacility}");

            IsLoading = true;

            try
            {
                var result = await _staffService.GetAllStaffAsync();

                if (result.IsSuccess)
                {
                    System.Diagnostics.Debug.WriteLine($"[STAFF_DIAGNOSTICS] GetAllStaffAsync SUCCESS. Count: {result.Value.Count}");
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                    {
                        var selectedId = SelectedStaff?.Id;
                        StaffMembers.Clear();
                        foreach (var dto in result.Value)
                        {
                            var vm = new StaffMemberViewModel
                            {
                                Id = dto.Id.ToString(),
                                FullName = dto.FullName,
                                Role = dto.Role.ToString(),
                                Email = dto.Email,
                                Phone = dto.PhoneNumber,
                                EmploymentStatus = dto.Status,
                                HireDate = dto.HireDate,
                                Salary = dto.Salary,
                                PaymentDay = dto.PaymentDay,
                                                                Initials = string.Join("", (dto.FullName ?? "Staff Member").Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(n => n?.FirstOrDefault() ?? 'S')).ToUpper()
                            };

                            vm.Permissions.Clear();
                            foreach (var p in dto.Permissions)
                            {
                                vm.Permissions.Add(new StaffPermission { Name = p.Name, IsGranted = p.IsGranted });
                            }

                            StaffMembers.Add(vm);
                        }
                        ApplyFilters();

                        if (selectedId != null)
                        {
                            var newSelected = StaffMembers.FirstOrDefault(s => s.Id == selectedId);
                            if (newSelected != null)
                            {
                                SelectedStaff = newSelected;
                            }
                        }
                    });
                }
                else
                {
                    _toastService?.ShowError((_localizationService?.GetString("Strings.Finance.Error.LoadStaff") ?? "Failed to load staff list.") + " " + result.Error.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading staff");
                _toastService?.ShowError(_localizationService?.GetString("Strings.Finance.Error.LoadStaff") ?? "Failed to load staff list.");
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
                _logger?.LogInformation("[FinanceAndStaff] Sync debounce passed, refreshing staff list...");
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

    public partial class StaffMemberViewModel : ObservableObject
    {
        [ObservableProperty] private string _id = string.Empty;
        [ObservableProperty] private string _fullName = string.Empty;
        [ObservableProperty] private string _role = string.Empty; // e.g., Manager, Trainer
        [ObservableProperty] private string _email = string.Empty;
        [ObservableProperty] private string _phone = string.Empty;
        public string PhoneNumber => Phone; // Alias for View compatibility
        [ObservableProperty] private string _employmentStatus = "Active"; // Active, Inactive
        [ObservableProperty] private string _initials = string.Empty;
        [ObservableProperty] private bool _isActive;
        [ObservableProperty] private decimal _salary;
        [ObservableProperty] private int _paymentDay;
        [ObservableProperty] private Guid _primaryFacilityId;
        [ObservableProperty] private string _primaryFacilityName = "Main Facility";
        
        [ObservableProperty] private DateTime _hireDate = DateTime.Now.AddYears(-1);
        [ObservableProperty] private ObservableCollection<StaffPermission> _permissions = new();
        [ObservableProperty] private ObservableCollection<StaffPayment> _paymentHistory = new();
        [ObservableProperty] private ObservableCollection<string> _allowedModules = new();
        
        public IRelayCommand RegisterPaymentCommand { get; }

        public StaffMemberViewModel()
        {
            AllowedModules = new ObservableCollection<string>();
            RegisterPaymentCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => 
            {
                // Placeholder logic
            });

            // PaymentHistory remains empty for now until backend support is added
            PaymentHistory = new ObservableCollection<StaffPayment>();
        }
    }

    public class StaffPermission : ObservableObject 
    {
        public string Name { get; set; } = string.Empty;
        private bool _isGranted;
        public bool IsGranted 
        { 
            get => _isGranted; 
            set => SetProperty(ref _isGranted, value); 
        }
    }

    public class StaffPayment 
    {
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string PaymentType { get; set; } = string.Empty;
    }

    public static class StaffViewMode
    {
        public const string Grid = "Grid";
        public const string List = "List";
    }
}
