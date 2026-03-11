using System;
using System.IO;
using Microsoft.Data.Sqlite;

class Cleanup
{
    static void Main()
    {
        string dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Titan", "GymManagement.db");
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        // 1. Identify corrupted appointments
        // Corrupted if:
        // - service_name is empty or null
        // - staff_name is 'Inconnu'
        // - OR status is Scheduled but it's using a Plan GUID (hard to check in SQL easily, so we stick to names)
        
        string selectQuery = "SELECT COUNT(*) FROM appointments WHERE service_name = '' OR service_name IS NULL OR staff_name = 'Inconnu'";
        using (var cmd = new SqliteCommand(selectQuery, connection))
        {
            long count = (long)cmd.ExecuteScalar();
            Console.WriteLine($"Found {count} corrupted appointments.");
        }

        // 2. Mark as DataError (Status 6, since we added it at the end of the enum)
        // Enum: Scheduled(0), Confirmed(1), InProgress(2), Completed(3), NoShow(4), Cancelled(5), DataError(6)
        string updateQuery = "UPDATE appointments SET status = 6 WHERE service_name = '' OR service_name IS NULL OR staff_name = 'Inconnu'";
        using (var cmd = new SqliteCommand(updateQuery, connection))
        {
            int updated = cmd.ExecuteNonQuery();
            Console.WriteLine($"Updated {updated} appointments to status 'DataError' (6).");
        }
    }
}
