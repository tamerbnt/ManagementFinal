#r "nuget: Npgsql, 8.0.2"

using System;
using System.Data;
using Npgsql;

public class Program
{
    public static void Main()
    {
        using (var conn = new NpgsqlConnection(@"Host=aws-1-eu-west-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.shnuwxfnmirffxjrcaui;Password=VG+9&%72_SvHmYq;CommandTimeout=60;Pooling=true;"))
        {
            conn.Open();
            using (var cmd = new NpgsqlCommand("SELECT id, email, allowed_modules FROM staff_members", conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    Console.WriteLine("ID: " + reader[0] + " Email: " + reader[1] + " Modules: " + (reader.IsDBNull(2) ? "NULL" : reader[2].ToString()));
                }
            }
        }
    }
}
Program.Main();
