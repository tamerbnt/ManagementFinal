using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Management.Presentation.Services;
using Management.Domain.DTOs;
using Management.Domain.Services;
using Management.Presentation.Extensions;

namespace Management.Presentation.ViewModels
{
    public class PlanDetailViewModel : ViewModelBase, IModalViewModel, IInitializable<object?>
    {
        private readonly IMembershipPlanService _planService;
        private readonly IModalNavigationService _modalService;
        private readonly INotificationService _notificationService;

        public ModalSize PreferredSize => ModalSize.Small;

        public Task<bool> CanCloseAsync() => Task.FromResult(true);

        // --- STATE ---
        private Guid? _planId;
        public bool IsEditMode => _planId.HasValue;
        public string Title => IsEditMode ? "Edit Membership Plan" : "Add Membership Plan";

        private string _name = string.Empty;
        public string Name { get => _name; set => SetProperty(ref _name, value); }

        private decimal _price;
        public decimal Price { get => _price; set => SetProperty(ref _price, value); }

        private int _durationDays;
        public int DurationDays { get => _durationDays; set => SetProperty(ref _durationDays, value); }

        private string _description = string.Empty;
        public string Description { get => _description; set => SetProperty(ref _description, value); }

        private bool _isActive = true;
        public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        // --- COMMANDS ---
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public PlanDetailViewModel(
            IMembershipPlanService planService,
            IModalNavigationService modalService,
            INotificationService notificationService)
        {
            _planService = planService;
            _modalService = modalService;
            _notificationService = notificationService;

            SaveCommand = new AsyncRelayCommand(ExecuteSaveAsync);
            CancelCommand = new RelayCommand(async () => await _modalService.CloseCurrentModalAsync());
        }

        public async Task InitializeAsync(object? parameter, CancellationToken cancellationToken = default)
        {
            if (parameter is Guid id)
            {
                _planId = id;
                var result = await _planService.GetAllPlansAsync();
                if (result.IsSuccess)
                {
                    var plan = result.Value.Find(p => p.Id == id);
                    if (plan != null)
                    {
                        Name = plan.Name;
                        Price = plan.Price;
                        DurationDays = plan.DurationDays;
                        Description = plan.Description;
                        IsActive = plan.IsActive;
                    }
                }
            }
            else
            {
                _planId = null;
                DurationDays = 30; // Default
            }

            OnPropertyChanged(nameof(IsEditMode));
            OnPropertyChanged(nameof(Title));
        }

        private async Task ExecuteSaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Name) || Price < 0 || DurationDays <= 0)
            {
                _notificationService.ShowError("Please provide valid plan details.");
                return;
            }

            var dto = new MembershipPlanDto
            {
                Id = _planId ?? Guid.Empty,
                Name = Name,
                Price = Price,
                DurationDays = DurationDays,
                Description = Description,
                IsActive = IsActive
            };

            IsBusy = true;
            try
            {
                var result = IsEditMode 
                    ? await _planService.UpdatePlanAsync(dto) 
                    : await _planService.CreatePlanAsync(dto);

                if (result.IsSuccess)
                {
                    _notificationService.ShowSuccess($"Plan {(IsEditMode ? "updated" : "created")} successfully.");
                    await _modalService.CloseCurrentModalAsync();
                }
                else
                {
                    _notificationService.ShowError(result.Error.Message);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
