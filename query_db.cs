using System;
using System.Data.SQLite;

namespace DbQuery
{
    class Program
    {
        static void Main()
        {
            try
            {
                var connStringBuilder = new SQLiteConnectionStringBuilder();
                connStringBuilder.DataSource = @"C:\ProgramData\Luxurya\app.db";
                
                using (var conn = new SQLiteConnection(connStringBuilder.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand("PRAGMA table_info(staff_members);", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        Console.WriteLine("Columns in staff_members:");
                        while (reader.Read())
                        {
                            Console.WriteLine($"- {reader["name"]} ({reader["type"]})");
                        }
                    }

                    using (var cmd = new SQLiteCommand("PRAGMA table_info(Members);", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        Console.WriteLine("\nColumns in Members:");
                        while (reader.Read())
                        {
                            Console.WriteLine($"- {reader["name"]} ({reader["type"]})");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }
    }
}
