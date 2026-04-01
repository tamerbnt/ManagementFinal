using System;
using System.IO;
using System.Linq;
using System.Windows;
using Serilog;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.WPF;

namespace Management.Presentation
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // Initialize LiveCharts Global Configuration as early as possible
            LiveChartsCore.LiveCharts.Configure(config => 
                config
                    .AddSkiaSharp()
                    .AddDefaultMappers()
            );

            try
            {
                // Basic console logging for immediate feedback
                Console.WriteLine("PROGRAM STARTED");
                
                // Early Serilog Setup
                Log.Logger = new LoggerConfiguration()
                    .WriteTo.File("logs/boot-error-.txt", rollingInterval: RollingInterval.Day)
                    .CreateLogger();

                Log.Information("Bootstrapping Application...");

                // --- EF CORE DESIGN TIME BYPASS ---
                // When running `dotnet ef migrations` or `database update`, the CLI attempts to 
                // build the host to discover DbContexts. If it tries to load WPF resources, it will crash.
                var isEfCoreTool = AppDomain.CurrentDomain.GetAssemblies()
                    .Any(a => a.FullName?.StartsWith("ef,") == true || a.FullName?.Contains("EntityFrameworkCore.Design") == true);
                
                if (isEfCoreTool)
                {
                    Console.WriteLine("EF Core Design Time Detected in Program.Main. Halting WPF bootstrapping.");
                    // Return early so EF Core tools drop back to looking for IDesignTimeDbContextFactory
                    // which we implemented in AppDbContextFactory.cs
                    return;
                }

                Console.WriteLine("DEBUG: Creating App instance...");
                var app = new App();
                Console.WriteLine("DEBUG: Initializing Component...");
                app.InitializeComponent(); // This parses App.xaml
                
                Console.WriteLine("DEBUG: Entering app.Run()...");
                // Force app to stay alive even if no window is shown yet
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                
                app.Run();
                Console.WriteLine("DEBUG: app.Run() has exited.");
            }
            catch (Exception ex)
            {
                var msg = $"FATAL CRASH: {ex.Message}\n{ex.StackTrace}";
                Console.WriteLine(msg);
                if (Log.Logger != null)
                {
                    Log.Fatal(ex, "Application crashed during bootstrapping");
                }
                
                MessageBox.Show(msg, "Fatal Crash", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
