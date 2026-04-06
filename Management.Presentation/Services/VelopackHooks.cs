using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using Velopack;
using Microsoft.EntityFrameworkCore;
using Management.Infrastructure.Data;

namespace Management.Presentation.Services
{
    public static class VelopackHooks
    {
        public static void Run()
        {
            try
            {
                VelopackApp.Build()
                    .WithFirstRun(version =>
                    {
                        // 1. Register ZKTeco on First Install
                        RegisterZKTecoSilent(force: false);
                    })
                    .WithBeforeUpdateFastCallback(version =>
                    {
                        // 1. Pre-update Data Backup
                        BackupDatabaseBeforeUpdate(version);
                    })
                    .WithAfterUpdateFastCallback(version =>
                    {
                        // 1. Silent COM verification (doesn't prompt if already registered)
                        RegisterZKTecoSilent(force: false);

                        // 3. Migrate Database
                        RunDatabaseMigrations();

                        // 4. Kill Ghost Processes
                        KillGhostProcesses();
                    })
                    .WithBeforeUninstallFastCallback(version =>
                    {
                        // Clean up COM registrations to avoid orphaned registry keys
                        UnregisterZKTeco();
                    })
                    .Run();
            }
            catch (Exception ex)
            {
                // If Velopack hooks crash, write to desktop for extreme diagnostics because Serilog isn't ready
                try
                {
                    File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "velopack_crash.txt"), ex.ToString());
                }
                catch { }
            }
        }

        private static void RegisterZKTecoSilent(bool force)
        {
            try
            {
                var zkDllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "zkemkeeper.dll");
                if (!File.Exists(zkDllPath)) return;

                if (!force && Type.GetTypeFromProgID("zkemkeeper.ZKEM.1") != null)
                {
                    // Already registered. Skipping regsvr32 to avoid UAC prompt.
                    return;
                }

                var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = "regsvr32.exe",
                    Arguments = $"/s \"{zkDllPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                });
                proc?.WaitForExit(10000);
            }
            catch { }
        }

        private static void UnregisterZKTeco()
        {
            try
            {
                var zkDllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "zkemkeeper.dll");
                if (!File.Exists(zkDllPath)) return;

                var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = "regsvr32.exe",
                    Arguments = $"/s /u \"{zkDllPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                });
                proc?.WaitForExit(10000);
            }
            catch { }
        }

        private static void BackupDatabaseBeforeUpdate(NuGet.Versioning.SemanticVersion newVersion)
        {
            try
            {
                var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                var dataDir = Path.Combine(programData, "Luxurya");
                var dbPath = Path.Combine(dataDir, "GymManagement.db");
                
                if (!File.Exists(dbPath)) return;

                var backupDir = Path.Combine(dataDir, "backups", "pre-update");
                Directory.CreateDirectory(backupDir);

                var backupName = $"GymManagement_before_v{newVersion}_{DateTime.Now:yyyyMMdd_HHmmss}.db";
                var backupPath = Path.Combine(backupDir, backupName);

                // Use Vacuum into to safely copy open DB
                var options = new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlite($"Data Source={dbPath};Mode=ReadWrite;Pooling=False;") // Pooling false to not hold locks
                    .Options;

                using (var context = new AppDbContext(options, null!, null!, null!, null!))
                {
                    context.Database.ExecuteSqlRaw($"VACUUM INTO '{backupPath}'");
                }

                // Keep only last 5 pre-update backups
                var oldBackups = Directory.GetFiles(backupDir, "*.db")
                    .OrderByDescending(f => f)
                    .Skip(5);
                foreach (var old in oldBackups)
                {
                    try { File.Delete(old); } catch { }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    // Fallback to basic copy if Vacuum fails
                    var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                    File.Copy(
                        Path.Combine(programData, "Luxurya", "GymManagement.db"), 
                        Path.Combine(programData, "Luxurya", "backups", "pre-update", $"emergency_v{newVersion}.db"), 
                        true);
                }
                catch { }
            }
        }

        private static void RunDatabaseMigrations()
        {
            try
            {
                var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                var dbPath = Path.Combine(programData, "Luxurya", "GymManagement.db");
                
                if (!File.Exists(dbPath)) return;

                var options = new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlite($"Data Source={dbPath};Mode=ReadWrite;Pooling=False;")
                    .Options;

                using (var context = new AppDbContext(options, null!, null!, null!, null!))
                {
                    context.Database.Migrate();
                }
            }
            catch { }
        }

        private static void KillGhostProcesses()
        {
            try
            {
                var currentPid = Process.GetCurrentProcess().Id;
                var names = new[] { "Luxurya.Client", "Luxurya", "Management.Presentation", "GymOS" };
                foreach (var name in names)
                {
                    foreach (var p in Process.GetProcessesByName(name))
                    {
                        try
                        {
                            if (p.Id != currentPid)
                            {
                                p.Kill();
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }
    }
}
