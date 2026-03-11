using System;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Management.Presentation.ViewModels.Scheduler
{
    public partial class AppointmentViewModel : ObservableObject
    {
        private const double ColumnWidth = 200; // Fixed width for V1
        private const double HourHeight = 120;   // 120px per hour (2px per minute)
        private static readonly TimeSpan DayStart = TimeSpan.FromHours(8); // Start at 8 AM

        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private string _description;

        [ObservableProperty]
        private DateTime _start;

        [ObservableProperty]
        private DateTime _end;

        [ObservableProperty]
        private Guid _staffId;

        [ObservableProperty]
        private Brush _backgroundBrush;

        private readonly int _staffIndex;

        public AppointmentViewModel(
            Guid id,
            string title,
            DateTime start,
            DateTime end,
            Guid staffId,
            int staffIndex,
            string hexColor)
        {
            Id = id;
            _title = title;
            _start = start;
            _end = end;
            _staffId = staffId;
            _staffIndex = staffIndex;

            // Simple hex conversion with Freeze
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hexColor);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                _backgroundBrush = brush;
            }
            catch
            {
                var brush = new SolidColorBrush(Colors.LightGray);
                brush.Freeze();
                _backgroundBrush = brush;
            }
        }

        public Guid Id { get; }

        public double CanvasLeft => _staffIndex * ColumnWidth;

        public double CanvasTop => (Start.TimeOfDay - DayStart).TotalHours * HourHeight;

        public double Height => (End - Start).TotalHours * HourHeight;

        public double Width => ColumnWidth - 10; // Slight padding
    }
}
