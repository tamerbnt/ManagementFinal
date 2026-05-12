using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.DTOs;
using Management.Application.Interfaces.App;
using Management.Application.Services;
using Management.Domain.Services;
using Management.Presentation.Extensions;
using Management.Presentation.ViewModels.Base;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Management.Presentation.ViewModels.Settings
{
    public partial class DiscountEditorViewModel : FacilityAwareViewModelBase
    {
        private readonly IDiscountService _discountService;
        
        public event EventHandler<Guid>? Saved;
        public event EventHandler? Canceled;

        [ObservableProperty]
        private Guid? _id;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private decimal _value;

        [ObservableProperty]
        private bool _isPercentage = true;

        [ObservableProperty]
        private bool _isFixedAmount;

        [ObservableProperty]
        private bool _isActive = true;

        [ObservableProperty]
        private bool _isEditMode;

        partial void OnIsFixedAmountChanged(bool value)
        {
            if (value) IsPercentage = false;
        }

        partial void OnIsPercentageChanged(bool value)
        {
            if (value) IsFixedAmount = false;
        }

        public DiscountEditorViewModel(
            ITerminologyService terminologyService,
            IFacilityContextService facilityContext,
            ILogger<DiscountEditorViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            IDiscountService discountService,
            Services.Localization.ILocalizationService localizationService)
            : base(terminologyService, facilityContext, logger, diagnosticService, toastService, localizationService)
        {
            _discountService = discountService;
            Title = GetLocalizedTitle(false);
        }

        private string GetLocalizedTitle(bool isEditMode)
        {
            // We'll reuse Terminology if possible or use defaults
            return isEditMode ? "Edit Discount" : "Add Discount";
        }

        public async Task InitializeAsync(Guid? id = null)
        {
            if (id.HasValue)
            {
                Id = id;
                IsEditMode = true;
                await LoadDiscountAsync(id.Value);
            }
            else
            {
                Id = null;
                IsEditMode = false;
                ResetValues();
            }
            Title = GetLocalizedTitle(IsEditMode);
        }

        public override async Task OnModalOpenedAsync(object parameter, System.Threading.CancellationToken cancellationToken = default)
        {
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
            Value = 0;
            IsPercentage = true;
            IsFixedAmount = false;
            IsActive = true;
        }

        private async Task LoadDiscountAsync(Guid id)
        {
            var result = await _discountService.GetDiscountAsync(_facilityContext.CurrentFacilityId, id);
            if (result.IsSuccess)
            {
                var d = result.Value;
                Name = d.Name;
                Description = d.Description ?? string.Empty;
                Value = d.Value;
                IsPercentage = d.IsPercentage;
                IsFixedAmount = !d.IsPercentage;
                IsActive = d.IsActive;
            }
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Name))
                {
                    _toastService.ShowWarning("Please enter a discount name.");
                    return;
                }

                if (Value <= 0)
                {
                    _toastService.ShowWarning("Please enter a valid discount value greater than 0.");
                    return;
                }

                var dto = new DiscountDto
                {
                    Id = Id ?? Guid.Empty,
                    Name = Name,
                    Description = Description,
                    Value = Value,
                    IsPercentage = IsPercentage,
                    IsActive = IsActive
                };

                var result = Id.HasValue 
                    ? await _discountService.UpdateDiscountAsync(_facilityContext.CurrentFacilityId, dto)
                    : await _discountService.CreateDiscountAsync(_facilityContext.CurrentFacilityId, dto);

                if (result.IsSuccess)
                {
                    _toastService.ShowSuccess("Discount saved successfully.");
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
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            Canceled?.Invoke(this, EventArgs.Empty);
        }
    }
}
