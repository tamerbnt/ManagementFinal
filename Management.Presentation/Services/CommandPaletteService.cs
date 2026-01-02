using System;
using Management.Domain.Enums;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Management.Presentation.Extensions;
using Management.Presentation.Views.Restaurant;
using Management.Presentation.Services.Restaurant;
using Management.Presentation.Services.Salon;
using Management.Presentation.Views.Salon;

namespace Management.Presentation.Services
{
    public interface ICommandPaletteService
    {
        bool IsVisible { get; set; }
        string SearchQuery { get; set; }
        ObservableCollection<CommandItemViewModel> Results { get; }
        void Open();
        void Close();
        ICommand TogglePaletteCommand { get; }
        void ExecuteSelected();
    }

    public class CommandPaletteService : ViewModelBase, ICommandPaletteService
    {
        private readonly INavigationService _navigationService;
        private readonly IModalNavigationService _modalService;
        private readonly Management.Domain.Services.IFacilityContextService _facilityService;
        private readonly ISalonService _salonService;
        private bool _isVisible;
        private string _searchQuery = string.Empty;

        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    FilterResults();
                }
            }
        }

        public ObservableCollection<CommandItemViewModel> Results { get; } = new();

        private List<CommandItemViewModel> _allCommands = null!;

        public CommandPaletteService(
            INavigationService navigationService, 
            IModalNavigationService modalService,
            Management.Domain.Services.IFacilityContextService facilityService,
            ISalonService salonService)
        {
            _navigationService = navigationService;
            _modalService = modalService;
            _facilityService = facilityService;
            _salonService = salonService;
            TogglePaletteCommand = new RelayCommand(() => IsVisible = !IsVisible);
            InitializeCommands();
        }

        private void InitializeCommands()
        {
            _allCommands = new List<CommandItemViewModel>
            {
                // General Navigation
                new("Dashboard", "View facility performance metrics", () => _navigationService.NavigateToAsync(0), "Navigation"),
                new("Settings", "App Configuration & System Defaults", () => _navigationService.NavigateToAsync(7), "Navigation"),
                
                // Gym Cross-Facility
                new("Gym Members", "Manage fitness database", () => { _facilityService.SetFacility(FacilityType.Gym); _navigationService.NavigateToAsync(2); }, "Gym"),
                new("Access Control", "Check people inside & terminal status", () => { _facilityService.SetFacility(FacilityType.Gym); _navigationService.NavigateToAsync(1); }, "Gym"),
                new("Live Feed", "Monitor turnstiles and scans", () => { _facilityService.SetFacility(FacilityType.Gym); _navigationService.NavigateToAsync(1); }, "Gym"),
                new("New Member", "Register a new person in the system", () => { _facilityService.SetFacility(FacilityType.Gym); _navigationService.NavigateToAsync(2); }, "Actions"),

                // Restaurant Cross-Facility
                new("Tables", "Manage floor plan and occupancy", () => { _facilityService.SetFacility(FacilityType.Restaurant); _navigationService.NavigateToAsync(1); }, "Restaurant"), 
                new("Kitchen Board", "View active orders in preparation", () => { _facilityService.SetFacility(FacilityType.Restaurant); _navigationService.NavigateToAsync(2); }, "Restaurant"), 
                new("New Order", "Open the quick-order interface", async () => { _facilityService.SetFacility(FacilityType.Restaurant); await _modalService.OpenModalAsync<OrderViewModel>(parameter: "QUICK"); }, "Actions"),
                
                // Salon Cross-Facility
                new("Book Appointment", "Schedule a new client appointment", async () => { _facilityService.SetFacility(FacilityType.Salon); await _modalService.OpenModalAsync<BookingViewModel>(); }, "Salon"),
                new("Salon Schedule", "Review daily appointments", () => { _facilityService.SetFacility(FacilityType.Salon); _navigationService.NavigateToAsync(1); }, "Salon"),
                new("Manage Services", "Service pricing and durations", () => { _facilityService.SetFacility(FacilityType.Salon); _navigationService.NavigateToAsync(2); }, "Salon"),

                // Searchable Users & Products (Mocked Indexing)
                new("Sarah (Gym Member)", "Plan: Premium | Card ID: 1042", () => { _facilityService.SetFacility(FacilityType.Gym); _navigationService.NavigateToAsync(2); }, "Members"),
                new("Sarah (Salon Client)", "Last visit: 2 days ago", () => { _facilityService.SetFacility(FacilityType.Salon); _navigationService.NavigateToAsync(1); }, "Members"),
                new("Protein Shake", "Stock: 42 | Price: $5.00", () => { _navigationService.NavigateToAsync(6); }, "Inventory"),
            };
        }

        public void Open()
        {
            SearchQuery = string.Empty;
            IsVisible = true;
            FilterResults();
        }

        public void Close()
        {
            IsVisible = false;
        }

        public ICommand TogglePaletteCommand { get; }

        public void ExecuteSelected()
        {
            var selected = Results.FirstOrDefault(r => r.IsSelected);
            selected?.Command.Execute(null);
            Close();
        }

        private void FilterResults()
        {
            Results.Clear();
            var filtered = string.IsNullOrWhiteSpace(SearchQuery) 
                ? _allCommands.Take(10) 
                : _allCommands.Where(c => (c.Label.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) || 
                                           c.Description.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                                           && (c.Category != "Actions" || IsActionRelevant(c)));

            foreach (var item in filtered)
            {
                Results.Add(item);
            }

            if (Results.Any()) Results[0].IsSelected = true;
        }

        private bool IsActionRelevant(CommandItemViewModel cmd)
        {
            if (cmd.Label == "New Order") return _facilityService.CurrentFacility == FacilityType.Restaurant;
            if (cmd.Label == "New Member") return _facilityService.CurrentFacility == FacilityType.Gym;
            return true;
        }

        private async void ExecuteCheckoutTable() { await _modalService.OpenModalAsync<OrderDetailViewModel>(); }

        private async void ExecuteBookAppointment() { await _modalService.OpenModalAsync<BookingViewModel>(); }
    }

    public class CommandItemViewModel : ViewModelBase
    {
        public string Label { get; }
        public string Description { get; }
        public ICommand Command { get; }
        public string Category { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public CommandItemViewModel(string label, string description, Action action, string category)
        {
            Label = label;
            Description = description;
            Command = new RelayCommand(action);
            Category = category;
        }
    }
}
