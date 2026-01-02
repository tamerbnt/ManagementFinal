using System;
using System.IO;
using System.Threading.Tasks;

namespace Management.Infrastructure.Services
{
    public interface IBackupService
    {
        Task<string> CreateBackupAsync();
        Task RestoreBackupAsync(string backupPath);
    }

    public class BackupService : IBackupService
    {
        private readonly string _dbPath;
        private readonly string _backupFolder;

        public BackupService()
        {
            _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gym.db");
            _backupFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups");
            
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

            // Simple file copy for SQLite backup (when not actively writing)
            // For production with heavy usage, SQLite 'VACUUM INTO' is better.
            await Task.Run(() => File.Copy(_dbPath, backupPath, true));

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
    }
}
