using System;

namespace Management.Application.Stores
{
    /// <summary>
    /// The Single Source of Truth for the application's main navigation state.
    /// This store is held as a Singleton in the DI container.
    /// </summary>
    public class NavigationStore
    {
        // Event raised whenever the view changes, notifying MainViewModel to update the UI
        public event Action CurrentViewModelChanged;

        private object _currentViewModel;

        /// <summary>
        /// The currently active ViewModel (e.g., DashboardViewModel, MembersViewModel).
        /// </summary>
        public object CurrentViewModel
        {
            get => _currentViewModel;
            set
            {
                // 1. Cleanup Logic (Memory Management)
                // If the previous view needs to stop timers or unsubscribe events, it should implement IDisposable.
                if (_currentViewModel is IDisposable disposableVm)
                {
                    disposableVm.Dispose();
                }

                // 2. State Update
                _currentViewModel = value;

                // 3. Notification
                OnCurrentViewModelChanged();
            }
        }

        /// <summary>
        /// Helper method to trigger the event.
        /// </summary>
        private void OnCurrentViewModelChanged()
        {
            CurrentViewModelChanged?.Invoke();
        }
    }
}