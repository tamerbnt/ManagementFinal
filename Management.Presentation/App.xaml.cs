// Correct Namespaces
using Management.Application.Services;
using Management.Application.Stores;
using Management.Domain.Interfaces;
using Management.Domain.Services;
using Management.Infrastructure.Data;
using Management.Infrastructure.Repositories;
using Management.Infrastructure.Services;
using Management.Presentation.Services;
using Management.Presentation.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Supabase;
using System.IO;
using System.Windows;

namespace Management.Presentation
{
    public partial class App : System.Windows.Application
    {
        public IServiceProvider ServiceProvider { get; private set; }
        public IConfiguration Configuration { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            // 1. Config
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            Configuration = builder.Build();

            // 2. DI Container
            var services = new ServiceCollection();

            // --- Stores (Singletons) ---
            services.AddSingleton<NavigationStore>();
            services.AddSingleton<AccountStore>();
            services.AddSingleton<ModalNavigationStore>();

            // --- Services ---
            services.AddSingleton<IDispatcher>(new WpfDispatcher(System.Windows.Application.Current.Dispatcher));
            services.AddSingleton<INavigationService, NavigationService>(provider =>
                new NavigationService(
                    t => provider.GetRequiredService(t),
                    provider.GetRequiredService<IDispatcher>()
                ));

            services.AddTransient<IAuthenticationService, AuthenticationService>();
            services.AddSingleton<IConnectionService, ConnectionService>();

            // --- Repos & Infrastructure ---
            services.AddTransient<IStaffRepository, StaffRepository>();
            services.AddTransient<GymDbContext>();
            services.AddSingleton<Client>(new Client("http://mock", "mock")); // Placeholder

            // --- ViewModels ---
            

            // 3. Show Window
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }
    }
}