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

                        // Data cleanup: Always remove local license and workspace data on uninstall.
                        // The user already consented to this when they clicked Uninstall in Windows.
                        // ProgramData requires admin rights — use elevated PowerShell (UAC prompt).
                        // LocalAppData is user-owned — delete directly without elevation.
                        
                        var commonData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                        // 1. ProgramData\Atrium — Elevated deletion (required for admin-owned folder)
                        var luxCommon = Path.Combine(commonData, "Atrium");
                        if (Directory.Exists(luxCommon))
                        {
                            try
                            {
                                var psArgs = $"-NoProfile -NonInteractive -Command \"Remove-Item -LiteralPath '{luxCommon}' -Recurse -Force -ErrorAction SilentlyContinue\"";
                                var proc = Process.Start(new ProcessStartInfo
                                {
                                    FileName = "powershell.exe",
                                    Arguments = psArgs,
                                    Verb = "runas",
                                    UseShellExecute = true,
                                    WindowStyle = ProcessWindowStyle.Hidden
                                });
                                proc?.WaitForExit(10000); // Wait up to 10 seconds
                                Serilog.Log.Information("[Uninstall] ProgramData\\Atrium cleanup completed.");
                            }
                            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception win32 && win32.NativeErrorCode == 1223)
                            {
                                // Error 1223 = Operation cancelled by user (UAC declined)
                                Serilog.Log.Warning("[Uninstall] User declined UAC for ProgramData cleanup. License files remain.");
                            }
                            catch (Exception ex)
                            {
                                Serilog.Log.Error(ex, "[Uninstall] ProgramData cleanup failed unexpectedly.");
                            }
                        }

                        // 2. LocalAppData\Atrium — Direct deletion (user owns this path, no elevation needed)
                        var luxLocal = Path.Combine(localAppData, "Atrium");
                        if (Directory.Exists(luxLocal))
                        {
                            try
                            {
                                Directory.Delete(luxLocal, true);
                                Serilog.Log.Information("[Uninstall] LocalAppData\\Atrium cleanup completed.");
                            }
                            catch (Exception ex)
                            {
                                Serilog.Log.Warning(ex, "[Uninstall] LocalAppData cleanup failed.");
                            }
                        }
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
                var dataDir = Path.Combine(programData, "Atrium");
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
                        Path.Combine(programData, "Atrium", "GymManagement.db"), 
                        Path.Combine(programData, "Atrium", "backups", "pre-update", $"emergency_v{newVersion}.db"), 
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
                var dbPath = Path.Combine(programData, "Atrium", "GymManagement.db");
                
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
                var names = new[] { "Atrium.Client", "Atrium", "Management.Presentation", "GymOS" };
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
