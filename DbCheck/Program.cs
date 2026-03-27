using System;
using System.IO;
using Microsoft.Data.Sqlite;

class Program
{
    static void Main()
    {
        var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Luxurya", "GymManagement.db");
        if (!File.Exists(dbPath))
        {
            dbPath = @"C:\Users\techbox\.gemini\antigravity\ManagementBackup1234\Management.Presentation\bin\Debug\net8.0-windows\GymManagement.db";
        }
        
        Console.WriteLine($"DB Path: {dbPath}");
        if (!File.Exists(dbPath)) { Console.WriteLine("DB not found."); return; }

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(payroll_entries);";
        using var reader = cmd.ExecuteReader();
        Console.WriteLine("--- Columns ---");
        while (reader.Read())
        {
            Console.WriteLine($"{reader["cid"]} | {reader["name"]} | {reader["type"]}");
        }

        using var cmd2 = connection.CreateCommand();
        cmd2.CommandText = "SELECT id, staff_member_id, amount, amount_currency, paid_amount, paid_amount_currency, is_paid, created_at, updated_at FROM payroll_entries ORDER BY created_at DESC LIMIT 5;";
        using var reader2 = cmd2.ExecuteReader();
        Console.WriteLine("--- Top 5 Rows ---");
        for (int i = 0; i < reader2.FieldCount; i++) { Console.Write(reader2.GetName(i) + " | "); }
        Console.WriteLine();
        while (reader2.Read())
        {
            for (int i = 0; i < reader2.FieldCount; i++) { Console.Write(reader2[i] + " | "); }
            Console.WriteLine();
        }
    }
}
