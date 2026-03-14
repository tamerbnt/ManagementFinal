using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Management.Domain.Models.Salon;
using Management.Presentation.Extensions;
using Management.Presentation.Services.Salon;

namespace Management.Presentation.Views.Salon
{
    public class ServicesViewModel : ViewModelBase
    {
        private readonly ISalonService _salonService;

        public ObservableCollection<ServiceItemViewModel> Services { get; } = new();

        public ServicesViewModel(ISalonService salonService)
        {
            _salonService = salonService;
            LoadServices();
        }

        private void LoadServices()
        {
            foreach (var service in _salonService.Services)
            {
                Services.Add(new ServiceItemViewModel(service));
            }
        }
    }

    public class ServiceItemViewModel : ViewModelBase
    {
        private readonly SalonService _model;
        
        private bool _isSaved;
        public bool IsSaved
        {
            get => _isSaved;
            set => SetProperty(ref _isSaved, value);
        }

        public ServiceItemViewModel(SalonService model)
        {
            _model = model;
        }

        public string Name => _model.Name;
        public string Category => _model.Category;

        public decimal BasePrice
        {
            get => _model.BasePrice;
            set
            {
                if (_model.BasePrice != value)
                {
                    _model.BasePrice = value;
                    OnPropertyChanged();
                    _ = TriggerSaveIndicatorAsync();
                }
            }
        }

        public int DurationMinutes
        {
            get => _model.DurationMinutes;
            set
            {
                if (_model.DurationMinutes != value)
                {
                    _model.DurationMinutes = value;
                    OnPropertyChanged();
                    _ = TriggerSaveIndicatorAsync();
                }
            }
        }

        private async Task TriggerSaveIndicatorAsync()
        {
            IsSaved = true;
            await Task.Delay(2000);
            IsSaved = false;
        }
    }
}
