using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Management.Infrastructure.Data;
using Management.Domain.Models;
using Management.Domain.ValueObjects;
using Management.Domain.Enums;
using Management.Application.Interfaces;
using Management.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

class Program
{
    class DummyUserService : ICurrentUserService { 
        public Guid? UserId => Guid.NewGuid(); 
        public bool IsAuthenticated => true; 
        public string Email => ""; 
        public string FullName => ""; 
        public Guid? CurrentFacilityId => Guid.NewGuid();
    }
    class DummyTenantService : ITenantService { 
        private Guid? _tenantId = Guid.NewGuid();
        private Guid? _userId = Guid.NewGuid();
        public Guid? GetTenantId() => _tenantId; 
        public void SetTenantId(Guid tenantId) { _tenantId = tenantId; }
        public Guid? GetUserId() => _userId;
        public void SetUserId(Guid userId) { _userId = userId; }
        public string? GetRole() => "Admin";
        public void SetRole(string role) { }
        public string GetHardwareId() => "HARDWARE1";
        public void Clear() { }
    }
    class DummyFacilityService : IFacilityContextService { 
        public Guid CurrentFacilityId => Guid.NewGuid(); 
        public FacilityType CurrentFacility => FacilityType.Gym; 
        public string Terminology => "Gym";
        public string LanguageCode => "en";
        public string PublicSlug => "test-facility";
        public event Action<FacilityType>? FacilityChanged;
        public Task SwitchFacility(FacilityType type) => Task.CompletedTask;
        public void SetFacility(FacilityType type) { }
        public void SaveLanguage(string languageCode) { }
        public void UpdateFacilities(System.Collections.Generic.Dictionary<FacilityType, Guid> facilityMappings) { }
        public void UpdateFacilityId(FacilityType type, Guid actualId) { }
        public void Initialize() { }
        public void CommitFacility() { }
    }
    class DummySecureStorage : Management.Application.Services.ISecureStorageService { 
        public Task<string?> GetAsync(string key) => Task.FromResult<string?>(null);
        public string? Get(string key) => null;
        public Task SetAsync(string key, string value) => Task.CompletedTask;
        public Task RemoveAsync(string key) => Task.CompletedTask;
        public Task<bool> ContainsKeyAsync(string key) => Task.FromResult(false);
        public Task ClearAsync() => Task.CompletedTask;
    }

    static async Task Main()
    {
        try
        {
            var dbFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Luxurya");
            var dbPath = Path.Combine(dbFolder, "GymManagement.db");

            var interceptor = new AuditableEntityInterceptor();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .AddInterceptors(interceptor)
                .Options;

            using (var ctx = new AppDbContext(
                options,
                new DummyUserService(),
                new DummyTenantService(),
                new DummyFacilityService(),
                new NullLogger<AppDbContext>(),
                null, null, interceptor, new DummySecureStorage()
            ))
            {
                Console.WriteLine("--- STARTING PERSISTENCE VERIFICATION ---");
                
                // Create a member
                var member = Member.Register(
                    "Verify User", 
                    Email.Create("verify@test.com").Value, 
                    PhoneNumber.Create("+1000000000").Value, 
                    "VERIFY1", 
                    Guid.NewGuid()).Value;
                ctx.Members.Add(member);
                
                // Create a sale with items (which have owned types)
                var sale = Sale.Create(member.Id, PaymentMethod.Cash, "Registration").Value;
                var saleItem = SaleItem.Create(sale.Id, Guid.NewGuid(), "Verification Item", new Money(100, "DA"), 1).Value;
                sale.AddItem(saleItem);
                ctx.Sales.Add(sale);

                Console.WriteLine("Step 1: SaveChanges (Add) - Should succeed.");
                await ctx.SaveChangesAsync();
                Console.WriteLine("SUCCESS: Initial save completed.");

                // Verify the sale item has the owned type price
                var si = sale.Items.First();
                Console.WriteLine($"SaleItem UnitPrice: {si.UnitPriceSnapshot?.Amount} {si.UnitPriceSnapshot?.Currency}");

                Console.WriteLine("\nStep 2: SaveChanges (Delete) - Tests the Interceptor Rescue.");
                ctx.Sales.Remove(sale);
                ctx.Members.Remove(member);
                
                await ctx.SaveChangesAsync();
                Console.WriteLine("SUCCESS: Soft-delete rescue completed without DbUpdateException!");

                // Verify state in DB
                var foundMember = await ctx.Members.IgnoreQueryFilters().FirstOrDefaultAsync(m => m.Id == member.Id);
                var foundSale = await ctx.Sales.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == sale.Id);
                
                Console.WriteLine($"Member IsDeleted: {foundMember?.IsDeleted}");
                Console.WriteLine($"Sale IsDeleted: {foundSale?.IsDeleted}");
            }
        }
        catch (DbUpdateException ex)
        {
            Console.WriteLine($"\nDB_UPDATE_EXCEPTION: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"INNER_EXCEPTION: {ex.InnerException.Message}");
            else
                Console.WriteLine("No Inner Exception available.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nEXCEPTION: {ex.GetType().Name} - {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"INNER: {ex.InnerException.Message}");
        }
    }
}
