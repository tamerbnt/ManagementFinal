using Microsoft.EntityFrameworkCore;
using Management.Application.Services;
using Management.Domain.Models;
using Management.Domain.Models.Restaurant;
using Management.Domain.Models.Salon;
using Management.Domain.Models.Resilience;
using Management.Domain.Common;
using Management.Domain.ValueObjects;
using Management.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MediatR;
using Management.Domain.Services;
using Management.Domain.Primitives;
using Management.Domain.Interfaces;
using ITenantEntity = Management.Domain.Primitives.ITenantEntity;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using Management.Infrastructure.Integrations.Supabase.Models;

namespace Management.Infrastructure.Data
{
    /// <summary>
    /// Main database context for the Management application.
    /// </summary>
    public class AppDbContext : DbContext
    {
        private readonly ICurrentUserService _currentUserService;
        // private readonly IPublisher _publisher; (Removed to prevent DI loop)
        private readonly ITenantService _tenantService;
        private readonly IFacilityContextService _facilityContext;
        private readonly Microsoft.Extensions.Logging.ILogger<AppDbContext> _logger;
        private readonly Management.Infrastructure.Data.Interceptors.ShadowPropertyInterceptor? _shadowInterceptor;
        private readonly Management.Infrastructure.Data.Interceptors.OutboxInterceptor? _outboxInterceptor;
        private readonly AuditableEntityInterceptor? _auditableInterceptor;
        private readonly ISecureStorageService? _secureStorage;
        private string _secretKey = "GymProductionKey2026!"; // Default fallback

        public AppDbContext(
            DbContextOptions<AppDbContext> options,
            ICurrentUserService currentUserService,
            ITenantService tenantService,
            IFacilityContextService facilityContext,
            Microsoft.Extensions.Logging.ILogger<AppDbContext> logger,
            Management.Infrastructure.Data.Interceptors.ShadowPropertyInterceptor? shadowInterceptor = null,
            Management.Infrastructure.Data.Interceptors.OutboxInterceptor? outboxInterceptor = null,
            AuditableEntityInterceptor? auditableInterceptor = null,
            ISecureStorageService? secureStorage = null) : base(options)
        {
            _currentUserService = currentUserService;
            _tenantService = tenantService;
            _facilityContext = facilityContext;
            _logger = logger;
            _shadowInterceptor = shadowInterceptor;
            _outboxInterceptor = outboxInterceptor;
            _auditableInterceptor = auditableInterceptor;
            _secureStorage = secureStorage;
            // DO NOT call any async method safely here. Handled by SecretKey lazy property.
        }

        private string SecretKey
        {
            get
            {
                if (_secureStorage != null && _secretKey == "GymProductionKey2026!")
                {
                    // This runs on background thread via repository calls - safe
                    var storedKey = _secureStorage.Get("DatabaseEncryptionKey");
                    if (!string.IsNullOrEmpty(storedKey))
                        _secretKey = storedKey;
                }
                return _secretKey;
            }
        }


        /// <summary>
        /// Ensures the database is optimized (WAL mode) and performs essential runtime data healing.
        /// Replaces legacy iterative schema checks with a streamlined startup routine.
        /// </summary>
        public async Task EnsureDatabaseSchemaAsync(CancellationToken ct = default)
        {
            if (Database.IsSqlite())
            {
                await Database.OpenConnectionAsync(ct);
                await Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);

                var schemaStopwatch = System.Diagnostics.Stopwatch.StartNew();
                _logger.LogInformation("Optimizing SQLite database (WAL mode)...");
                
                await Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);", ct);

                // EMERGENCY SYNC RESET: Reset error count for stalled messages
                try { await Database.ExecuteSqlRawAsync("UPDATE outbox_messages SET error_count = 0 WHERE is_processed = 0;", ct); } catch { }
                
