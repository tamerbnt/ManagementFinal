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
using Management.Domain.Models.Restaurant;
using MediatR;
using Management.Application.Notifications;

namespace Management.Infrastructure.Services.Sync
{
    public class RestaurantSyncStrategy : IFacilitySyncStrategy
    {
        private readonly Supabase.Client _supabase;
        private readonly ITenantService _tenantService;
        private readonly ILogger<RestaurantSyncStrategy> _logger;
        private readonly IMediator _mediator;

        public FacilityType FacilityType => FacilityType.Restaurant;

        public RestaurantSyncStrategy(
            Supabase.Client supabase,
            ITenantService tenantService,
            ILogger<RestaurantSyncStrategy> logger,
            IMediator mediator)
        {
            _supabase = supabase;
            _tenantService = tenantService;
            _logger = logger;
            _mediator = mediator;
        }

        public async Task PullSpecificDataAsync(AppDbContext context, DateTimeOffset lastSync, CancellationToken ct)
        {
            _logger.LogInformation("RestaurantSyncStrategy: Pulling Restaurant-specific data...");
            await PullRestaurantMenuItemsAsync(context, lastSync, ct);
            await PullRestaurantOrdersAsync(context, lastSync, ct);
        }

        public async Task<bool> HandleOutboxMessageAsync(Management.Domain.Models.OutboxMessage message, CancellationToken ct)
        {
            switch (message.EntityType)
            {
                case "RestaurantMenuItem":
                    return await SyncSnapshotAsync<SupabaseRestaurantMenuItem>(message, MapToSupabaseRestaurantMenuItem, ct);
                case "RestaurantOrder":
                    return await SyncSnapshotAsync<SupabaseRestaurantOrder>(message, MapToSupabaseRestaurantOrder, ct);
                default:
                    return false;
            }
        }

        private async Task PullRestaurantMenuItemsAsync(AppDbContext context, DateTimeOffset lastSync, CancellationToken ct)
        {
            try
            {
                var tenantId = _tenantService.GetTenantId();
                if (tenantId == null || tenantId == Guid.Empty) return;

                var remoteData = await _supabase.From<SupabaseRestaurantMenuItem>()
                    .Filter("tenant_id", Supabase.Postgrest.Constants.Operator.Equals, tenantId.ToString())
                    .Where(x => x.UpdatedAt > lastSync.UtcDateTime)
                    .Get();

                if (!remoteData.Models.Any()) return;

                foreach (var remote in remoteData.Models)
                {
                    var existing = await context.MenuItems.FindAsync(new object[] { remote.Id }, ct);
                    if (existing == null)
                    {
                        var newItem = new RestaurantMenuItem
                        {
                            Id = remote.Id,
                            TenantId = remote.TenantId,
                            FacilityId = remote.FacilityId,
                            Name = remote.Name ?? string.Empty,
                            Category = remote.Category ?? string.Empty,
                            Price = remote.Price,
                            ImagePath = remote.ImagePath ?? string.Empty,
                            IsAvailable = remote.IsAvailable,
                            Ingredients = !string.IsNullOrEmpty(remote.IngredientsJson) ? JsonSerializer.Deserialize<string[]>(remote.IngredientsJson) ?? Array.Empty<string>() : Array.Empty<string>(),
                            IsSynced = true
                        };
                        context.MenuItems.Add(newItem);
                    }
                    else
                    {
                        var localUpdateAt = existing.UpdatedAt ?? existing.CreatedAt;
                        if (remote.UpdatedAt < localUpdateAt)
                        {
                            _logger.LogInformation("[Sync Conflict] Menu item {Id}: Local ({Local}) is newer than Server ({Server}). Triggering UI resolution.", remote.Id, localUpdateAt, remote.UpdatedAt);
                            
                            await _mediator.Publish(new SyncConflictNotification(
                                existing.Id, 
                                "RestaurantMenuItem", 
                                localUpdateAt, 
                                remote.UpdatedAt, 
                                existing.FacilityId), ct);
                            
                            continue;
                        }

                        if (remote.UpdatedAt == localUpdateAt)
                        {
                            continue;
                        }

                        existing.Name = remote.Name ?? string.Empty;
                        existing.Category = remote.Category ?? string.Empty;
                        existing.Price = remote.Price;
                        existing.ImagePath = remote.ImagePath ?? string.Empty;
                        existing.IsAvailable = remote.IsAvailable;
                        existing.Ingredients = !string.IsNullOrEmpty(remote.IngredientsJson) ? JsonSerializer.Deserialize<string[]>(remote.IngredientsJson) ?? Array.Empty<string>() : Array.Empty<string>();
                        existing.IsSynced = true;
                    }
                }
                await context.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pulling restaurant menu items.");
            }
        }

