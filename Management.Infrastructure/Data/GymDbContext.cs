using Management.Domain.Models;
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
        public DbSet<IntegrationConfig> IntegrationConfigs { get; set; }

        public GymDbContext(DbContextOptions<GymDbContext> options) : base(options)
        {
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var entries = ChangeTracker.Entries<Entity>();

            foreach (var entry in entries)
            {
                // 1. Automatic Auditing
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                }

                // 2. Intercept Deletes for Soft-Delete (Shadow Property)
                // Note: Only applies to entities where we enabled the shadow property below
                if (entry.State == EntityState.Deleted)
                {
                    if (entry.Metadata.FindProperty("IsDeleted") != null)
                    {
                        entry.State = EntityState.Modified;
                        entry.CurrentValues["IsDeleted"] = true;
                    }
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

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
                (typeof(PayrollEntry), nameof(PayrollEntry.Amount)),
                (typeof(PayrollEntry), nameof(PayrollEntry.Bonus))
            };

            foreach (var (type, prop) in decimalProps)
            {
                modelBuilder.Entity(type).Property(prop).HasColumnType("decimal(18,2)");
            }

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
            modelBuilder.Entity<Member>().HasIndex(m => m.Email).IsUnique();
            modelBuilder.Entity<StaffMember>().HasIndex(s => s.Email).IsUnique();
            modelBuilder.Entity<Product>().HasIndex(p => p.SKU).IsUnique();

            // --- SOFT DELETE CONFIGURATION (Shadow Property) ---
            // Apply to specific entities that require history preservation
            var softDeleteEntities = new[]
            {
                typeof(Member), typeof(StaffMember), typeof(Product),
                typeof(MembershipPlan), typeof(Registration)
            };

            foreach (var entityType in softDeleteEntities)
            {
                // 1. Add "IsDeleted" boolean shadow property
                modelBuilder.Entity(entityType).Property<bool>("IsDeleted");

                // 2. Add Global Query Filter
                // Logic: "SELECT * FROM Table WHERE IsDeleted = false"
                var param = System.Linq.Expressions.Expression.Parameter(entityType, "e");
                var prop = System.Linq.Expressions.Expression.PropertyOrField(param, "IsDeleted");
                var falseConst = System.Linq.Expressions.Expression.Constant(false);
                var lambda = System.Linq.Expressions.Expression.Lambda(
                    System.Linq.Expressions.Expression.Equal(prop, falseConst),
                    param);

                modelBuilder.Entity(entityType).HasQueryFilter(lambda);
            }
        }
    }
}