using Management.Application.Interfaces;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Services;
using Management.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Infrastructure.Data.Interceptors
{
    public class OutboxInterceptor : SaveChangesInterceptor
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<OutboxInterceptor> _logger;

        public OutboxInterceptor(
            ICurrentUserService currentUserService,
            ILogger<OutboxInterceptor> logger)
        {
            _currentUserService = currentUserService;
            _logger = logger;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context is not AppDbContext context) return base.SavingChangesAsync(eventData, result, cancellationToken);

            var outboxMessages = new List<OutboxMessage>();
            var currentUserId = _currentUserService.UserId;

            foreach (var entry in context.ChangeTracker.Entries())
            {
                var isJoinEntity = entry.Metadata.IsPropertyBag;
                var syncable = entry.Entity as ISyncable;

                // --- LOCAL-FIRST REFACTOR: SYNC EXCLUSIONS ---
                // We only sync Identity (Staff), External Ingress (Registrations), and Infrastructure Config.
                // Bulk business data (Members, Sales, Products, Logs) is LOCAL-ONLY.
                
                bool shouldSync = entry.Entity switch
                {
                    StaffMember => true,
                    Registration => true,
                    GymSettings => true,
                    Facility => true,
                    FacilitySchedule => true,
                    Turnstile => true,
                    _ => false
                };

                // Additional check for Join Entities (like FacilityPlans) - these are usually related to excluded entities
                if (isJoinEntity) shouldSync = false; 

                if (!shouldSync) continue;
                if (entry.Entity is OutboxMessage) continue;

                if (syncable != null)
                {
                    if (entry.State == EntityState.Added || entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
                    {
                        // SYNC LOOP PREVENTION
                        if (syncable.IsSynced) continue;
                        syncable.IsSynced = false;

                        string entityType = entry.Entity.GetType().Name;
                        string entityId = string.Empty;

                        try { entityId = entry.Property("Id").CurrentValue?.ToString() ?? string.Empty; } catch { }

                        // CAPTURE SNAPSHOT
                        var snapshot = new Dictionary<string, object?>();
                        foreach (var prop in entry.Properties)
                        {
                            var value = prop.CurrentValue;
                            var converter = prop.Metadata.GetValueConverter();
                            if (converter != null) value = converter.ConvertToProvider(value);
                            snapshot[prop.Metadata.Name] = value;
                        }

                        foreach (var navigation in entry.Navigations)
                        {
                            if (navigation.Metadata.TargetEntityType.IsOwned())
                            {
                                var ownedEntry = navigation.CurrentValue != null 
                                    ? entry.Reference(navigation.Metadata.Name).TargetEntry 
                                    : null;

                                if (ownedEntry != null)
                                {
                                    string prefix = navigation.Metadata.Name;
                                    foreach (var prop in ownedEntry.Properties)
                                    {
                                        snapshot[$"{prefix}_{prop.Metadata.Name}"] = prop.CurrentValue;
                                    }
                                }
                            }
                        }

                        // FETCH CONTEXT IDs
                        var messageTenantId = GetPropertyGuid(entry.Entity, "TenantId");
                        var messageFacilityId = GetPropertyGuid(entry.Entity, "FacilityId");

                        var message = new OutboxMessage
                        {
                            Id = AppDbContext.GenerateTimeOrderedGuid(),
                            TenantId = messageTenantId,
                            FacilityId = messageFacilityId,
                            EntityType = entityType,
                            EntityId = entityId,
                            Action = entry.State.ToString(),
                            ContentJson = entry.State == EntityState.Deleted 
                                ? "{}" 
                                : JsonSerializer.Serialize(snapshot, new JsonSerializerOptions 
                                  { 
                                      ReferenceHandler = ReferenceHandler.IgnoreCycles,
                                      WriteIndented = false
                                  }),
                            CreatedBy = currentUserId,
                            IsProcessed = false
                        };

                        outboxMessages.Add(message);
                        _logger.LogInformation("[OUTBOX INTERCEPTOR] Generated {Action} for {Type} ({Id})", message.Action, message.EntityType, message.EntityId);
                    }
                }
            }

            if (outboxMessages.Any())
            {
                context.OutboxMessages.AddRange(outboxMessages);
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private Guid GetPropertyGuid(object entity, string propName)
        {
            try 
            {
                var prop = entity.GetType().GetProperty(propName);
                if (prop != null)
                {
                    var val = prop.GetValue(entity);
                    if (val is Guid g) return g;
                }
            }
            catch { }
            return Guid.Empty;
        }
    }
}
