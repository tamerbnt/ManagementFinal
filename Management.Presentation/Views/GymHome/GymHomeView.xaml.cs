using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Management.Presentation.ViewModels.GymHome;
using Management.Infrastructure.Hardware;

namespace Management.Presentation.Views.GymHome
{
    public partial class GymHomeView : UserControl
    {
        private readonly ScannerService _scannerService = new();

        public GymHomeView()
        {
            InitializeComponent();
            _scannerService.ScanCompleted += OnScanCompleted;
            
            // Hook into the parent window's key events to capture global input
            this.Loaded += (s, e) =>
            {
                var window = Window.GetWindow(this);
                if (window != null)
                {
                    window.PreviewKeyDown += OnWindowPreviewKeyDown;
                }
            };

            this.Unloaded += (s, e) =>
            {
                var window = Window.GetWindow(this);
                if (window != null)
                {
                    window.PreviewKeyDown -= OnWindowPreviewKeyDown;
                }
            };
        }

        private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Don't capture keys if an input control has focus (e.g., search box in top bar)
            if (Keyboard.FocusedElement is TextBox or PasswordBox)
                return;

            string keyText = string.Empty;

            // Handle numeric and alpha keys for the scanner buffer
            if (e.Key >= Key.D0 && e.Key <= Key.D9)
                keyText = (e.Key - Key.D0).ToString();
            else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
                keyText = (e.Key - Key.NumPad0).ToString();
            else if (e.Key >= Key.A && e.Key <= Key.Z)
                keyText = e.Key.ToString();
            else if (e.Key == Key.Return)
                keyText = "\r";

            if (!string.IsNullOrEmpty(keyText))
            {
                _scannerService.ProcessKey(keyText);
                e.Handled = true; // Mark as handled to prevent other UI elements from reacting
            }
        }

        private void OnScanCompleted(string scanData)
        {
            if (DataContext is GymHomeViewModel viewModel)
            {
                viewModel.ScanInput = scanData;
                viewModel.ScanCommand.Execute(null);
            }
        }
    }
}
