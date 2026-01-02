using System;
using System.IO;
using System.Windows;
using Serilog;

namespace Management.Presentation
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                // Basic console logging for immediate feedback
                Console.WriteLine("PROGRAM STARTED");
                
                // Early Serilog Setup
                Log.Logger = new LoggerConfiguration()
                    .WriteTo.File("logs/boot-error-.txt", rollingInterval: RollingInterval.Day)
                    .CreateLogger();

                Log.Information("Bootstrapping Application...");

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
