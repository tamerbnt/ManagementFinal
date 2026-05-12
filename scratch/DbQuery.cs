using System;
using System.Data;
using Npgsql;

class Program
{
    static void Main()
    {
        string connStr = "Host=aws-0-eu-central-1.pooler.supabase.com;Database=postgres;Username=postgres.shnuwxfnmirffxjrcaui;Password=VG+9&%72_SvHmYq;Port=5432;";
        using var conn = new NpgsqlConnection(connStr);
        conn.Open();

        RunQuery(conn, "SELECT tablename, policyname, permissive, roles, cmd FROM pg_policies WHERE schemaname = 'public' ORDER BY tablename, cmd;");
        
        RunQuery(conn, @"
        SELECT column_name, COUNT(*) as total_rows, SUM(CASE WHEN column_value IS NULL THEN 1 ELSE 0 END) as null_count
        FROM (
            SELECT 'tenant_id' as column_name, tenant_id::text as column_value FROM licenses
            UNION ALL SELECT 'device_id', device_id::text FROM licenses
            UNION ALL SELECT 'license_key', license_key FROM licenses
            UNION ALL SELECT 'expires_at', expires_at::text FROM licenses
            UNION ALL SELECT 'tier', tier FROM licenses
        ) sub GROUP BY column_name;");

        RunQuery(conn, @"
        SELECT COUNT(*) as total,
            SUM(CASE WHEN tenant_id IS NULL THEN 1 ELSE 0 END) as null_tenant,
            SUM(CASE WHEN facility_id IS NULL THEN 1 ELSE 0 END) as null_facility,
            SUM(CASE WHEN full_name IS NULL THEN 1 ELSE 0 END) as null_name
        FROM staff_members;");

        RunQuery(conn, @"
        SELECT 'staff_members' as tbl, COUNT(*) as rows FROM staff_members
        UNION ALL SELECT 'tenants', COUNT(*) FROM tenants
        UNION ALL SELECT 'licenses', COUNT(*) FROM licenses
        UNION ALL SELECT 'facilities', COUNT(*) FROM facilities
        UNION ALL SELECT 'registrations', COUNT(*) FROM registrations
        UNION ALL SELECT 'gym_settings', COUNT(*) FROM gym_settings
        UNION ALL SELECT 'daily_facility_snapshots', COUNT(*) FROM daily_facility_snapshots;");
    }

    static void RunQuery(NpgsqlConnection conn, string sql)
    {
        Console.WriteLine("--- QUERY START ---");
        try {
            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            for (int i = 0; i < reader.FieldCount; i++) Console.Write(reader.GetName(i) + "\t");
            Console.WriteLine();
            while (reader.Read())
            {
                for (int i = 0; i < reader.FieldCount; i++) Console.Write(reader[i]?.ToString() + "\t");
                Console.WriteLine();
            }
        } catch (Exception ex) {
            Console.WriteLine("Error: " + ex.Message);
        }
        Console.WriteLine("--- QUERY END ---");
    }
}
