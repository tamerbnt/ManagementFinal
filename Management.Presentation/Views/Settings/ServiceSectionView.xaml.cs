using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Management.Presentation.Views.Settings
{
    public partial class ServiceSectionView : UserControl
    {
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(ServiceSectionView), new PropertyMetadata(null));

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(ServiceSectionView), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty AddServiceCommandProperty =
            DependencyProperty.Register(nameof(AddServiceCommand), typeof(ICommand), typeof(ServiceSectionView), new PropertyMetadata(null));

        public static readonly DependencyProperty EditServiceCommandProperty =
            DependencyProperty.Register(nameof(EditServiceCommand), typeof(ICommand), typeof(ServiceSectionView), new PropertyMetadata(null));

        public static readonly DependencyProperty DeleteServiceCommandProperty =
            DependencyProperty.Register(nameof(DeleteServiceCommand), typeof(ICommand), typeof(ServiceSectionView), new PropertyMetadata(null));

        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public ICommand AddServiceCommand
        {
            get => (ICommand)GetValue(AddServiceCommandProperty);
            set => SetValue(AddServiceCommandProperty, value);
        }

        public ICommand EditServiceCommand
        {
            get => (ICommand)GetValue(EditServiceCommandProperty);
            set => SetValue(EditServiceCommandProperty, value);
        }

        public ICommand DeleteServiceCommand
        {
            get => (ICommand)GetValue(DeleteServiceCommandProperty);
            set => SetValue(DeleteServiceCommandProperty, value);
        }

        public ServiceSectionView()
        {
            InitializeComponent();
        }
    }
}
