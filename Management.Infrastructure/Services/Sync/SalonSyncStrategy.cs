using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Management.Domain.Enums;
using Management.Domain.Services;
using Management.Infrastructure.Data;
using Management.Infrastructure.Data.Models;
using Management.Infrastructure.Integrations.Supabase.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Supabase.Postgrest.Models;
using Management.Domain.Models;
using Management.Domain.Models.Salon;

namespace Management.Infrastructure.Services.Sync
{
    public class SalonSyncStrategy : IFacilitySyncStrategy
    {
        private readonly Supabase.Client _supabase;
        private readonly ITenantService _tenantService;
        private readonly IFacilityContextService _facilityContext;
        private readonly ILogger<SalonSyncStrategy> _logger;

        public FacilityType FacilityType => FacilityType.Salon;

        public SalonSyncStrategy(
            Supabase.Client supabase,
            ITenantService tenantService,
            IFacilityContextService facilityContext,
            ILogger<SalonSyncStrategy> logger)
        {
            _supabase = supabase;
            _tenantService = tenantService;
            _facilityContext = facilityContext;
            _logger = logger;
        }

        public async Task PullSpecificDataAsync(AppDbContext context, DateTimeOffset lastSync, CancellationToken ct)
        {
            _logger.LogInformation("SalonSyncStrategy: Pulling Salon-specific data...");
            // PullSalonServicesAsync is now local-only and handled by seeding.
            await PullAppointmentsAsync(context, lastSync, ct);
        }

        public async Task<bool> HandleOutboxMessageAsync(Management.Domain.Models.OutboxMessage message, CancellationToken ct)
        {
            switch (message.EntityType)
            {
                // SalonService is local-only
                case "Appointment":
                    return await SyncSnapshotAsync<SupabaseAppointment>(message, MapToSupabaseAppointment, ct);
                default:
                    return false;
            }
        }


        private async Task PullAppointmentsAsync(AppDbContext context, DateTimeOffset lastSync, CancellationToken ct)
        {
            try
            {
                var tenantId = _tenantService.GetTenantId();
                if (tenantId == null || tenantId == Guid.Empty) return;

                var remoteData = await _supabase.From<SupabaseAppointment>()
                    .Filter("tenant_id", Supabase.Postgrest.Constants.Operator.Equals, tenantId.ToString())
                    .Where(x => x.UpdatedAt > lastSync.UtcDateTime)
                    .Get();

                if (!remoteData.Models.Any()) return;

                foreach (var remote in remoteData.Models)
                {
                    var existing = await context.Appointments.FindAsync(new object[] { remote.Id }, ct);
                    if (existing == null)
                    {
                        var newAppointment = new Appointment
                        {
                            Id = remote.Id,
                            TenantId = remote.TenantId,
                            FacilityId = remote.FacilityId,
                            ClientId = remote.ClientId,
                            ClientName = remote.ClientName ?? string.Empty,
                            StaffId = remote.StaffId,
                            StaffName = remote.StaffName ?? string.Empty,
                            ServiceId = remote.ServiceId,
                            ServiceName = remote.ServiceName ?? string.Empty,
                            StartTime = remote.StartTime,
                            EndTime = remote.EndTime,
                            Status = (AppointmentStatus)remote.Status,
                            Price = remote.Price,
                            Notes = remote.Notes ?? string.Empty,
                            IsSynced = true
                        };
                        context.Appointments.Add(newAppointment);
                    }
                    else
                    {
                        // FIX: Only update if remote is actually newer than local
                        if (remote.UpdatedAt <= (existing.UpdatedAt ?? existing.CreatedAt))
                        {
                            _logger.LogDebug("[Sync] Skipping appointment update for {Id}: Local is newer or same.", remote.Id);
                            continue;
                        }

                        existing.ClientId = remote.ClientId;
                        existing.ClientName = remote.ClientName ?? string.Empty;
                        existing.StaffId = remote.StaffId;
                        existing.StaffName = remote.StaffName ?? string.Empty;
                        existing.ServiceId = remote.ServiceId;
                        existing.ServiceName = remote.ServiceName ?? string.Empty;
                        existing.StartTime = remote.StartTime;
                        existing.EndTime = remote.EndTime;
                        existing.Status = (AppointmentStatus)remote.Status;
                        existing.Price = remote.Price;
                        existing.Notes = remote.Notes ?? string.Empty;
                        existing.IsSynced = true;
                    }
                }
                await context.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pulling salon appointments.");
            }
        }


        private async Task<bool> SyncSnapshotAsync<TSupabaseModel>(
            Management.Domain.Models.OutboxMessage message,
            Func<Dictionary<string, JsonElement>, TSupabaseModel> mapper,
            CancellationToken ct)
            where TSupabaseModel : BaseModel, new()
        {
            if (message.Action == "Deleted")
            {
                await _supabase.From<TSupabaseModel>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, message.EntityId)
                    .Delete(cancellationToken: ct);
                return true;
            }

            var snapshot = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(message.ContentJson);
            if (snapshot == null) return false;
            
            var supabaseModel = mapper(snapshot);

            var result = await _supabase.From<TSupabaseModel>().Upsert(supabaseModel, cancellationToken: ct);
            return result.ResponseMessage.IsSuccessStatusCode;
        }

        private SupabaseAppointment MapToSupabaseAppointment(Dictionary<string, JsonElement> snapshot)
        {
            return new SupabaseAppointment
            {
                Id = GetVal<Guid>(snapshot, "Id"),
                TenantId = GetVal<Guid>(snapshot, "TenantId"),
                FacilityId = GetVal<Guid>(snapshot, "FacilityId"),
                ClientId = GetVal<Guid>(snapshot, "ClientId"),
                ClientName = GetVal<string>(snapshot, "ClientName"),
                StaffId = GetVal<Guid>(snapshot, "StaffId"),
                StaffName = GetVal<string>(snapshot, "StaffName"),
                ServiceId = GetVal<Guid>(snapshot, "ServiceId"),
                ServiceName = GetVal<string>(snapshot, "ServiceName"),
                StartTime = GetVal<DateTime>(snapshot, "StartTime"),
                EndTime = GetVal<DateTime>(snapshot, "EndTime"),
                Status = (int)GetVal<int>(snapshot, "Status"),
                Price = GetVal<decimal>(snapshot, "Price"),
                Notes = GetVal<string>(snapshot, "Notes"),
                CreatedAt = GetVal<DateTime>(snapshot, "CreatedAt"),
                UpdatedAt = GetVal<DateTime>(snapshot, "UpdatedAt")
            };
        }

        private T? GetVal<T>(Dictionary<string, JsonElement> dict, string key)
        {
            // Case-insensitive lookup to handle potential property name mismatches between domain/dto
            var actualKey = dict.Keys.FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase)) ?? key;

            if (!dict.TryGetValue(actualKey, out var element) || element.ValueKind == JsonValueKind.Null)
                return default;

            try
            {
                if (typeof(T) == typeof(Guid)) return (T)(object)element.GetGuid();
                if (typeof(T) == typeof(string)) return (T)(object)element.GetString()!;
                if (typeof(T) == typeof(decimal)) return (T)(object)element.GetDecimal();
                if (typeof(T) == typeof(int)) return (T)(object)element.GetInt32();
                if (typeof(T) == typeof(bool)) return (T)(object)element.GetBoolean();
                if (typeof(T) == typeof(DateTime)) return (T)(object)element.GetDateTime();
                
                return JsonSerializer.Deserialize<T>(element.GetRawText());
            }
            catch
            {
                return default;
            }
        }
    }
}

