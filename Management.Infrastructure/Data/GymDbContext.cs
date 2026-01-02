using Management.Domain.Models;
using Management.Domain.Models.Resilience;
using Management.Domain.Primitives;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Infrastructure.Data
{
    public class GymDbContext : DbContext
    {
        private readonly Domain.Services.ITenantService _tenantService;

        public GymDbContext(DbContextOptions<GymDbContext> options, Domain.Services.ITenantService tenantService) : base(options)
        {
            _tenantService = tenantService;
        }

        // Helper for Query Filter to access Service dynamically per-context
        internal Guid? GetCurrentTenantId() => _tenantService.GetTenantId();

        // Helper method to apply filter via reflection
        void ConfigureGlobalFilters<T>(ModelBuilder builder) where T : Entity
        {
            builder.Entity<T>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == _tenantService.GetTenantId());
        }

        // --- 1. CORE ENTITIES ---
        public DbSet<Member> Members { get; set; }
        public DbSet<StaffMember> StaffMembers { get; set; }
        public DbSet<Registration> Registrations { get; set; }
        public DbSet<MembershipPlan> MembershipPlans { get; set; }

        // --- 2. OPERATIONS ---
        public DbSet<AccessEvent> AccessEvents { get; set; }
        public DbSet<Turnstile> Turnstiles { get; set; }
        public DbSet<Reservation> Reservations { get; set; }

        // --- 3. COMMERCE ---
        public DbSet<Product> Products { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleItem> SaleItems { get; set; }
        public DbSet<PayrollEntry> PayrollEntries { get; set; }

        // --- 4. CONFIGURATION ---
        public DbSet<GymSettings> GymSettings { get; set; }
        public DbSet<FacilityZone> FacilityZones { get; set; }
        public DbSet<OutboxMessage> OutboxMessages { get; set; }
        public DbSet<OfflineAction> OfflineActions { get; set; }



        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var currentTenantId = _tenantService.GetTenantId();
            var entries = ChangeTracker.Entries<Entity>().ToList();
            var outboxItems = new List<OutboxMessage>();

            foreach (var entry in entries)
            {
                // 1. Automatic Auditing & Sync Tracking
                string? action = null;

                if (entry.State == EntityState.Added)
                {
                    entry.Entity.UpdateTimestamp(); // Assuming we use the method or setting property
                    if (entry.Entity.TenantId == Guid.Empty && currentTenantId.HasValue)
                    {
                        entry.Entity.TenantId = currentTenantId.Value;
                    }
                    action = "Insert";
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdateTimestamp();
                    action = "Update";
                }
                else if (entry.State == EntityState.Deleted)
                {
                    action = "Delete";
                    if (entry.Metadata.FindProperty("IsDeleted") != null)
                    {
                        entry.State = EntityState.Modified;
                        entry.CurrentValues["IsDeleted"] = true;
                    }
                }

                if (action != null)
                {
                    var outbox = new OutboxMessage
                    {
                        EntityType = entry.Entity.GetType().Name,
                        EntityId = entry.Entity.Id.ToString(),
                        Action = action,
                        ContentJson = System.Text.Json.JsonSerializer.Serialize(entry.Entity),
                        CreatedBy = _tenantService.GetUserId(), // Captured from Tenant Context
                        IsProcessed = false
                    };
                    if (currentTenantId.HasValue) outbox.TenantId = currentTenantId.Value;
                    outboxItems.Add(outbox);
                }
            }

            if (outboxItems.Any())
            {
                OutboxMessages.AddRange(outboxItems);
            }

            return base.SaveChangesAsync(cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Ignore Supabase types that might be accidentally discovered
            modelBuilder.Ignore<Supabase.Gotrue.ClientOptions>();
            modelBuilder.Ignore<Supabase.Postgrest.ClientOptions>();
            modelBuilder.Ignore<Supabase.Realtime.ClientOptions>();
            modelBuilder.Ignore<Supabase.Storage.ClientOptions>();
            modelBuilder.Ignore<Supabase.SupabaseOptions>();

            // Fail-safe: Ignore any type named 'ClientOptions' that might have been discovered transitively
            foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
            {
                if (entityType.Name.Contains("ClientOptions") || entityType.Name.Contains("SupabaseOptions"))
                {
                    modelBuilder.Ignore(entityType.ClrType);
                }
            }

            // --- DECIMAL PRECISION (Financials) ---
            var decimalProps = new[]
            {
                (typeof(Product), nameof(Product.Price)),
                (typeof(Product), nameof(Product.Cost)),
                (typeof(MembershipPlan), nameof(MembershipPlan.Price)),
                (typeof(Sale), nameof(Sale.TotalAmount)),
                (typeof(Sale), nameof(Sale.SubtotalAmount)),
                (typeof(Sale), nameof(Sale.TaxAmount)),
                (typeof(SaleItem), nameof(SaleItem.UnitPriceSnapshot)),
                (typeof(PayrollEntry), nameof(PayrollEntry.Amount))
            };

            foreach (var (type, prop) in decimalProps)
            {
                // If it's a Money VO, we map the underlying Amount
                if (prop == "Price" || prop == "Cost" || prop == "TotalAmount" || prop == "SubtotalAmount" || prop == "TaxAmount" || prop == "UnitPriceSnapshot" || prop == "Amount")
                {
                    modelBuilder.Entity(type).OwnsOne(type.GetProperty(prop)!.PropertyType, prop)
                        .Property("Amount").HasColumnType("decimal(18,2)");
                }
                else
                {
                    modelBuilder.Entity(type).Property(prop).HasColumnType("decimal(18,2)");
                }
            }

            // --- VALUE OBJECT MAPPING (Email, Phone) ---
            // --- VALUE OBJECT MAPPING (Email, Phone) ---
            modelBuilder.Entity<Member>().OwnsOne(m => m.Email, e => 
            {
                e.Property(x => x.Value).HasColumnName("Email");
                e.HasIndex(x => x.Value).IsUnique(); 
            });
            modelBuilder.Entity<Member>().OwnsOne(m => m.PhoneNumber, p => p.Property(x => x.Value).HasColumnName("PhoneNumber"));
            modelBuilder.Entity<Member>().OwnsOne(m => m.EmergencyContactPhone, p => p.Property(x => x.Value).HasColumnName("EmergencyContactPhone"));

            modelBuilder.Entity<StaffMember>().OwnsOne(s => s.Email, e => 
            {
                e.Property(x => x.Value).HasColumnName("Email");
                e.HasIndex(x => x.Value).IsUnique();
            });
            modelBuilder.Entity<StaffMember>().OwnsOne(s => s.PhoneNumber, p => p.Property(x => x.Value).HasColumnName("PhoneNumber"));

            modelBuilder.Entity<Registration>().OwnsOne(r => r.Email, e => e.Property(x => x.Value).HasColumnName("Email"));
            modelBuilder.Entity<Registration>().OwnsOne(r => r.PhoneNumber, p => p.Property(x => x.Value).HasColumnName("PhoneNumber"));

            // --- RELATIONSHIPS ---
            modelBuilder.Entity<Sale>()
                .HasMany(s => s.Items)
                .WithOne()
                .HasForeignKey(i => i.SaleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Member>()
                .HasOne<MembershipPlan>()
                .WithMany()
                .HasForeignKey(m => m.MembershipPlanId)
                .OnDelete(DeleteBehavior.SetNull);

            // --- INDEXING ---
            modelBuilder.Entity<AccessEvent>().HasIndex(e => e.Timestamp);
            modelBuilder.Entity<Sale>().HasIndex(e => e.Timestamp);
            // Member.Email and StaffMember.Email indexes moved to OwnsOne block above
            modelBuilder.Entity<Product>().HasIndex(p => p.SKU).IsUnique();

            // --- MULTI-TENANCY & SOFT DELETE ---
            var configureMethod = typeof(GymDbContext).GetMethod(nameof(ConfigureGlobalFilters), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(Entity).IsAssignableFrom(entityType.ClrType))
                {
                    // 1. Configure "IsDeleted" and "TenantId"
                    modelBuilder.Entity(entityType.ClrType).Property(nameof(Entity.IsDeleted));
                    modelBuilder.Entity(entityType.ClrType).Property(nameof(Entity.TenantId));

                    // 2. Global Query Filter via Helper Method
                    // This ensures the Lambda is compiled effectively accessing 'this._tenantService'
                    configureMethod!.MakeGenericMethod(entityType.ClrType).Invoke(this, new object[] { modelBuilder });
                }
            }

            // --- CONCURRENCY ---
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(Entity).IsAssignableFrom(entityType.ClrType))
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .Property("RowVersion")
                        .IsRowVersion();
                }
            }
        }
    }
}