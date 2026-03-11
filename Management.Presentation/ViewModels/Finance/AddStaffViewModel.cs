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

namespace Management.Presentation.ViewModels.Finance
{
    public class FacilityOption
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public partial class AddStaffViewModel : FacilityAwareViewModelBase
    {
        [ObservableProperty]
        private StaffMemberViewModel _newStaff = new();

        public CommunityToolkit.Mvvm.Input.IAsyncRelayCommand CancelCommand { get; }
        public CommunityToolkit.Mvvm.Input.IAsyncRelayCommand SaveCommand { get; }

        private readonly IStaffService _staffService;
        private readonly ITenantService _tenantService;
        private readonly ModalNavigationStore _modalNavigationStore;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty] private bool _isGymEnabled = true;
        [ObservableProperty] private bool _isSalonEnabled = false;
        [ObservableProperty] private bool _isRestaurantEnabled = false;

        [ObservableProperty] private ObservableCollection<FacilityOption> _availableFacilities = new();
        [ObservableProperty] private FacilityOption? _selectedFacility;
        [ObservableProperty] private string _actionButtonText = "Save";

        public Action<StaffMemberViewModel>? OnStaffAdded;

        public ObservableCollection<string> Roles { get; } = new() { "Staff", "Owner" };

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
            SaveCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand<object>(SaveAsync);

            NewStaff.Role = "Staff";
            NewStaff.Permissions = new System.Collections.ObjectModel.ObservableCollection<StaffPermission>
            {
                new StaffPermission { Name = GetTerm("Strings.Finance.CanCreateMembers") ?? "Can Create Members", IsGranted = false }
            };

            InitializeFacilityOptions();
            ActionButtonText = GetTerm("Terminology.Staff.Add.Action.Create") ?? "Create Staff";
            Title = GetTerm("Strings.FinanceAndStaff.AddNewStaffMember") ?? "Add New Staff Member";
        }

        private void InitializeFacilityOptions()
        {
            AvailableFacilities.Clear();
            AvailableFacilities.Add(new FacilityOption { Id = "Gym", DisplayName = GetTerm("Strings.Global.Gym") ?? "Gym" });
            AvailableFacilities.Add(new FacilityOption { Id = "Salon", DisplayName = GetTerm("Strings.Global.Salon") ?? "Salon" });
            AvailableFacilities.Add(new FacilityOption { Id = "Restaurant", DisplayName = GetTerm("Strings.Global.Restaurant") ?? "Restaurant" });
            SelectedFacility = AvailableFacilities.FirstOrDefault();
        }

        protected override void OnLanguageChanged()
        {
            Title = GetTerm("Strings.FinanceAndStaff.AddNewStaffMember") ?? "Add New Staff Member";
            ActionButtonText = GetTerm("Terminology.Staff.Add.Action.Create") ?? "Create Staff";
            InitializeFacilityOptions();
            
            // Refresh permissions labels if needed
            if (NewStaff.Permissions.Count > 0)
            {
                NewStaff.Permissions[0].Name = GetTerm("Strings.Finance.CanCreateMembers") ?? "Can Create Members";
            }
        }

        private async Task CloseAsync(object? result = null)
        {
             await _modalNavigationStore.CloseAsync(result is null ? null : Management.Presentation.Stores.ModalResult.Success(result));
        }
        private async Task SaveAsync(object? parameter)
        {
            if (string.IsNullOrWhiteSpace(NewStaff.FullName))
            {
                _toastService?.ShowError(_localizationService.GetString("Strings.Finance.Validation.NameRequired"));
                return;
            }

            string password = string.Empty;
            if (parameter is System.Windows.Controls.PasswordBox pb)
            {
                password = pb.Password;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                _toastService?.ShowError(_localizationService.GetString("Strings.Finance.Validation.PasswordRequired"));
                return;
            }

            await ExecuteLoadingAsync(async () =>
            {
                var role = Enum.TryParse<Management.Domain.Enums.StaffRole>(NewStaff.Role, out var r) ? r : Management.Domain.Enums.StaffRole.Staff;

                var dto = new Management.Application.DTOs.StaffDto
                {
                    TenantId = _tenantService.GetTenantId() ?? Guid.Empty,
                    FacilityId = _facilityContext.CurrentFacilityId,
                    FullName = NewStaff.FullName,
                    Email = NewStaff.Email,
                    PhoneNumber = NewStaff.Phone,
                    Role = role,
                    Salary = NewStaff.Salary,
                    PaymentDay = NewStaff.PaymentDay,
                    HireDate = DateTime.UtcNow,
                    Password = password,
                    Permissions = new System.Collections.Generic.List<Management.Application.DTOs.PermissionDto>(),
                    AllowedModules = new System.Collections.Generic.List<string>()
                };

                // Populate Allowed Modules
                if (SelectedFacility != null)
                {
                    dto.AllowedModules.Add(SelectedFacility.Id);
                }
                else
                {
                    if (IsGymEnabled) dto.AllowedModules.Add("Gym");
                    if (IsSalonEnabled) dto.AllowedModules.Add("Salon");
                    if (IsRestaurantEnabled) dto.AllowedModules.Add("Restaurant");
                }

                // Handle basic permission
                var canCreateMembers = NewStaff.Permissions.FirstOrDefault(p => p.Name == "Can Create Members")?.IsGranted ?? false;
                dto.Permissions.Add(new Management.Application.DTOs.PermissionDto("CanCreateMembers", canCreateMembers));

                var result = await _staffService.CreateStaffAsync(dto);

                if (result.IsSuccess)
                {
                    NewStaff.Id = result.Value.ToString();
                    NewStaff.Initials = string.Join("", NewStaff.FullName.Split(' ').Select(n => n.FirstOrDefault())).ToUpper();
                    
                    OnStaffAdded?.Invoke(NewStaff);
                    _toastService?.ShowSuccess(string.Format(_localizationService.GetString("Terminology.Staff.Toast.AddSuccess") ?? "Added {0}", NewStaff.FullName));
                    await CloseAsync(NewStaff);
                }
                else
                {
                    _toastService?.ShowError(string.Format(_localizationService.GetString("Strings.Finance.Toast.UpdateStaffError") ?? "Failed to update staff: {0}", result.Error.Message));
                }
            }, _localizationService.GetString("Strings.Finance.Loading.AddingStaff"));
        }
        public override Task OnModalOpenedAsync(object parameter, System.Threading.CancellationToken cancellationToken = default)
        {
            if (parameter is Action<StaffMemberViewModel> callback)
            {
                OnStaffAdded = callback;
            }

            // Initialize default permission
            NewStaff.Permissions.Clear();
            NewStaff.Permissions.Add(new StaffPermission { Name = GetTerm("Strings.Finance.CanCreateMembers") ?? "Can Create Members", IsGranted = false });

            return base.OnModalOpenedAsync(parameter, cancellationToken);
        }
    }
}
