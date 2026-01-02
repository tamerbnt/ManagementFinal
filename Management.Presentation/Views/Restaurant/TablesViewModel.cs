using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Management.Domain.Models.Restaurant;
using Management.Presentation.Extensions;
using Management.Presentation.ViewModels;

namespace Management.Presentation.Views.Restaurant
{
    public class TablesViewModel : ViewModelBase
    {
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private bool _isFloorPlanMode;
        public bool IsFloorPlanMode
        {
            get => _isFloorPlanMode;
            set => SetProperty(ref _isFloorPlanMode, value);
        }

        public ObservableCollection<TableItemViewModel> Tables { get; } = new();

        public ICommand ToggleViewModeCommand { get; }

        public TablesViewModel()
        {
            ToggleViewModeCommand = new RelayCommand(() => IsFloorPlanMode = !IsFloorPlanMode);
            LoadMockData();
        }

        private void LoadMockData()
        {
            Tables.Add(new TableItemViewModel { Id = 1, Number = "T1", MaxSeats = 4, CurrentOccupancy = 0, Status = TableStatus.Available, X = 100, Y = 100 });
            Tables.Add(new TableItemViewModel { Id = 2, Number = "T2", MaxSeats = 2, CurrentOccupancy = 2, Status = TableStatus.Occupied, X = 320, Y = 100 });
            Tables.Add(new TableItemViewModel { Id = 3, Number = "B1", MaxSeats = 6, CurrentOccupancy = 4, Status = TableStatus.Occupied, X = 100, Y = 360 });
            Tables.Add(new TableItemViewModel { Id = 4, Number = "T3", MaxSeats = 4, CurrentOccupancy = 0, Status = TableStatus.Cleaning, X = 540, Y = 100 });
        }
    }

    public class TableItemViewModel : ViewModelBase
    {
        public int Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        
        private int _maxSeats;
        public int MaxSeats
        {
            get => _maxSeats;
            set { if (SetProperty(ref _maxSeats, value)) UpdateDots(); }
        }

        private int _currentOccupancy;
        public int CurrentOccupancy
        {
            get => _currentOccupancy;
            set { if (SetProperty(ref _currentOccupancy, value)) UpdateDots(); }
        }

        private TableStatus _status;
        public TableStatus Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public ObservableCollection<SeatDotViewModel> OccupancyDots { get; } = new();

        public TableItemViewModel()
        {
            UpdateDots();
        }

        private void UpdateDots()
        {
            OccupancyDots.Clear();
            for (int i = 0; i < MaxSeats; i++)
            {
                OccupancyDots.Add(new SeatDotViewModel { IsOccupied = i < CurrentOccupancy });
            }
        }
    }

    public class SeatDotViewModel : ViewModelBase
    {
        private bool _isOccupied;
        public bool IsOccupied
        {
            get => _isOccupied;
            set => SetProperty(ref _isOccupied, value);
        }
    }
}
