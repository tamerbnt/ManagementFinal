using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Management.Presentation.ViewModels.Restaurant
{
    public partial class AddTableViewModel : ObservableObject
    {
        private readonly Stores.ModalNavigationStore _modalNavigationStore;

        public AddTableViewModel(Stores.ModalNavigationStore modalNavigationStore)
        {
            _modalNavigationStore = modalNavigationStore;
        }
        [ObservableProperty]
        private string _tableNumber = "T-01";

        [ObservableProperty]
        private int _capacity = 4;

        public ObservableCollection<string> Shapes { get; } = new() { "Square", "Round", "Rectangular" };

        [ObservableProperty]
        private string _selectedShape = "Square";

        public ObservableCollection<string> Sections { get; } = new() { "Main Hall", "Patio", "Bar Seating" };

        [ObservableProperty]
        private string _selectedSection = "Main Hall";

        [RelayCommand]
        private void IncrementCapacity() => Capacity++;

        [RelayCommand]
        private void DecrementCapacity() { if (Capacity > 1) Capacity--; }

        [RelayCommand]
        private void Cancel()
        {
            _ = _modalNavigationStore.CloseAsync();
        }

        [RelayCommand]
        private void Confirm()
        {
            var newTile = new TableTileViewModel
            {
                TableNumber = TableNumber,
                Capacity = Capacity,
                Section = SelectedSection,
                Status = Management.Domain.Models.Restaurant.TableStatus.Available
            };

            _ = _modalNavigationStore.CloseAsync(Stores.ModalResult.Success(newTile));
        }
    }
}
