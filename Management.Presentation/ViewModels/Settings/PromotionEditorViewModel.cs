using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.DTOs;
using Management.Application.Interfaces.App;
using Management.Application.Services;
using Management.Domain.Enums;
using Management.Domain.Services;
using Management.Presentation.Extensions;
using Management.Presentation.Stores;
using Management.Presentation.ViewModels.Base;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Management.Presentation.ViewModels.Settings
{
    public partial class PromotionEditorViewModel : FacilityAwareViewModelBase
    {
        public record TargetItem(Guid Id, string Name);

        private readonly IPromotionService _promotionService;
        private readonly IMembershipPlanService _planService;
        private readonly IProductService _productService;
        private readonly Management.Presentation.Services.Salon.ISalonService _salonService;
        
        public event EventHandler<Guid>? Saved;
        public event EventHandler? Canceled;

        [ObservableProperty]
        private Guid? _id;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private PromotionTargetType _targetType = PromotionTargetType.Product;

        [ObservableProperty]
        private Guid _targetId;

        [ObservableProperty]
        private decimal _discountValue;

        [ObservableProperty]
        private bool _isPercentage = true;

        [ObservableProperty]
        private Gender? _requiredGender;

        [ObservableProperty]
        private Guid? _requiredMembershipPlanId;

        [ObservableProperty]
        private DateTime _startDate = DateTime.Today;

        [ObservableProperty]
        private DateTime _endDate = DateTime.Today.AddMonths(1);

        [ObservableProperty]
        private TargetItem? _selectedTargetItem;

        [ObservableProperty]
        private ObservableCollection<TargetItem> _availableTargets = new();

        [ObservableProperty]
        private bool _isActive = true;

        [ObservableProperty]
        private bool _isFixedAmount;

        partial void OnIsFixedAmountChanged(bool value)
        {
            if (value) IsPercentage = false;
        }

        partial void OnIsPercentageChanged(bool value)
        {
            if (value) IsFixedAmount = false;
        }

        [ObservableProperty]
        private bool _isEditMode;

        [ObservableProperty]
        private ObservableCollection<MembershipPlanDto> _membershipPlans = new();

        [ObservableProperty]
        private ObservableCollection<ProductDto> _products = new();

        [ObservableProperty]
        private ObservableCollection<Management.Domain.Models.Salon.SalonService> _salonServices = new();

        public ObservableCollection<PromotionTargetType> TargetTypes { get; } = new(Enum.GetValues<PromotionTargetType>().Cast<PromotionTargetType>());
        public ObservableCollection<Gender?> GenderOptions { get; } = new() { null, Gender.Male, Gender.Female };

        public PromotionEditorViewModel(
            ITerminologyService terminologyService,
            IFacilityContextService facilityContext,
            ILogger<PromotionEditorViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            IPromotionService promotionService,
            IMembershipPlanService planService,
            IProductService productService,
            Management.Presentation.Services.Salon.ISalonService salonService,
            Services.Localization.ILocalizationService localizationService)
            : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService)
        {
            _promotionService = promotionService;
            _planService = planService;
            _productService = productService;
            _salonService = salonService;
            Title = "Promotion Editor";
        }

        public async Task InitializeAsync(Guid? id = null)
        {
            await LoadTargetsAsync();

            if (id.HasValue)
            {
                Id = id;
                IsEditMode = true;
                await LoadPromotionAsync(id.Value);
                SyncSelectedTarget();
            }
            else
            {
                Id = null;
                IsEditMode = false;
                ResetValues();
            }
        }

        public override async Task OnModalOpenedAsync(object parameter, System.Threading.CancellationToken cancellationToken = default)
        {
            // Backward compatibility for center modal if still needed, but primarily using InitializeAsync for drawer
            if (parameter is Guid id)
            {
                await InitializeAsync(id);
            }
            else
            {
                await InitializeAsync(null);
            }
        }

        private void ResetValues()
        {
            Name = string.Empty;
            Description = string.Empty;
            TargetType = PromotionTargetType.Product;
            TargetId = Guid.Empty;
            SelectedTargetItem = null;
            DiscountValue = 0;
            IsPercentage = true;
            RequiredGender = null;
            RequiredMembershipPlanId = null;
            StartDate = DateTime.Today;
            EndDate = DateTime.Today.AddMonths(1);
            IsActive = true;
            IsPercentage = true;
            IsFixedAmount = false;
        }

        private void SyncSelectedTarget()
        {
            SelectedTargetItem = AvailableTargets.FirstOrDefault(t => t.Id == TargetId);
        }

        partial void OnTargetTypeChanged(PromotionTargetType value)
        {
            UpdateAvailableTargets();
        }

        partial void OnSelectedTargetItemChanged(TargetItem? value)
        {
            if (value != null)
            {
                TargetId = value.Id;
            }
        }

        private void UpdateAvailableTargets()
        {
            AvailableTargets.Clear();
            switch (TargetType)
            {
                case PromotionTargetType.Product:
                    foreach (var p in Products) AvailableTargets.Add(new TargetItem(p.Id, p.Name));
                    break;
                case PromotionTargetType.MembershipPlan:
                    foreach (var p in MembershipPlans) AvailableTargets.Add(new TargetItem(p.Id, p.Name));
                    break;
                case PromotionTargetType.SalonService:
                    foreach (var s in SalonServices) AvailableTargets.Add(new TargetItem(s.Id, s.Name));
                    break;
            }
            SyncSelectedTarget();
        }

        private async Task LoadTargetsAsync()
        {
            var plans = await _planService.GetAllPlansAsync(_facilityContext.CurrentFacilityId);
            if (plans.IsSuccess)
            {
                MembershipPlans.Clear();
                // Add "None" option
                MembershipPlans.Add(new MembershipPlanDto { Id = Guid.Empty, Name = "--- All Members (No Requirement) ---" });
                foreach (var p in plans.Value) MembershipPlans.Add(p);
            }

            var prods = await _productService.GetActiveProductsAsync(_facilityContext.CurrentFacilityId);
            if (prods.IsSuccess)
            {
                Products.Clear();
                foreach (var p in prods.Value) Products.Add(p);
            }

            if (_facilityContext.CurrentFacility == FacilityType.Salon)
            {
                await _salonService.LoadServicesAsync();
                SalonServices.Clear();
                foreach (var s in _salonService.Services) SalonServices.Add(s);
            }

            UpdateAvailableTargets();
        }

        private async Task LoadPromotionAsync(Guid id)
        {
            var result = await _promotionService.GetPromotionAsync(_facilityContext.CurrentFacilityId, id);
            if (result.IsSuccess)
            {
                var p = result.Value;
                Name = p.Name;
                Description = p.Description ?? string.Empty;
                TargetType = p.TargetType;
                TargetId = p.TargetId;
                DiscountValue = p.DiscountValue;
                IsPercentage = p.IsPercentage;
                IsFixedAmount = !p.IsPercentage;
                RequiredGender = p.RequiredGender;
                RequiredMembershipPlanId = p.RequiredMembershipPlanId;
                StartDate = p.StartDate;
                EndDate = p.EndDate;
                IsActive = p.IsActive;
            }
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Name))
                {
                    _toastService.ShowWarning("Please enter a promotion name.");
                    return;
                }

                if (TargetId == Guid.Empty)
                {
                    _toastService.ShowWarning("Please select a target (Product, Plan, or Service).");
                    return;
                }

                if (DiscountValue <= 0)
                {
                    _toastService.ShowWarning("Please enter a valid discount value greater than 0.");
                    return;
                }

                var dto = new PromotionDto
                {
                    Id = Id ?? Guid.NewGuid(),
                    Name = Name,
                    Description = Description,
                    TargetType = TargetType,
                    TargetId = TargetId,
                    DiscountValue = DiscountValue,
                    IsPercentage = IsPercentage,
                    RequiredGender = RequiredGender,
                    RequiredMembershipPlanId = RequiredMembershipPlanId == Guid.Empty ? null : RequiredMembershipPlanId,
                    StartDate = StartDate,
                    EndDate = EndDate,
                    IsActive = IsActive
                };

                var result = Id.HasValue 
                    ? await _promotionService.UpdatePromotionAsync(_facilityContext.CurrentFacilityId, dto)
                    : await _promotionService.CreatePromotionAsync(_facilityContext.CurrentFacilityId, dto);

                if (result.IsSuccess)
                {
                    _toastService.ShowSuccess("Promotion saved successfully.");
                    Saved?.Invoke(this, dto.Id);
                }
                else
                {
                    _toastService.ShowError($"Save failed: {result.Error}");
                }
            }
            catch (Exception ex)
            {
                _toastService.ShowError("An unexpected error occurred while saving.");
                Serilog.Log.Error(ex, "Promotion Save Error");
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            Canceled?.Invoke(this, EventArgs.Empty);
        }
    }
}
