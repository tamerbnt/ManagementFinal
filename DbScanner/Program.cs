using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Management.Infrastructure.Data;
using Management.Domain.Models;
using Management.Domain.ValueObjects;
using Management.Domain.Enums;
using Management.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

class Program
{
    class DummyUserService : ICurrentUserService { public Guid? UserId => Guid.NewGuid(); public bool IsAuthenticated => true; public string Email => ""; public string FullName => ""; }
    class DummyTenantService : ITenantService { public Guid? GetTenantId() => Guid.NewGuid(); public void SetTenantId(Guid tenantId) { } }
    class DummyFacilityService : IFacilityContextService { 
        public Guid CurrentFacilityId => Guid.NewGuid(); 
        public FacilityType CurrentFacility => FacilityType.Gym; 
        public string Terminology => "Gym";
        public event EventHandler FacilityChanged;
        public Task SwitchFacilityAsync(Guid facilityId) => Task.CompletedTask;
    }
    class DummySecureStorage : ISecureStorageService { 
        public Task SaveAsync(string k, string v) => Task.CompletedTask; 
        public Task<string> LoadAsync(string k) => Task.FromResult(""); 
        public Task DeleteAsync(string k) => Task.CompletedTask;
    }

    static async Task Main()
    {
        try
        {
            var dbFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Luxurya");
            var dbPath = Path.Combine(dbFolder, "GymManagement.db");

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            using (var ctx = new AppDbContext(
                options,
                new DummyUserService(),
                new DummyTenantService(),
                new DummyFacilityService(),
                new NullLogger<AppDbContext>(),
                null, null, null, new DummySecureStorage()
            ))
            {
                var memberResult = Member.Register("Test User", Email.Create("test@test.com").Value, PhoneNumber.Create("+1234567890").Value, "CARD123", null);
                if (memberResult.IsSuccess)
                {
                    ctx.Members.Add(memberResult.Value);
                    await ctx.SaveChangesAsync();
                    Console.WriteLine("CREATED MEMBER SUCCESSFULLY!");
                    
                    ctx.Members.Remove(memberResult.Value);
                    await ctx.SaveChangesAsync();
                }
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
