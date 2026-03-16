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

        private readonly System.Collections.Generic.HashSet<Type> _persistentTypes = new();

        /// <summary>
        /// Registers a ViewModel type as persistent (Singleton/never disposed on navigation).
        /// </summary>
        public void RegisterPersistentType(Type type)
        {
            _persistentTypes.Add(type);
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
                    // Only dispose if outgoing VM is not registered as persistent
                    if (!_persistentTypes.Contains(_currentViewModel.GetType()))
                    {
                        disposableVm.Dispose();
                    }
                }

                // 2. State Update (Atomic)
                _currentViewModel = value;
                
                // Do not call OnCurrentViewModelChanged for NextViewModel/IsNavigating
                // Update backing fields directly to skip redundant event invocations
                _nextViewModel = null; 
                _isNavigating = false;

                // 3. Single Notification
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
