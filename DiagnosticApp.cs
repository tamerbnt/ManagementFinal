using System;
using Microsoft.Data.Sqlite;

namespace DiagnosticApp
{
    class Program
    {
        static void Main(string[] args)
        {
            string dbPath = @"c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy\Management.Presentation\bin\Debug\net8.0-windows\Management.db";
            string connectionString = $"Data Source={dbPath};";

            try
            {
                using (var connection = new SqliteConnection(connectionString))
                {
                    connection.Open();
                    var command = connection.CreateCommand();
                    command.CommandText = "SELECT EntityType, Action, ErrorCount, LastError FROM OutboxMessages WHERE IsProcessed = 0 ORDER BY CreatedAt DESC LIMIT 10;";

                    using (var reader = command.ExecuteReader())
                    {
                        Console.WriteLine("--- PENDING OUTBOX MESSAGES ---");
                        while (reader.Read())
                        {
                            Console.WriteLine($"Type: {reader["EntityType"]}, Action: {reader["Action"]}, Errors: {reader["ErrorCount"]}");
                            Console.WriteLine($"Error: {reader["LastError"]}");
                            Console.WriteLine("-------------------------------");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Diagnostic failed: {ex.Message}");
            }
        }
    }
}
