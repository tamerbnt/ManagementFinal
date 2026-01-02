using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Management.Infrastructure.Data
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<GymDbContext>
    {
        public GymDbContext CreateDbContext(string[] args)
        {
            // Build config to read connection string
            // In a real app, this reads appsettings.json
            // For the prototype, we can use a direct builder or hardcoded string placeholder

            var optionsBuilder = new DbContextOptionsBuilder<GymDbContext>();

            // Local SQLite for offline-first capability
            string connectionString = "Data Source=GymManagement.db";

            optionsBuilder.UseSqlite(connectionString);

            return new GymDbContext(optionsBuilder.Options, new MockTenantService());
        }

        private class MockTenantService : Domain.Services.ITenantService
        {
            public Guid? GetTenantId() => Guid.Empty;
            public void SetTenantId(Guid tenantId) { }
            public Guid? GetUserId() => Guid.Empty;
            public void SetUserId(Guid userId) { }
            public string? GetRole() => null;
            public void SetRole(string role) { }
            public string GetHardwareId() => "DESIGN-TIME";
            public void Clear() { }
        }
    }
}