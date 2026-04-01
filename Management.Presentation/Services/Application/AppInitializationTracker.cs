using System.ComponentModel;
using System.Runtime.CompilerServices;
using Management.Application.Interfaces.App;

namespace Management.Presentation.Services.Application
{
    public class AppInitializationTracker : IAppInitializationTracker
    {
        private bool _isComplete;
        public bool IsComplete
        {
            get => _isComplete;
            private set => SetProperty(ref _isComplete, value);
        }

        private double _progress;
        public double Progress
        {
            get => _progress;
            private set => SetProperty(ref _progress, value);
        }

        private string _statusMessage = "Starting application...";
        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public void UpdateStatus(string message, double progress)
        {
            StatusMessage = message;
            Progress = progress;
        }

        public void SetComplete()
        {
            IsComplete = true;
            Progress = 100;
            StatusMessage = "Ready";
        }

        public void Reset()
        {
            IsComplete = false;
            Progress = 0;
            StatusMessage = "Initializing...";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value)) return;
            storage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
