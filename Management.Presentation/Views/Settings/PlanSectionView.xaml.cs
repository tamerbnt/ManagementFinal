using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Management.Presentation.Views.Settings
{
    public partial class PlanSectionView : UserControl
    {
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(PlanSectionView), new PropertyMetadata(null));

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(PlanSectionView), new PropertyMetadata(string.Empty, OnTitleChanged));

        public static readonly DependencyProperty AddPlanCommandProperty =
            DependencyProperty.Register(nameof(AddPlanCommand), typeof(ICommand), typeof(PlanSectionView), new PropertyMetadata(null));

        public static readonly DependencyProperty EditPlanCommandProperty =
            DependencyProperty.Register(nameof(EditPlanCommand), typeof(ICommand), typeof(PlanSectionView), new PropertyMetadata(null));

        public static readonly DependencyProperty DeletePlanCommandProperty =
            DependencyProperty.Register(nameof(DeletePlanCommand), typeof(ICommand), typeof(PlanSectionView), new PropertyMetadata(null));

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

        public ICommand AddPlanCommand
        {
            get => (ICommand)GetValue(AddPlanCommandProperty);
            set => SetValue(AddPlanCommandProperty, value);
        }

        public ICommand EditPlanCommand
        {
            get => (ICommand)GetValue(EditPlanCommandProperty);
            set => SetValue(EditPlanCommandProperty, value);
        }

        public ICommand DeletePlanCommand
        {
            get => (ICommand)GetValue(DeletePlanCommandProperty);
            set => SetValue(DeletePlanCommandProperty, value);
        }

        public PlanSectionView()
        {
            InitializeComponent();
        }

        private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PlanSectionView view)
            {
                view.SectionTitle.Text = e.NewValue?.ToString();
            }
        }
    }
}
