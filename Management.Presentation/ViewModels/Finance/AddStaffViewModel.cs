using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.Interfaces.App;
using Management.Application.Services; 
using Management.Presentation.Extensions; 
using Management.Presentation.ViewModels.Base;
using Management.Presentation.Stores;
using Management.Domain.Services;
using Management.Presentation.Services.Localization;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Management.Domain.Primitives;
using Management.Domain.Constants;

namespace Management.Presentation.ViewModels.Finance
{
    public partial class AddStaffViewModel : FacilityAwareViewModelBase
    {
        [ObservableProperty]
        private StaffMemberViewModel _newStaff = new();

        public CommunityToolkit.Mvvm.Input.IAsyncRelayCommand CancelCommand { get; }
        public CommunityToolkit.Mvvm.Input.IAsyncRelayCommand SaveCommand { get; }

        private readonly IStaffService _staffService;
        private readonly ITenantService _tenantService;
        private readonly ModalNavigationStore _modalNavigationStore;


        [ObservableProperty] private bool _isGymEnabled = true;
        [ObservableProperty] private bool _isSalonEnabled = false;
        [ObservableProperty] private bool _isRestaurantEnabled = false;
        [ObservableProperty] private string _actionButtonText = "Save";
        [ObservableProperty] private bool _isEditing;

        public Action<StaffMemberViewModel>? OnStaffAdded;

        public ObservableCollection<Management.Domain.Enums.StaffRole> Roles { get; } = new() { Management.Domain.Enums.StaffRole.Staff, Management.Domain.Enums.StaffRole.Owner };

        public AddStaffViewModel(
            ITerminologyService terminologyService,
            IFacilityContextService facilityContext,
            Microsoft.Extensions.Logging.ILogger<AddStaffViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            IStaffService staffService,
            ITenantService tenantService,
            ModalNavigationStore modalNavigationStore,
            ILocalizationService localizationService)
            : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService)
        {
            _staffService = staffService;
            _tenantService = tenantService;
            _modalNavigationStore = modalNavigationStore;

            CancelCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(() => CloseAsync(null));
            SaveCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(SaveAsync);

            NewStaff.Role = Management.Domain.Enums.StaffRole.Staff;
            NewStaff.Permissions = new System.Collections.ObjectModel.ObservableCollection<StaffPermission>
            {
            };

            ActionButtonText = GetTerm("Terminology.Staff.Add.Action.Create") ?? "Create Staff";
            Title = GetTerm("Strings.FinanceAndStaff.AddNewStaffMember") ?? "Add New Staff Member";
        }



        protected override void OnLanguageChanged()
        {
            Title = GetTerm("Strings.FinanceAndStaff.AddNewStaffMember") ?? "Add New Staff Member";
            ActionButtonText = GetTerm("Terminology.Staff.Add.Action.Create") ?? "Create Staff";
            
            // Refresh permissions labels if needed
            foreach (var p in NewStaff.Permissions)
            {
                if (p.Key == SystemPermissions.ViewDashboard) p.Name = GetTerm("Permission.ViewDashboard") ?? "View Dashboard";
                else if (p.Key == SystemPermissions.CreateMember) p.Name = GetTerm("Permission.CreateMember") ?? "Manage Members";
                else if (p.Key == SystemPermissions.ModifyProduct) p.Name = GetTerm("Permission.ModifyProduct") ?? "Manage Products";
                else if (p.Key == SystemPermissions.CreateStaff) p.Name = GetTerm("Permission.CreateStaff") ?? "Manage Staff";
            }
        }