                _logger.LogInformation("Database optimization completed in {Elapsed}ms", schemaStopwatch.ElapsedMilliseconds);
            }
            else if (Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                // PostgreSQL essential runtime data healing
                try { await Database.ExecuteSqlRawAsync("UPDATE staff_members SET permissions = '{}' WHERE permissions IS NULL;", ct); } catch { }
                try { await Database.ExecuteSqlRawAsync("UPDATE staff_members SET allowed_modules = '[]' WHERE allowed_modules IS NULL;", ct); } catch { }
                
                // Ensure legacy 'price' columns are not NULL (to satisfy EF IsRequired shadow properties)
                try { await Database.ExecuteSqlRawAsync("UPDATE membership_plans SET price = 0 WHERE price IS NULL;", ct); } catch { }
                try { await Database.ExecuteSqlRawAsync("UPDATE products SET price = 0 WHERE price IS NULL;", ct); } catch { }

                // Task 4: Fix Salon Missing Price Column in PostgreSQL (Cloud Fallback)
                // Use ALTER TABLE to safely add the column if missing on the remote instance
                try { await Database.ExecuteSqlRawAsync("ALTER TABLE appointments ADD COLUMN IF NOT EXISTS price numeric NOT NULL DEFAULT 0;", ct); } catch { }
            }
        }

        // Task 1: Database Safety - UUID v7 (Time-Ordered)
        public static Guid GenerateTimeOrderedGuid()
        {
            return UuidV7.Create();
        }

        // Internal class for V7 generation to keep it clean
        private static class UuidV7 
        {
            public static Guid Create() 
            {
                var guidBytes = Guid.NewGuid().ToByteArray();
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                
                // Big Endian Timestamp
                guidBytes[0] = (byte)((timestamp >> 40) & 0xFF);
                guidBytes[1] = (byte)((timestamp >> 32) & 0xFF);
                guidBytes[2] = (byte)((timestamp >> 24) & 0xFF);
                guidBytes[3] = (byte)((timestamp >> 16) & 0xFF);
                guidBytes[4] = (byte)((timestamp >> 8) & 0xFF);
                guidBytes[5] = (byte)((timestamp) & 0xFF);
                
                // Version 7
                guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x70);
                
                // Variant 1
                guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
                
                return new Guid(guidBytes);
            }
        }

        public DbSet<Member> Members { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<TableModel> RestaurantTables { get; set; }
        public DbSet<RestaurantOrder> RestaurantOrders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<RestaurantMenuItem> MenuItems { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<MembershipPlan> MembershipPlans { get; set; }
        public DbSet<StaffMember> StaffMembers { get; set; }
        public DbSet<Registration> Registrations { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleItem> SaleItems { get; set; }
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<PayrollEntry> PayrollEntries { get; set; }
        public DbSet<AccessEvent> AccessEvents { get; set; }
        public DbSet<Turnstile> Turnstiles { get; set; }
        public DbSet<FacilityZone> FacilityZones { get; set; }
        public DbSet<IntegrationConfig> IntegrationConfigs { get; set; }
        public DbSet<GymSettings> GymSettings { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<SalonService> SalonServices { get; set; }
        public DbSet<Facility> Facilities { get; set; }
        public DbSet<FacilitySchedule> FacilitySchedules { get; set; }
        public DbSet<OutboxMessage> OutboxMessages { get; set; }
        public DbSet<OfflineAction> OfflineActions { get; set; }

        private static string UnescapeOverSerializedJson(string val)
        {
            if (string.IsNullOrEmpty(val)) return "{}";
            return val.StartsWith("\"") && val.EndsWith("\"") && val.Length > 1 
                ? val.Substring(1, val.Length - 2).Replace("\\\"", "\"") 
                : val;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Ignore Supabase models - they're only for cloud sync, not local storage
            modelBuilder.Ignore<SupabaseProfile>();
            modelBuilder.Ignore<SupabaseTenant>();
            modelBuilder.Ignore<SupabaseFacility>();
            modelBuilder.Ignore<SupabaseMember>();
            modelBuilder.Ignore<SupabaseDevice>();
            modelBuilder.Ignore<SupabaseLicense>();
            modelBuilder.Ignore<SupabaseAccessEvent>();
            modelBuilder.Ignore<SupabaseSaleItem>();
            modelBuilder.Ignore<SupabaseStaffMember>();
            modelBuilder.Ignore<SupabaseMembershipPlan>();
            modelBuilder.Ignore<SupabaseFacilitySchedule>();
            modelBuilder.Ignore<SupabaseMembershipPlanFacility>();
            modelBuilder.Ignore<SupabaseMoney>();
            modelBuilder.Ignore<SupabaseRegistration>();
            modelBuilder.Ignore<SupabaseGymSettings>();
            modelBuilder.Ignore<SupabaseTurnstile>();
            
            // (Loop moved to end of method)

            // MembershipPlan configuration
            modelBuilder.Entity<MembershipPlan>(entity =>
            {
                entity.OwnsOne(e => e.Price, p => 
                {
                    p.Property(m => m.Amount).HasColumnName("price_amount");
                    p.Property(m => m.Currency).HasColumnName("price_currency");
                });

                // Shadow property to satisfy legacy NOT NULL constraint on 'price' column if it exists
                // We use ValueGeneratedNever to ensure EF Core includes it in the INSERT statement
                entity.Property<decimal>("price")
                    .HasColumnName("price")
                    .ValueGeneratedNever()
                    .IsRequired();

                entity.HasMany(p => p.AccessibleFacilities)
                    .WithMany(f => f.AccessiblePlans)
                    .UsingEntity(j => j.ToTable("membership_plan_facilities"));

                entity.Property(e => e.IsWalkIn).HasColumnName("is_walk_in");
            });


            // StaffMember configuration
            modelBuilder.Entity<StaffMember>(entity =>
            {
                entity.ToTable("staff_members");

                // Always set jsonb for these columns (handled gracefully by providers)
                entity.Property(e => e.Permissions).HasColumnType("jsonb");
                entity.Property(e => e.AllowedModules).HasColumnType("jsonb");

                entity.Property(e => e.Permissions)
                    .HasColumnName("permissions")
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                        v => JsonSerializer.Deserialize<Dictionary<string, bool>>(UnescapeOverSerializedJson(v), (JsonSerializerOptions)null) 
                             ?? new Dictionary<string, bool>())
                    .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<Dictionary<string, bool>>(
                        (c1, c2) => c1.SequenceEqual(c2),
                        c => c.Aggregate(0, (a, val) => HashCode.Combine(a, val.GetHashCode())),
                        c => c.ToDictionary(entry => entry.Key, entry => entry.Value)));

                entity.Property(e => e.AllowedModules)
                    .HasColumnName("allowed_modules")
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                        v => JsonSerializer.Deserialize<List<string>>(UnescapeOverSerializedJson(v), (JsonSerializerOptions)null) ?? new List<string>())
                    .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
                        (c1, c2) => c1.SequenceEqual(c2),
                        c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                        c => c.ToList()));

                entity.Property(e => e.SupabaseUserId)
                    .HasColumnName("supabase_user_id")
                    .HasConversion(
                        v => !string.IsNullOrEmpty(v) ? Guid.Parse(v) : (Guid?)null,
                        v => v.HasValue ? v.Value.ToString() : null);
            });

            // Member configuration
            modelBuilder.Entity<Member>(entity =>
            {
                entity.ToTable("members");
                entity.HasKey(e => e.Id);

                // Removed OwnsOne for Email, PhoneNumber, and EmergencyContactPhone to avoid conflict with ValueConverter
                // These are now handled by the global ValueConverter and snake_case naming logic.
                
                // Ensure other properties are mapped correctly even if they have weird names
                entity.Property(e => e.FullName).HasColumnName("full_name");
                entity.Property(e => e.CardId).HasColumnName("card_id");
                entity.HasIndex(e => e.CardId).IsUnique().HasFilter("[card_id] IS NOT NULL").HasDatabaseName("idx_member_card_id_unique");
                entity.HasIndex(e => e.Email).IsUnique().HasFilter("[email] IS NOT NULL").HasDatabaseName("idx_member_email_unique");
                entity.HasIndex(e => e.PhoneNumber).IsUnique().HasFilter("[phone_number] IS NOT NULL").HasDatabaseName("idx_member_phone_unique");
                entity.Property(e => e.ProfileImageUrl).HasColumnName("profile_image_url");
                entity.Property(e => e.MembershipPlanId).HasColumnName("membership_plan_id");
                entity.Property(e => e.SegmentDataJson).HasColumnName("segment_data_json");
                
                // PERFORMANCE INDICES for filtering
                entity.HasIndex(e => e.Status).HasDatabaseName("idx_member_status");
                entity.HasIndex(e => e.Gender).HasDatabaseName("idx_member_gender");
                entity.HasIndex(e => e.StartDate).HasDatabaseName("idx_member_start_date");
                entity.HasIndex(e => e.ExpirationDate).HasDatabaseName("idx_member_expiration_date");
                entity.HasIndex(m => new { m.FacilityId, m.Status, m.ExpirationDate }).HasDatabaseName("idx_member_performance_composite");

                entity.Ignore(e => e.Metadata);
            });

            modelBuilder.Entity<Appointment>(entity =>
            {
                entity.ToTable("appointments");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Price).HasColumnName("price");
                
                entity.HasIndex(a => new { a.FacilityId, a.StartTime, a.IsDeleted })
                      .HasDatabaseName("idx_appointment_search");
                entity.HasIndex(a => new { a.FacilityId, a.StartTime, a.Status })
                      .HasDatabaseName("idx_appointment_performance_composite");
            });

            modelBuilder.Entity<Registration>(entity =>
            {
                entity.ToTable("registrations");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.InterestPayloadJson).HasColumnName("interest_payload_json");
                entity.Ignore(e => e.Metadata);
            });

            // Product configuration
            modelBuilder.Entity<Product>(entity =>
            {
                entity.ToTable("products");
                entity.HasKey(e => e.Id);

                entity.OwnsOne(e => e.Price, b =>
                {
                    b.Property(p => p.Amount).HasColumnName("price_amount");
                    b.Property(p => p.Currency).HasColumnName("price_currency");
                });

                // Shadow property to satisfy legacy NOT NULL constraint on 'price' column if it exists
                // We use ValueGeneratedNever to ensure EF Core includes it in the INSERT statement
                entity.Property<decimal>("price")
                    .HasColumnName("price")
                    .ValueGeneratedNever()
                    .IsRequired();

                entity.OwnsOne(e => e.Cost, b =>
                {
                    b.Property(p => p.Amount).HasColumnName("cost_amount");
                    b.Property(p => p.Currency).HasColumnName("cost_currency");
                });

                entity.ToTable(t => t.HasCheckConstraint("CK_Product_StockNonNegative", "stock_quantity >= 0"));
            });

            // Restaurant Order configuration
            modelBuilder.Entity<RestaurantOrder>(entity =>
            {
                entity.ToTable("restaurant_orders");
                entity.HasKey(e => e.Id);
                entity.HasMany(e => e.Items)
                    .WithOne()
                    .HasForeignKey(e => e.RestaurantOrderId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Order Item configuration
            modelBuilder.Entity<OrderItem>(entity =>
            {
                entity.ToTable("order_items");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.RestaurantOrderId).HasColumnName("restaurant_order_id");
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.Price).HasColumnName("price");
                entity.Property(e => e.Quantity).HasColumnName("quantity");
            });

            // Restaurant Menu Item configuration
            modelBuilder.Entity<RestaurantMenuItem>(entity =>
            {
                entity.ToTable("restaurant_menu_items");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Ingredients)
                    .HasColumnName("ingredients_json")
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                        v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions)null) ?? Array.Empty<string>())
                    .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<string[]>(
                        (c1, c2) => c1.SequenceEqual(c2),
                        c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                        c => c.ToArray()));
                
                entity.Property(e => e.Name).HasColumnName("name");
                entity.Property(e => e.Category).HasColumnName("category");
                entity.Property(e => e.Price).HasColumnName("price");
                entity.Property(e => e.ImagePath).HasColumnName("image_path");
                entity.Property(e => e.IsAvailable).HasColumnName("is_available");
            });

            // Value Object: Money (Owned Types)
            modelBuilder.Entity<Sale>(entity =>
            {
                entity.OwnsOne(s => s.SubtotalAmount);
                entity.OwnsOne(s => s.TaxAmount);
                entity.OwnsOne(s => s.TotalAmount);
                entity.Property(e => e.Category).HasColumnName("category");
                entity.Property(e => e.CapturedLabel).HasColumnName("captured_label");
                
                // FIX: Map private _items collection
                entity.HasMany(s => s.Items)
                    .WithOne()
                    .HasForeignKey(si => si.SaleId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Navigation(s => s.Items)
                    .HasField("_items")
                    .UsePropertyAccessMode(PropertyAccessMode.Field);

                entity.HasIndex(s => new { s.FacilityId, s.CreatedAt }).HasDatabaseName("idx_sale_performance_composite");
            });

            modelBuilder.Entity<SaleItem>(entity =>
            {
                entity.OwnsOne(si => si.UnitPriceSnapshot, p =>
                {
                    p.Property(m => m.Amount).HasColumnName("price_snapshot");
                    p.Property(m => m.Currency).HasColumnName("price_snapshot_currency");
                });
            });

            modelBuilder.Entity<PayrollEntry>(entity =>
            {
                entity.ToTable("payroll_entries");
                entity.Property(e => e.StaffId).HasColumnName("staff_member_id");
                entity.Property(e => e.BaseSalary).HasColumnName("base_salary");
                entity.Property(e => e.AbsenceCount).HasColumnName("absence_count");
                entity.Property(e => e.AbsenceDeduction).HasColumnName("absence_deduction");

                // Map base Entity properties to snake_case columns
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
                entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
                entity.Property(e => e.IsSynced).HasColumnName("is_synced");
                entity.Property(e => e.RowVersion).HasColumnName("row_version");
                entity.Property(e => e.TenantId).HasColumnName("tenant_id");
                entity.Property(e => e.FacilityId).HasColumnName("facility_id");

                // Legacy 'is_paid' column is NOT NULL but our model is computed.
                // We map a shadow property to write to it.
                entity.Property<bool>("IsPaidShadow")
                      .HasColumnName("is_paid");

                entity.OwnsOne(pe => pe.Amount, p =>
                {
                    p.Property(m => m.Amount).HasColumnName("amount");
                    p.Property(m => m.Currency).HasColumnName("amount_currency");
                });

                entity.OwnsOne(pe => pe.PaidAmount, p =>
                {
                    p.Property(m => m.Amount).HasColumnName("paid_amount");
                    p.Property(m => m.Currency).HasColumnName("paid_amount_currency");
                });

                entity.HasOne<StaffMember>()
                    .WithMany()
                    .HasForeignKey(e => e.StaffId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.StaffId).HasDatabaseName("idx_payroll_staff_id");
            });

            modelBuilder.Entity<OutboxMessage>(entity =>
            {
                entity.HasIndex(m => new { m.IsProcessed, m.CreatedAt })
                    .HasDatabaseName("idx_outbox_unprocessed_created");
            });

            modelBuilder.Entity<AccessEvent>(entity =>
            {
                entity.HasIndex(ae => new { ae.FacilityId, ae.Timestamp }).HasDatabaseName("idx_access_event_performance_composite");
            });

            modelBuilder.Entity<RestaurantOrder>(entity =>
            {
                entity.HasIndex(o => new { o.FacilityId, o.CreatedAt, o.Status }).HasDatabaseName("idx_order_performance_composite");
            });

            modelBuilder.Entity<StaffMember>(entity =>
            {
                entity.HasIndex(s => new { s.FacilityId, s.TenantId }).HasDatabaseName("idx_staff_performance_composite");
            });

            // --- GLOBAL QUERY FILTERS (Multi-Tenancy) ---
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var type = entityType.ClrType;

                bool isTenantEntity = typeof(ITenantEntity).IsAssignableFrom(type);
                bool isFacilityEntity = typeof(IFacilityEntity).IsAssignableFrom(type);

                if (isTenantEntity && isFacilityEntity)
                {
                    var method = typeof(AppDbContext)
                        .GetMethod(nameof(ApplyCompositeFilter), BindingFlags.NonPublic | BindingFlags.Instance)
                        ?.MakeGenericMethod(type);
                    method?.Invoke(this, new object[] { modelBuilder });
                }
                else if (isTenantEntity)
                {
                    var method = typeof(AppDbContext)
                        .GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)
                        ?.MakeGenericMethod(type);
                    method?.Invoke(this, new object[] { modelBuilder });
                }
                else if (isFacilityEntity)
                {
                    var method = typeof(AppDbContext)
                        .GetMethod(nameof(ApplyFacilityFilter), BindingFlags.NonPublic | BindingFlags.Instance)
                        ?.MakeGenericMethod(type);
                    method?.Invoke(this, new object[] { modelBuilder });
                }
            }

            // --- SNAKE_CASE NAMING CONVENTION (Final Pass) ---
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                // --- Performance Optimization: Indexing Global Filter Columns ---
                var clrType = entity.ClrType;
                if (typeof(ITenantEntity).IsAssignableFrom(clrType))
                {
                    var prop = entity.FindProperty("TenantId");
                    if (prop != null) entity.AddIndex(prop);
                }
                if (typeof(IFacilityEntity).IsAssignableFrom(clrType))
                {
                    var prop = entity.FindProperty("FacilityId");
                    if (prop != null && !entity.GetIndexes().Any(i => i.Properties.Count == 1 && i.Properties[0].Name == "FacilityId"))
                    {
                        entity.AddIndex(prop);
                    }
                }
                if (typeof(Management.Domain.Interfaces.ISyncable).IsAssignableFrom(clrType))
                {
                    var prop = entity.FindProperty("IsSynced");
                    if (prop != null && !entity.GetIndexes().Any(i => i.Properties.Count == 1 && i.Properties[0].Name == "IsSynced"))
                    {
                        entity.AddIndex(prop);
                    }
                }
                var deletedProp = entity.FindProperty("IsDeleted");
                if (deletedProp != null && !entity.GetIndexes().Any(i => i.Properties.Count == 1 && i.Properties[0].Name == "IsDeleted"))
                {
                    entity.AddIndex(deletedProp);
                }

                // Table names
                var tableName = entity.GetTableName();
                if (tableName != null && !tableName.StartsWith("aspnet_")) // Avoid touching system tables
                {
                    entity.SetTableName(ToSnakeCase(tableName));
                }

                // Column names
                foreach (var property in entity.GetProperties())
                {
                    // EF Core Owned Types Fix: Shadow primary keys of owned types MUST map to the 
                    // principal's PK column (usually "id") for table sharing to work in Postgres.
                    if (entity.IsOwned() && property.IsPrimaryKey())
                    {
                        property.SetColumnName("id");
                        continue;
                    }

                    // For all other properties, we want to snake_case the name.
                    // Important: For owned types, 'property.Name' is just the property name (e.g. "Amount").
                    // We need the full prefixed name (e.g. "Price_Amount") to map to "price_amount".
                    // property.GetColumnName() returns the convention-based name including prefixes.
                    var storeObject = StoreObjectIdentifier.Table(tableName, entity.GetSchema());
                    var currentName = property.GetColumnName(storeObject) ?? property.Name;
                    property.SetColumnName(ToSnakeCase(currentName));

                }

                // Key names
                foreach (var key in entity.GetKeys())
                {
                    key.SetName(ToSnakeCase(key.GetName() ?? ""));
                }

                // Foreign Key names
                foreach (var foreignKey in entity.GetForeignKeys())
                {
                    foreignKey.SetConstraintName(ToSnakeCase(foreignKey.GetConstraintName() ?? ""));
                }

                // Index names
                foreach (var index in entity.GetIndexes())
                {
                    index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName() ?? ""));
                }
            }
        }

        private void ApplyTenantFilter<T>(ModelBuilder modelBuilder) where T : class, ITenantEntity
        {
            modelBuilder.Entity<T>().HasQueryFilter(e => 
                _tenantService.GetTenantId() == null || e.TenantId == _tenantService.GetTenantId() || e.TenantId == Guid.Empty);
        }

        private void ApplyFacilityFilter<T>(ModelBuilder modelBuilder) where T : class, IFacilityEntity
        {
            modelBuilder.Entity<T>().HasQueryFilter(e => 
                _facilityContext.CurrentFacilityId == Guid.Empty || 
                e.FacilityId == _facilityContext.CurrentFacilityId);
        }

        private void ApplyCompositeFilter<T>(ModelBuilder modelBuilder) where T : class, ITenantEntity, IFacilityEntity
        {
            modelBuilder.Entity<T>().HasQueryFilter(e => 
                (_tenantService.GetTenantId() == null || e.TenantId == _tenantService.GetTenantId() || e.TenantId == Guid.Empty) &&
                (_facilityContext.CurrentFacilityId == Guid.Empty || e.FacilityId == _facilityContext.CurrentFacilityId));
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            // Global Guid Lowercase Converter for SQLite/Cross-Platform consistency
            configurationBuilder.Properties<Guid>()
                .HaveConversion<GuidLowercaseConverter>();
            configurationBuilder.Properties<Guid?>()
                .HaveConversion<GuidLowercaseConverter>();

            configurationBuilder.Properties<Email>()
                .HaveConversion<EmailValueConverter>();
            configurationBuilder.Properties<PhoneNumber>()
                .HaveConversion<PhoneNumberValueConverter>();
        }

        private class GuidLowercaseConverter : ValueConverter<Guid, string>
        {
            public GuidLowercaseConverter() : base(
                v => v.ToString().ToLower(),
                v => Guid.Parse(v))
            { }
        }

        private class EmailValueConverter : ValueConverter<Email, string>
        {
            public EmailValueConverter() : base(
                v => v.Value,
                v => Email.Create(v).Value) 
            { }
        }

        private class PhoneNumberValueConverter : ValueConverter<PhoneNumber, string>
        {
            public PhoneNumberValueConverter() : base(
                v => v.Value,
                v => PhoneNumber.Create(v).Value) 
            { }
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Task 1: Database Safety - Connection String Enforcement
            if (!optionsBuilder.IsConfigured)
            {
                var dbFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Luxurya");
                if (!Directory.Exists(dbFolder)) Directory.CreateDirectory(dbFolder);
                
                var dbPath = Path.Combine(dbFolder, "GymManagement.db");
                optionsBuilder.UseSqlite($"Data Source={dbPath};Mode=ReadWriteCreate;Foreign Keys=True;Pooling=True;");
            }
            
            // Add Interceptors if they were provided (avoids resolution loop in App.xaml.cs delegate)
            if (_shadowInterceptor != null) optionsBuilder.AddInterceptors(_shadowInterceptor);
            if (_outboxInterceptor != null) optionsBuilder.AddInterceptors(_outboxInterceptor);
            if (_auditableInterceptor != null) optionsBuilder.AddInterceptors(_auditableInterceptor);

            base.OnConfiguring(optionsBuilder);
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await base.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during SaveChangesAsync.");
                throw;
            }
        }

        private string ToSnakeCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var startText = Regex.Replace(input, "(?<!^)([A-Z][a-z]|(?<=[a-z])[A-Z])", "_$1", RegexOptions.Compiled);
            return startText.ToLower();
        }
    }
}


