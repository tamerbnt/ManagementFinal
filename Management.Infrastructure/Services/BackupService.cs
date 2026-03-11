using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Management.Infrastructure.Data;

namespace Management.Infrastructure.Services
{
    public interface IBackupService
    {
        Task<string> CreateBackupAsync();
        Task RestoreBackupAsync(string backupPath);
        Task CleanupOldBackupsAsync(int daysToKeep);
        Task<bool> IsBackupNeededTodayAsync();
        string GetBackupFolderPath();
        Task<(DateTime? LastDate, long LastSize)> GetLastBackupMetadataAsync();
    }

    public class BackupService : IBackupService
    {
        private readonly AppDbContext _context;
        private readonly string _dbPath;
        private readonly string _backupFolder;

        public BackupService(AppDbContext context)
        {
            _context = context;
            
            // Database is in LocalAppData
            var titanDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Titan");
            _dbPath = Path.Combine(titanDataFolder, "GymManagement.db");

            // Backups are in MyDocuments for better persistence/sync
            _backupFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Titan", "Backups");
            
            if (!Directory.Exists(_backupFolder))
            {
                Directory.CreateDirectory(_backupFolder);
            }
        }

        public async Task<string> CreateBackupAsync()
        {
            if (!File.Exists(_dbPath))
            {
                throw new FileNotFoundException("Database file not found.", _dbPath);
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupFileName = $"gym_backup_{timestamp}.db";
            string backupPath = Path.Combine(_backupFolder, backupFileName);

            // Execute safe online backup using SQLite's VACUUM INTO
            // This ensures a consistent snapshot even if the DB is under load.
            await _context.Database.ExecuteSqlRawAsync($"VACUUM INTO '{backupPath}'");

            return backupPath;
        }

        public async Task RestoreBackupAsync(string backupPath)
        {
            if (!File.Exists(backupPath))
            {
                throw new FileNotFoundException("Backup file not found.", backupPath);
            }

            // Note: Restoring requires the DB to be closed. 
            // In a real app, this might require a restart or dropping connections.
            // For now, we just copy it back.
            await Task.Run(() => File.Copy(backupPath, _dbPath, true));
        }

        public async Task CleanupOldBackupsAsync(int daysToKeep)
        {
            await Task.Run(() => 
            {
                if (!Directory.Exists(_backupFolder)) return;

                var threshold = DateTime.Now.AddDays(-daysToKeep);
                var files = Directory.GetFiles(_backupFolder, "gym_backup_*.db");

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < threshold)
                    {
                        try { fileInfo.Delete(); } catch { /* Ignore locked files */ }
                    }
                }
            });
        }

        public async Task<bool> IsBackupNeededTodayAsync()
        {
            return await Task.Run(() => 
            {
                if (!Directory.Exists(_backupFolder)) return true;

                string todayPattern = $"gym_backup_{DateTime.Now:yyyyMMdd}_*.db";
                return !Directory.GetFiles(_backupFolder, todayPattern).Any();
            });
        }

        public string GetBackupFolderPath() => _backupFolder;

        public async Task<(DateTime? LastDate, long LastSize)> GetLastBackupMetadataAsync()
        {
            return await Task.Run(() => 
            {
                if (!Directory.Exists(_backupFolder)) return (null, 0L);

                var lastFile = Directory.GetFiles(_backupFolder, "gym_backup_*.db")
                                        .Select(f => new FileInfo(f))
                                        .OrderByDescending(f => f.CreationTime)
                                        .FirstOrDefault();

                return (lastFile?.CreationTime, lastFile?.Length ?? 0L);
            });
        }
    }
}
