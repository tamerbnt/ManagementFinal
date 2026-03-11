using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.Interfaces.App;
using Management.Domain.Models.Salon;
using Management.Presentation.Services.Salon;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Management.Application.Services;
using Management.Application.ViewModels.Base;

namespace Management.Presentation.ViewModels.Settings
{
    public partial class SalonServiceEditorViewModel : ViewModelBase
    {
        private readonly ISalonService _salonService;
        private readonly IFacilityContextService _facilityContext;
        private readonly ITerminologyService _terminologyService;
        private readonly IServiceScopeFactory _scopeFactory;

        public SalonServiceEditorViewModel(
            ISalonService salonService,
            IFacilityContextService facilityContext,
            ITerminologyService terminologyService,
            IServiceScopeFactory scopeFactory,
            ILogger<SalonServiceEditorViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService) : base(logger, diagnosticService, toastService)
        {
            _salonService = salonService;
            _facilityContext = facilityContext;
            _terminologyService = terminologyService;
            _scopeFactory = scopeFactory;
        }

        [ObservableProperty]
        private Guid _id;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private decimal _basePrice;

        [ObservableProperty]
        private int _durationMinutes = 30;

        [ObservableProperty]
        private string _category = "General";

        [ObservableProperty]
        private bool _isEditMode;

        public event EventHandler? Saved;
        public event EventHandler? Canceled;

        [RelayCommand]
        public async Task Save()
        {
            await ExecuteLoadingAsync(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<Management.Infrastructure.Data.AppDbContext>();

                SalonService? service;
                if (Id == Guid.Empty)
                {
                    service = new SalonService
                    {
                        Id = Guid.NewGuid(),
                        FacilityId = _facilityContext.CurrentFacilityId,
                        Name = Name,
                        BasePrice = BasePrice,
                        DurationMinutes = DurationMinutes,
                        Category = Category
                    };
                    context.SalonServices.Add(service);
                }
                else
                {
                    service = await context.SalonServices.FindAsync(Id);
                    if (service != null)
                    {
                        service.Name = Name;
                        service.BasePrice = BasePrice;
                        service.DurationMinutes = DurationMinutes;
                        service.Category = Category;
                    }
                }

                await context.SaveChangesAsync();
                await _salonService.LoadServicesAsync();

                _toastService?.ShowSuccess(_terminologyService.GetTerm("Terminology.Settings.Editor.SaveSuccess"));
                Saved?.Invoke(this, EventArgs.Empty);
            }, _terminologyService.GetTerm("Terminology.Settings.Editor.Saving"));
        }

        [RelayCommand]
        public void Cancel()
        {
            Canceled?.Invoke(this, EventArgs.Empty);
        }
    }
}
