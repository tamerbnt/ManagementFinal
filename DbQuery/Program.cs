using System;
using System.IO;
using System.Data;
using Microsoft.Data.Sqlite;

class Program
{
    static void Main(string[] args)
    {
        string dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Titan", "GymManagement.db");
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        ExecuteQuery(connection, "SELECT '--- Facilities ---' AS Header");
        ExecuteQuery(connection, "SELECT id, name FROM facilities");

        ExecuteQuery(connection, "SELECT '--- Members ---' AS Header");
        ExecuteQuery(connection, "SELECT 'Total', COUNT(*) FROM members UNION ALL SELECT 'Active', COUNT(*) FROM members WHERE is_deleted = 0");
        ExecuteQuery(connection, "SELECT facility_id, is_deleted FROM members LIMIT 5");

        ExecuteQuery(connection, "SELECT '--- AccessEvents ---' AS Header");
        ExecuteQuery(connection, "SELECT 'Total', COUNT(*) FROM access_events UNION ALL SELECT 'Today', COUNT(*) FROM access_events WHERE timestamp >= date('now', 'localtime')");

        ExecuteQuery(connection, "SELECT '--- Sales ---' AS Header");
        ExecuteQuery(connection, "SELECT 'Total', COUNT(*) FROM sales UNION ALL SELECT 'Today', COUNT(*) FROM sales WHERE created_at >= date('now', 'localtime')");
        ExecuteQuery(connection, "SELECT created_at, total_amount__amount FROM sales LIMIT 5");

        ExecuteQuery(connection, "SELECT '--- PayrollEntries ---' AS Header");
        ExecuteQuery(connection, "SELECT 'Total', COUNT(*) FROM payroll_entries");

        ExecuteQuery(connection, "SELECT '--- InventoryPurchases ---' AS Header");
        ExecuteQuery(connection, "SELECT 'Total', COUNT(*) FROM inventory_purchases");

        ExecuteQuery(connection, "SELECT '--- Appointments ---' AS Header");
        ExecuteQuery(connection, "SELECT 'Total', COUNT(*) FROM appointments UNION ALL SELECT 'Today+', COUNT(*) FROM appointments WHERE start_time >= date('now', 'localtime')");

        ExecuteQuery(connection, "SELECT '--- RestaurantOrders ---' AS Header");
        ExecuteQuery(connection, "SELECT 'Total', COUNT(*) FROM restaurant_orders");

        ExecuteQuery(connection, "SELECT '--- Registrations ---' AS Header");
        ExecuteQuery(connection, "SELECT 'Total', COUNT(*) FROM registrations UNION ALL SELECT 'Pending', COUNT(*) FROM registrations WHERE status = 0");

        Console.WriteLine("\n--- CLEANUP: Corrupted Salon Appointments ---");
        ExecuteQuery(connection, "SELECT 'Corrupted Total', COUNT(*) FROM appointments WHERE service_name = '' OR service_name IS NULL OR staff_name = 'Inconnu'");
        
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "UPDATE appointments SET status = 6 WHERE service_name = '' OR service_name IS NULL OR staff_name = 'Inconnu'";
            int updated = cmd.ExecuteNonQuery();
            Console.WriteLine($"Successfully updated {updated} corrupted appointments to 'DataError' (6).");
        }
    }

    static void ExecuteQuery(SqliteConnection connection, string query)
    {
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = query;
            using var reader = command.ExecuteReader();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                Console.Write(reader.GetName(i) + " | ");
            }
            Console.WriteLine();

            while (reader.Read())
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    Console.Write(reader.GetValue(i) + " | ");
                }
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.WriteLine();
    }
}
