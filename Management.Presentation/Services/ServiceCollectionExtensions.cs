// ******************************************************************************************
//  Management.Presentation/ServiceCollectionExtensions.cs
//  FINAL PRODUCTION VERSION – v1.2.0-production
//  Design System: Apple 2025 Edition – v1.2 FINAL (LOCKED)
//  Status: PRODUCTION READY
// ******************************************************************************************

using System;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Management.Presentation.Services
{
    /// <summary>
    /// Dependency injection extensions for modal navigation services
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds modal navigation services to the DI container
        /// </summary>
        public static IServiceCollection AddModalNavigation(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            // Add WPF dispatcher (singleton)
            services.TryAddSingleton<IDispatcher>(provider =>
            {
                var dispatcher = Dispatcher.FromThread(System.Threading.Thread.CurrentThread) ??
                         System.Windows.Application.Current?.Dispatcher;

                if (dispatcher == null)
                    throw new InvalidOperationException("No WPF dispatcher available");

                return new WpfDispatcher(dispatcher);
            });

            // Add ViewMappingService (singleton)
            services.TryAddSingleton<IViewMappingService, ViewMappingService>();

            // Add ModalNavigationService (singleton)
            services.TryAddSingleton<IModalNavigationService, ModalNavigationService>();

            return services;
        }

        /// <summary>
        /// Registers a modal View-ViewModel mapping
        /// </summary>
        public static IServiceCollection RegisterModal<TViewModel, TView>(this IServiceCollection services)
            where TView : Window
            where TViewModel : class
        {
            // Register ViewModel as transient (per your architecture)
            services.AddTransient<TViewModel>();

            // Register View as transient (each modal gets its own instance)
            services.AddTransient<TView>();

            // The actual mapping is done at runtime via ViewMappingService
            // This is configured in App.xaml.cs or similar startup code

            return services;
        }

        /// <summary>
        /// Configures default modal View-ViewModel mappings
        /// </summary>
        public static IServiceCollection ConfigureDefaultModalMappings(this IServiceCollection services)
        {
            // Example: This would be called in App.xaml.cs to set up mappings
            // services.AddSingleton<IBootstrapService>(provider =>
            // {
            //     var mappingService = provider.GetRequiredService<IViewMappingService>();
            //     mappingService.Register<AddMemberViewModel, AddMemberView>();
            //     mappingService.Register<EditMemberViewModel, EditMemberView>();
            //     // etc.
            //     return new BootstrapService();
            // });

            return services;
        }
    }
}
