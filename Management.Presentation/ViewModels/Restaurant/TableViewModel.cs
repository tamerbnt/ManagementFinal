using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Windows;
using System.Windows.Media;
using Management.Domain.Models.Restaurant;

namespace Management.Presentation.ViewModels.Restaurant
{
    public partial class TableViewModel : ObservableObject
    {
        [ObservableProperty]
        private Guid _id;

        [ObservableProperty]
        private double _x;

        [ObservableProperty]
        private double _y;

        [ObservableProperty]
        private int _zIndex;

        [ObservableProperty]
        private bool _isEditMode;

        [ObservableProperty]
        private int _tableNumber;

        [ObservableProperty]
        private int _seats;

        [ObservableProperty]
        private double _width;

        [ObservableProperty]
        private double _height;

        [ObservableProperty]
        private TableStatus _status;

        [ObservableProperty]
        private string _shape = "Square";

        // Computed Properties for Visuals (Apple Spatial Glass Effect)
        public SolidColorBrush StatusBrush => Status switch
        {
            TableStatus.Available => new SolidColorBrush(Color.FromArgb(200, 34, 197, 94)),      // Green Glass
            TableStatus.Occupied => new SolidColorBrush(Color.FromArgb(200, 239, 68, 68)),       // Red Glass
            TableStatus.OrderSent => new SolidColorBrush(Color.FromArgb(200, 251, 146, 60)),     // Orange Glass
            TableStatus.Ready => new SolidColorBrush(Color.FromArgb(200, 59, 130, 246)),         // Blue Glass
            TableStatus.BillRequested => new SolidColorBrush(Color.FromArgb(200, 168, 85, 247)), // Purple Glass
            TableStatus.Dirty => new SolidColorBrush(Color.FromArgb(200, 107, 114, 128)),        // Gray Glass
            _ => new SolidColorBrush(Color.FromArgb(200, 156, 163, 175))
        };

        public CornerRadius BorderRadius => Shape == "Round" 
            ? new CornerRadius(Width / 2) 
            : new CornerRadius(12);

        public TableViewModel() { }

        public TableViewModel(TableModel table, bool isEditMode)
        {
            Id = table.Id;
            X = table.X;
            Y = table.Y;
            Width = table.Width;
            Height = table.Height;
            Shape = table.Shape;
            TableNumber = table.TableNumber;
            Seats = table.MaxSeats;
            Status = table.Status;
            IsEditMode = isEditMode;
        }

        partial void OnStatusChanged(TableStatus value)
        {
            OnPropertyChanged(nameof(StatusBrush));
        }

        partial void OnShapeChanged(string value)
        {
            OnPropertyChanged(nameof(BorderRadius));
        }

        partial void OnWidthChanged(double value)
        {
            if (Shape == "Round")
                OnPropertyChanged(nameof(BorderRadius));
        }

        public void UpdateLocation(double x, double y)
        {
            X = x;
            Y = y;
        }
    }
}
