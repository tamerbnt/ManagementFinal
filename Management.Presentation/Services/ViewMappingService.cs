// ******************************************************************************************
//  Management.Presentation/Services/ViewMappingService.cs
//  FINAL PRODUCTION VERSION – v1.2.0-production
//  Design System: Apple 2025 Edition – v1.2 FINAL (LOCKED)
//  Status: PRODUCTION READY
// ******************************************************************************************

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Management.Presentation.Services
{
    /// <summary>
    /// Production ViewMappingService - Manual View-ViewModel mapping for modal windows
    /// </summary>
    public sealed class ViewMappingService : IViewMappingService, IDisposable
    {
        private readonly ConcurrentDictionary<Type, Type> _viewModelToViewMap = new();
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ViewMappingService>? _logger;
        private bool _isDisposed;

        public ViewMappingService(
            IServiceProvider serviceProvider,
            ILogger<ViewMappingService>? logger = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger;

            LogServiceInitialization();
        }

        public void Register<TViewModel, TView>()
            where TView : Window
            where TViewModel : class
        {
            ThrowIfDisposed();

            var viewModelType = typeof(TViewModel);
            var viewType = typeof(TView);

            if (!_viewModelToViewMap.TryAdd(viewModelType, viewType))
            {
                var existing = _viewModelToViewMap[viewModelType];
                throw new InvalidOperationException(
                    $"ViewModel {viewModelType.Name} is already registered to View {existing.Name}");
            }

            _logger?.LogInformation("Registered View mapping: {ViewModel} -> {View}",
                viewModelType.Name, viewType.Name);
        }

        public Type GetViewType<TViewModel>() where TViewModel : class
        {
            ThrowIfDisposed();

            var viewModelType = typeof(TViewModel);
            return GetViewType(viewModelType);
        }

        public Type GetViewType(Type viewModelType)
        {
            ThrowIfDisposed();

            if (!_viewModelToViewMap.TryGetValue(viewModelType, out var viewType))
            {
                throw new KeyNotFoundException(
                    $"No View registered for ViewModel {viewModelType.Name}. " +
                    $"Call Register<{viewModelType.Name}, TView>() first.");
            }

            return viewType;
        }

        public Window CreateView<TViewModel>(TViewModel viewModel) where TViewModel : class
        {
            ThrowIfDisposed();

            if (viewModel == null)
                throw new ArgumentNullException(nameof(viewModel));

            var viewType = GetViewType<TViewModel>();
            return CreateViewInstance(viewType, viewModel);
        }

        public Window CreateView(Type viewModelType, object viewModel)
        {
            ThrowIfDisposed();

            if (viewModelType == null)
                throw new ArgumentNullException(nameof(viewModelType));

            if (viewModel == null)
                throw new ArgumentNullException(nameof(viewModel));

            if (!viewModelType.IsInstanceOfType(viewModel))
            {
                throw new ArgumentException(
                    $"viewModel must be an instance of {viewModelType.Name}");
            }

            var viewType = GetViewType(viewModelType);
            return CreateViewInstance(viewType, viewModel);
        }

        private Window CreateViewInstance(Type viewType, object viewModel)
        {
            try
            {
                // Try to create via DI first (allows constructor injection)
                var window = _serviceProvider.GetService(viewType) as Window;
                if (window != null)
                {
                    window.DataContext = viewModel;
                    return window;
                }

                // Fallback to Activator
                window = Activator.CreateInstance(viewType) as Window;
                if (window == null)
                {
                    throw new InvalidOperationException(
                        $"Failed to create Window instance of type {viewType.Name}");
                }

                window.DataContext = viewModel;
                return window;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to create View instance for {ViewType}", viewType.Name);
                throw new InvalidOperationException(
                    $"Failed to create View instance for {viewType.Name}. See inner exception.", ex);
            }
        }

        public IReadOnlyDictionary<Type, Type> GetAllMappings()
        {
            ThrowIfDisposed();
            return new Dictionary<Type, Type>(_viewModelToViewMap);
        }

        public bool IsRegistered<TViewModel>() where TViewModel : class
        {
            ThrowIfDisposed();
            return _viewModelToViewMap.ContainsKey(typeof(TViewModel));
        }

        public void ClearMappings()
        {
            ThrowIfDisposed();
            _viewModelToViewMap.Clear();
            _logger?.LogInformation("Cleared all View mappings");
        }

        private void LogServiceInitialization()
        {
            _logger?.LogInformation("ViewMappingService initialized with {InitialMappings} mappings",
                _viewModelToViewMap.Count);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(ViewMappingService));
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            _viewModelToViewMap.Clear();
            _logger?.LogInformation("ViewMappingService disposed");
        }
    }
}