        private async Task CloseAsync(object? result = null)
        {
             await _modalNavigationStore.CloseAsync(result is null ? null : Management.Presentation.Stores.ModalResult.Success(result));
        }
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(NewStaff.FullName))
            {
                _toastService?.ShowError(_localizationService.GetString("Strings.Finance.Validation.NameRequired"));
                return;
            }

            if (string.IsNullOrWhiteSpace(NewStaff.Email) || !System.Text.RegularExpressions.Regex.IsMatch(NewStaff.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                _toastService?.ShowError(_localizationService.GetString("Strings.Finance.Validation.EmailInvalid") ?? "Please enter a valid email address.");
                return;
            }

            if (string.IsNullOrWhiteSpace(NewStaff.Phone))
            {
                _toastService?.ShowError(_localizationService.GetString("Strings.Finance.Validation.PhoneRequired") ?? "Phone number is required.");
                return;
            }

            string password = NewStaff.Password;
            if (!IsEditing)
            {
                if (string.IsNullOrWhiteSpace(password))
                {
                    _toastService?.ShowError(_localizationService.GetString("Strings.Finance.Validation.PasswordRequired"));
                    return;
                }
            }

            await ExecuteLoadingAsync(async () =>
            {
                var role = NewStaff.Role;

                var targetFacilityId = _facilityContext.CurrentFacilityId;

                var dto = new Management.Application.DTOs.StaffDto
                {
                    Id = IsEditing && Guid.TryParse(NewStaff.Id, out var gid) ? gid : Guid.Empty,
                    TenantId = _tenantService.GetTenantId() ?? Guid.Empty,
                    FacilityId = targetFacilityId,
                    FullName = NewStaff.FullName,
                    Email = NewStaff.Email,
                    PhoneNumber = NewStaff.Phone,
                    Role = role,
                    Salary = NewStaff.Salary,
                    PaymentDay = NewStaff.PaymentDay,
                    HireDate = IsEditing ? NewStaff.HireDate : DateTime.UtcNow,
                    Password = password,
                    AllowedModules = new System.Collections.Generic.List<string> { _facilityContext.CurrentFacility.ToString() }
                };

                // Map UI permissions to DTO
                foreach (var p in NewStaff.Permissions)
                {
                    if (!string.IsNullOrEmpty(p.Key))
                    {
                        dto.Permissions.Add(new Management.Application.DTOs.PermissionDto(p.Key, p.IsGranted));
                    }
                }

                var result = IsEditing 
                    ? await _staffService.UpdateStaffAsync(dto)
                    : await _staffService.CreateStaffAsync(dto).ContinueWith(t => t.Result.IsSuccess ? Result.Success(t.Result.Value) : Result.Failure<Guid>(t.Result.Error));

                if (result.IsSuccess)
                {
                    if (!IsEditing && result is Result<Guid> createResult)
                    {
                        NewStaff.Id = createResult.Value.ToString();
                    }
                    
                    NewStaff.Initials = string.Join("", (NewStaff.FullName ?? "Staff").Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(n => n.FirstOrDefault())).ToUpper();
                    
                    OnStaffAdded?.Invoke(NewStaff);
                    _toastService?.ShowSuccess(string.Format(_localizationService.GetString(IsEditing ? "Terminology.Staff.Toast.UpdateSuccess" : "Terminology.Staff.Toast.AddSuccess") ?? (IsEditing ? "Updated {0}" : "Added {0}"), NewStaff.FullName));
                    await CloseAsync(NewStaff);
                }
                else
                {
                    _toastService?.ShowError(string.Format(_localizationService.GetString("Strings.Finance.Toast.UpdateStaffError") ?? "Failed to save staff: {0}", result.Error.Message));
                }
            }, _localizationService.GetString(IsEditing ? "Strings.Finance.Loading.UpdatingStaff" : "Strings.Finance.Loading.AddingStaff") ?? (IsEditing ? "Updating staff..." : "Adding staff..."));
        }
        public override Task OnModalOpenedAsync(object parameter, System.Threading.CancellationToken cancellationToken = default)
        {
            // Always reset to creation mode first, so a previous edit session never bleeds into a new creation dialog
            IsEditing = false;
            NewStaff = new StaffMemberViewModel
            {
                Role = Management.Domain.Enums.StaffRole.Staff,
                Permissions = new System.Collections.ObjectModel.ObservableCollection<StaffPermission>
                {
                    new StaffPermission { Name = GetTerm("Permission.ViewDashboard") ?? "View Dashboard", Key = SystemPermissions.ViewDashboard, IsGranted = false },
                    new StaffPermission { Name = GetTerm("Permission.CreateMember") ?? "Manage Members", Key = SystemPermissions.CreateMember, IsGranted = false },
                    new StaffPermission { Name = GetTerm("Permission.ModifyProduct") ?? "Manage Products", Key = SystemPermissions.ModifyProduct, IsGranted = false },
                    new StaffPermission { Name = GetTerm("Permission.CreateStaff") ?? "Manage Staff", Key = SystemPermissions.CreateStaff, IsGranted = false }
                }
            };
            ActionButtonText = GetTerm("Terminology.Staff.Add.Action.Create") ?? "Create Staff Member";
            Title = GetTerm("Strings.FinanceAndStaff.AddNewStaffMember") ?? "Add New Staff Member";

            if (parameter is Action<StaffMemberViewModel> callback)
            {
                OnStaffAdded = callback;
            }
            else if (parameter is StaffMemberViewModel existingStaff)
            {
                NewStaff = existingStaff;
                IsEditing = true;
                ActionButtonText = GetTerm("Terminology.Global.Save") ?? "Save Changes";
                Title = GetTerm("Terminology.Staff.Edit.Header") ?? "Edit Staff Member";
            }

            return base.OnModalOpenedAsync(parameter, cancellationToken);
        }
    }
}