        private async Task PullRestaurantOrdersAsync(AppDbContext context, DateTimeOffset lastSync, CancellationToken ct)
        {
            try
            {
                var tenantId = _tenantService.GetTenantId();
                if (tenantId == null || tenantId == Guid.Empty) return;

                var remoteData = await _supabase.From<SupabaseRestaurantOrder>()
                    .Filter("tenant_id", Supabase.Postgrest.Constants.Operator.Equals, tenantId.ToString())
                    .Where(x => x.UpdatedAt > lastSync.UtcDateTime)
                    .Get();

                if (!remoteData.Models.Any()) return;

                foreach (var remote in remoteData.Models)
                {
                    var existing = await context.RestaurantOrders.FindAsync(new object[] { remote.Id }, ct);
                    if (existing == null)
                    {
                        var newOrder = new RestaurantOrder
                        {
                            Id = remote.Id,
                            TenantId = remote.TenantId,
                            FacilityId = remote.FacilityId,
                            TableNumber = remote.TableNumber ?? string.Empty,
                            Subtotal = remote.TotalAmount,
                            Status = (OrderStatus)remote.Status,
                            IsSynced = true
                        };
                        context.RestaurantOrders.Add(newOrder);
                    }
                    else
                    {
                        var localUpdateAt = existing.UpdatedAt ?? existing.CreatedAt;
                        if (remote.UpdatedAt < localUpdateAt)
                        {
                            _logger.LogInformation("[Sync Conflict] Restaurant order {Id}: Local ({Local}) is newer than Server ({Server}). Triggering UI resolution.", remote.Id, localUpdateAt, remote.UpdatedAt);
                            
                            await _mediator.Publish(new SyncConflictNotification(
                                existing.Id, 
                                "RestaurantOrder", 
                                localUpdateAt, 
                                remote.UpdatedAt, 
                                existing.FacilityId), ct);
                            
                            continue;
                        }

                        if (remote.UpdatedAt == localUpdateAt)
                        {
                            continue;
                        }

                        existing.TableNumber = remote.TableNumber ?? string.Empty;
                        existing.Subtotal = remote.TotalAmount;
                        existing.Status = (OrderStatus)remote.Status;
                        existing.IsSynced = true;
                    }
                }
                await context.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pulling restaurant orders.");
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
            var supabaseModel = mapper(snapshot);

            var result = await _supabase.From<TSupabaseModel>().Upsert(supabaseModel, cancellationToken: ct);
            return result.ResponseMessage.IsSuccessStatusCode;
        }

        private SupabaseRestaurantMenuItem MapToSupabaseRestaurantMenuItem(Dictionary<string, JsonElement> snapshot)
        {
            return new SupabaseRestaurantMenuItem
            {
                Id = GetVal<Guid>(snapshot, "Id"),
                TenantId = GetVal<Guid>(snapshot, "TenantId"),
                FacilityId = GetVal<Guid>(snapshot, "FacilityId"),
                Name = GetVal<string>(snapshot, "Name"),
                Category = GetVal<string>(snapshot, "Category"),
                Price = GetVal<decimal>(snapshot, "Price"),
                ImagePath = GetVal<string>(snapshot, "ImagePath"),
                IsAvailable = GetVal<bool>(snapshot, "IsAvailable"),
                IngredientsJson = GetVal<string>(snapshot, "IngredientsJson"),
                CreatedAt = GetVal<DateTime>(snapshot, "CreatedAt"),
                UpdatedAt = GetVal<DateTime>(snapshot, "UpdatedAt")
            };
        }

        private SupabaseRestaurantOrder MapToSupabaseRestaurantOrder(Dictionary<string, JsonElement> snapshot)
        {
            return new SupabaseRestaurantOrder
            {
                Id = GetVal<Guid>(snapshot, "Id"),
                TenantId = GetVal<Guid>(snapshot, "TenantId"),
                FacilityId = GetVal<Guid>(snapshot, "FacilityId"),
                TableNumber = GetVal<string>(snapshot, "TableNumber"),
                TotalAmount = GetVal<decimal>(snapshot, "TotalAmount"),
                Status = (int)GetVal<int>(snapshot, "Status"),
                CreatedAt = GetVal<DateTime>(snapshot, "CreatedAt"),
                UpdatedAt = GetVal<DateTime>(snapshot, "UpdatedAt")
            };
        }

        private T? GetVal<T>(Dictionary<string, JsonElement> dict, string key)
        {
            if (!dict.TryGetValue(key, out var element) || element.ValueKind == JsonValueKind.Null)
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

