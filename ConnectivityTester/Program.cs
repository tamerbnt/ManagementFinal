using System;
using Npgsql;

class Program
{
    static void Main(string[] args)
    {
        var connString = "Host=aws-1-eu-west-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.shnuwxfnmirffxjrcaui;Password=VG+9&%72_SvHmYq;Pooling=false;";
        
        try
        {
            using var conn = new NpgsqlConnection(connString);
            conn.Open();
            Console.WriteLine("✅ CONNECTED SUCCESS!");
            
            Console.WriteLine("\n--- Tables in 'public' schema ---");
            using var cmd = new NpgsqlCommand(
                "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE';", 
                conn);
            
            using var reader = cmd.ExecuteReader();
            int count = 0;
            while (reader.Read())
            {
                Console.WriteLine($"- {reader.GetString(0)}");
                count++;
            }
            
            if (count == 0)
            {
                Console.WriteLine("⚠️ No tables found in 'public' schema.");
            }
            else 
            {
                Console.WriteLine($"Total: {count} tables.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ FAILED: {ex.Message}");
        }
    }
}
