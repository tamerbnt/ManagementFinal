using CommunityToolkit.Mvvm.ComponentModel;
using Management.Presentation.Services;
using Management.Application.Stores;
using Management.Presentation.Stores;
using Management.Presentation.Extensions;
using System;

namespace Management.Presentation.ViewModels
{
    public partial class AuthViewModel : ViewModelBase, IDisposable
    {
        private readonly ModalNavigationStore _modalNavigationStore;
        private readonly NavigationStore _navigationStore;

        private ViewModelBase? _currentView;
        public ViewModelBase? CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }
        
        private object? _currentModal;
        public object? CurrentModal
        {
            get => _currentModal;
            set => SetProperty(ref _currentModal, value);
        }

        private bool _isModalOpen;
        public bool IsModalOpen
        {
            get => _isModalOpen;
            set => SetProperty(ref _isModalOpen, value);
        }

        public AuthViewModel(ModalNavigationStore modalNavigationStore, NavigationStore navigationStore)
        {
            _modalNavigationStore = modalNavigationStore;
            _modalNavigationStore.PropertyChanged += OnModalStorePropertyChanged;

            _navigationStore = navigationStore;
            _currentView = _navigationStore.CurrentViewModel as ViewModelBase;
            _navigationStore.CurrentViewModelChanged += OnCurrentViewModelChanged;

            CurrentModal = _modalNavigationStore.CurrentModalViewModel;
            IsModalOpen = _modalNavigationStore.IsOpen;
        }

        private void OnCurrentViewModelChanged()
        {
            CurrentView = _navigationStore.CurrentViewModel as ViewModelBase;
        }

        private void OnModalStorePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ModalNavigationStore.CurrentModalViewModel) ||
                e.PropertyName == nameof(ModalNavigationStore.IsOpen))
            {
                CurrentModal = _modalNavigationStore.CurrentModalViewModel;
                IsModalOpen = _modalNavigationStore.IsOpen;
            }
        }

        public void Dispose()
        {
            _modalNavigationStore.PropertyChanged -= OnModalStorePropertyChanged;
            if (_navigationStore != null)
            {
                _navigationStore.CurrentViewModelChanged -= OnCurrentViewModelChanged;
            }
        }
    }
}
