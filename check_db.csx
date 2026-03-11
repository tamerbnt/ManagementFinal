using System;
using Microsoft.Data.Sqlite;
using System.IO;

var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Titan", "GymManagement.db");
using var conn = new SqliteConnection($"Data Source={dbPath}");
conn.Open();

var cmd = conn.CreateCommand();
cmd.CommandText = "SELECT id, status, party_size, created_at FROM restaurant_orders ORDER BY created_at DESC LIMIT 10;";
using var reader = cmd.ExecuteReader();

Console.WriteLine("Recent Orders:");
while (reader.Read())
{
    Console.WriteLine($"{reader.GetString(0)} | Status: {reader.GetInt32(1)} | Party: {reader.GetInt32(2)} | CreatedAt: {reader.GetString(3)}");
}
