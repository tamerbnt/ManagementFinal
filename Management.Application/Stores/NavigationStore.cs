using System;

using Management.Domain.Interfaces;

namespace Management.Application.Stores
{
    /// <summary>
    /// The Single Source of Truth for the application's main navigation state.
    /// This store is held as a Singleton in the DI container.
    /// </summary>
    public class NavigationStore : IStateResettable
    {
        public void ResetState()
        {
            // Clear current view on reset
            CurrentViewModel = null;
        }
        // Event raised whenever the view changes, notifying MainViewModel to update the UI
        public event Action? CurrentViewModelChanged;

        private object? _currentViewModel;
        private object? _nextViewModel;
        private bool _isNavigating;

        /// <summary>
        /// The ViewModel currently being initialized but not yet active.
        /// </summary>
        public object? NextViewModel
        {
            get => _nextViewModel;
            set
            {
                _nextViewModel = value;
                OnCurrentViewModelChanged();
            }
        }

        /// <summary>
        /// Indicates if a navigation transition is currently in progress.
        /// </summary>
        public bool IsNavigating
        {
            get => _isNavigating;
            set
            {
                _isNavigating = value;
                OnCurrentViewModelChanged();
            }
        }

        /// <summary>
        /// The currently active ViewModel (e.g., DashboardViewModel, MembersViewModel).
        /// </summary>
        public object? CurrentViewModel
        {
            get => _currentViewModel;
            set
            {
                // 1. Cleanup Logic (Memory Management)
                if (_currentViewModel is IDisposable disposableVm)
                {
                    bool isPersistent = value != null && (
                        value.GetType().Name.EndsWith("ViewModel") && 
                        !value.GetType().Name.Contains("Modal") &&
                        !value.GetType().Name.Contains("Detail")
                    );

                    if (!isPersistent)
                    {
                        disposableVm.Dispose();
                    }
                }

                // 2. State Update
                _currentViewModel = value;
                _nextViewModel = null; // Clear next once current is set
                _isNavigating = false;

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
