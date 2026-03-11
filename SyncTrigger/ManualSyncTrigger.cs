using System;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Collections.Generic;

namespace ManualSyncTrigger
{
    class Program
    {
        static void Main(string[] args)
        {
            string dbPath = @"c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy\Management.Presentation\bin\Debug\net8.0-windows\Management.db";
            string connectionString = $"Data Source={dbPath};";

            try 
            {
                using (var conn = new SqliteConnection(connectionString))
                {
                    conn.Open();

                    if (args.Length > 0 && args[0].Trim().ToUpper().StartsWith("SELECT"))
                    {
                        var selectCmd = conn.CreateCommand();
                        selectCmd.CommandText = args[0];
                        using (var reader = selectCmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    Console.Write($"{reader.GetName(i)}: {reader.GetValue(i)} | ");
                                }
                                Console.WriteLine();
                            }
                        }
                        return;
                    }
                    else if (args.Length > 0)
                    {
                        var nonQueryCmd = conn.CreateCommand();
                        nonQueryCmd.CommandText = args[0];
                        int affected = nonQueryCmd.ExecuteNonQuery();
                        Console.WriteLine($"Command executed successfully. Rows affected: {affected}");
                        return;
                    }

                    var staffData = new[]
                    {
                        new { Id = "F1B8A079-077C-419F-A436-56D2F2312253", Name = "dalila", Email = "bentouaitamer6@gmail.com", Phone = "0797130571", SId = "EF08F81A-F297-4E98-BCB1-9EE6954A89B7" },
                        new { Id = "87DAA2A7-2FA7-43E7-87CB-E56F43B9248B", Name = "oumaima", Email = "bentouatitamer5@gmail.com", Phone = "0797130571", SId = "1F9DDB0B-387D-48C5-B0DE-859A3E4DE8F6" },
                        new { Id = "B568A4FC-E1AE-4A7B-90BE-02180E836A58", Name = "safa", Email = "bentouatitamer4@gmail.com", Phone = "0797130571", SId = "53F7451A-3B96-4D42-A1CF-A539FC4209ED" }
                    };

                    var tenantId = "A7A30B51-2AD8-4B9E-BA2E-B665EACBCE90";
                    var facilityId = "81FAABB1-F5FD-4B4F-B749-DA87AA501EE3";

                    // Step 1: Repair local phone numbers if truncated
                    foreach (var s in staffData)
                    {
                        var repairCmd = conn.CreateCommand();
                        repairCmd.CommandText = "UPDATE staff_members SET phone_number = @phone WHERE id = @id";
                        repairCmd.Parameters.AddWithValue("@phone", s.Phone);
                        repairCmd.Parameters.AddWithValue("@id", s.Id);
                        repairCmd.ExecuteNonQuery();
                    }

                    // Step 2: Clear any existing failed messages to avoid duplicates
                    var clearCmd = conn.CreateCommand();
                    clearCmd.CommandText = "DELETE FROM outbox_messages WHERE entity_type = 'StaffMember' AND entity_id IN ('f1b8a079-077c-419f-a436-56d2f2312253', '87daa2a7-2fa7-43e7-87cb-e56f43b9248b', 'b568a4fc-e1ae-4a7b-90be-02180e836a58') AND is_processed = 0";
                    clearCmd.ExecuteNonQuery();

                    // Step 3: Insert new outbox messages
                    foreach (var s in staffData)
                    {
                        var snapshot = new Dictionary<string, object>
                        {
                            ["Id"] = s.Id,
                            ["TenantId"] = tenantId,
                            ["FacilityId"] = facilityId,
                            ["FullName"] = s.Name,
                            ["Email"] = s.Email,
                            ["PhoneNumber"] = s.Phone,
                            ["Role"] = 7,
                            ["IsActive"] = true,
                            ["IsDeleted"] = false,
                            ["SupabaseUserId"] = s.SId,
                            ["AuthSyncStatus"] = "completed",
                            ["AllowedModules"] = "[\"Salon\"]",
                            ["CreatedAt"] = DateTime.UtcNow.ToString("o"),
                            ["UpdatedAt"] = DateTime.UtcNow.ToString("o")
                        };

                        string json = JsonSerializer.Serialize(snapshot);
                        string outboxId = Guid.NewGuid().ToString().ToLower();

                        var cmd = conn.CreateCommand();
                        cmd.CommandText = @"
                            INSERT INTO outbox_messages (id, tenant_id, facility_id, entity_type, entity_id, action, content_json, is_processed, error_count, is_conflict, is_deleted, is_synced, row_version, created_at)
                            VALUES (@id, @tid, @fid, 'StaffMember', @eid, 'Modified', @json, 0, 0, 0, 0, 0, @rv, datetime('now'));";
                        
                        cmd.Parameters.AddWithValue("@id", outboxId);
                        cmd.Parameters.AddWithValue("@tid", tenantId);
                        cmd.Parameters.AddWithValue("@fid", facilityId);
                        cmd.Parameters.AddWithValue("@eid", s.Id.ToLower()); 
                        cmd.Parameters.AddWithValue("@json", json);
                        cmd.Parameters.AddWithValue("@rv", new byte[] { 0 });

                        cmd.ExecuteNonQuery();
                        Console.WriteLine($"Triggered sync for {s.Name} ({s.Id})");
                    }
                    
                    Console.WriteLine("Repair and Sync Trigger completed successfully.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
