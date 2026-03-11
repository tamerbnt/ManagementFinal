using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Domain.Enums;
using Management.Presentation.Extensions;
using Management.Presentation.Stores;
using Microsoft.Extensions.DependencyInjection;
using Management.Domain.Services;


namespace Management.Presentation.ViewModels.Shell
{
    public partial class ChangeFacilityViewModel : ViewModelBase
    {
        private readonly ModalNavigationStore _modalNavigationStore;
        private readonly IServiceProvider _serviceProvider;
        private readonly ITerminologyService _terminologyService;

        [ObservableProperty]
        private ObservableCollection<FacilityOption> _facilities = new();

        [ObservableProperty]
        private FacilityOption? _selectedFacility;

        public new string Title => _terminologyService.GetTerm("Strings.Shell.ChangeFacility");

        public ChangeFacilityViewModel(
            ModalNavigationStore modalNavigationStore,
            IServiceProvider serviceProvider,
            ITerminologyService terminologyService)
        {
            _modalNavigationStore = modalNavigationStore;
            _serviceProvider = serviceProvider;
            _terminologyService = terminologyService;
            base.Title = Title;

            InitializeFacilities();
        }

        private void InitializeFacilities()
        {
            Facilities = new ObservableCollection<FacilityOption>
            {
                new FacilityOption("Gym Facility", "Master fitness & training hub", "Icon.Runner", FacilityType.Gym),
                new FacilityOption("Salon & Spa", "Premium wellness & beauty", "Icon.Users", FacilityType.Salon), // Using Users as fallback for Scissors
                new FacilityOption("The Restaurant", "Nutrition & social lounge", "Icon.Storefront", FacilityType.Restaurant)
            };

            SelectedFacility = Facilities[0];
        }

        [RelayCommand]
        private async Task CancelAsync()
        {
            await _modalNavigationStore.CloseAsync(ModalResult.Cancel());
        }

        [RelayCommand(CanExecute = nameof(CanContinue))]
        private async Task ContinueAsync()
        {
            if (SelectedFacility == null) return;

            // Close current and open Auth modal
            await _modalNavigationStore.CloseAsync(ModalResult.Success(SelectedFacility));
            
            // Note: The caller (MainViewModel) will handle opening the Auth modal
            // or we could trigger it here if we had access to the logic.
            // Let's have MainViewModel handle the sequence.
        }

        private bool CanContinue() => SelectedFacility != null;
    }

    public class FacilityOption
    {
        public string Name { get; }
        public string Description { get; }
        public string IconKey { get; }
        public FacilityType Type { get; }

        public FacilityOption(string name, string description, string iconKey, FacilityType type)
        {
            Name = name;
            Description = description;
            IconKey = iconKey;
            Type = type;
        }
    }
}
