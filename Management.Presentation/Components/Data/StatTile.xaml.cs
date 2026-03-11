using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Management.Presentation.Components.Data
{
    public partial class StatTile : UserControl
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            "Value", typeof(string), typeof(StatTile), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
            "Label", typeof(string), typeof(StatTile), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty IconDataProperty = DependencyProperty.Register(
            "IconData", typeof(Geometry), typeof(StatTile), new PropertyMetadata(null));

        public static readonly DependencyProperty IconBrushProperty = DependencyProperty.Register(
            "IconBrush", typeof(Brush), typeof(StatTile), new PropertyMetadata(Brushes.Black));

        public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(
            "IsLoading", typeof(bool), typeof(StatTile), new PropertyMetadata(false));

        public Brush IconBrush
        {
            get => (Brush)GetValue(IconBrushProperty);
            set => SetValue(IconBrushProperty, value);
        }

        public string Value
        {
            get => (string)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public Geometry IconData
        {
            get => (Geometry)GetValue(IconDataProperty);
            set => SetValue(IconDataProperty, value);
        }

        public bool IsLoading
        {
            get => (bool)GetValue(IsLoadingProperty);
            set => SetValue(IsLoadingProperty, value);
        }

        public StatTile()
        {
            InitializeComponent();
        }
    }
}
