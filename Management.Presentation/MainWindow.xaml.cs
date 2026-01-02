using System.Windows;
using Management.Presentation.ViewModels; // Add this namespace

namespace Management.Presentation
{
    public partial class MainWindow : Window
    {
        // 1. Default Constructor (Required by XAML Designer)
        public MainWindow()
        {
            InitializeComponent();
        }

        // 2. Injection Constructor (Used by App.xaml.cs)
        public MainWindow(MainViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}