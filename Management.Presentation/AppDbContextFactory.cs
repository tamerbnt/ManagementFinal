using System;
using System.IO;
using Management.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Management.Presentation
{
    /// <summary>
    /// Instructs the Entity Framework Core CLI (e.g., dotnet ef database update) on how
    /// to build the DbContext at design time, entirely bypassing App.xaml.cs and WPF GUI 
    /// instantiation which throws "Resource not found" errors in CLI.
    /// </summary>
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            // Setup local SQLite connection string, mimicking the runtime DefaultConnection
            var dbFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Titan");
            if (!Directory.Exists(dbFolder)) Directory.CreateDirectory(dbFolder);
            string connectionString = $"Data Source={Path.Combine(dbFolder, "GymManagement.db")}";
            optionsBuilder.UseSqlite(connectionString, b => b.MigrationsAssembly("Management.Infrastructure"));

            // Inject mocked scoped dependencies for design-time 
            return new AppDbContext(
                optionsBuilder.Options,
                new MockCurrentUserService(),
                new MockTenantService(),
                new MockFacilityContextService(),
                new NullLogger<AppDbContext>());
        }

        private class MockCurrentUserService : Management.Application.Interfaces.ICurrentUserService
        {
            public Guid? CurrentFacilityId => Guid.Empty;
            public Guid? UserId => Guid.Empty;
        }

        private class MockTenantService : Management.Domain.Services.ITenantService
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

        private class MockPublisher : MediatR.IPublisher
        {
            public System.Threading.Tasks.Task Publish(object notification, System.Threading.CancellationToken cancellationToken = default) => System.Threading.Tasks.Task.CompletedTask;
            public System.Threading.Tasks.Task Publish<TNotification>(TNotification notification, System.Threading.CancellationToken cancellationToken = default) where TNotification : MediatR.INotification => System.Threading.Tasks.Task.CompletedTask;
        }

        private class MockFacilityContextService : Management.Domain.Services.IFacilityContextService
        {
            public Guid CurrentFacilityId => Guid.Empty;
            public Management.Domain.Enums.FacilityType CurrentFacility => Management.Domain.Enums.FacilityType.Gym;
            public string LanguageCode => "en";
            public event Action<Management.Domain.Enums.FacilityType>? FacilityChanged;
            public System.Threading.Tasks.Task SwitchFacility(Management.Domain.Enums.FacilityType type) => System.Threading.Tasks.Task.CompletedTask;
            public void SetFacility(Management.Domain.Enums.FacilityType type) { }
            public void SaveLanguage(string languageCode) { }
            public void UpdateFacilities(System.Collections.Generic.Dictionary<Management.Domain.Enums.FacilityType, Guid> facilityMappings) { }
            public void UpdateFacilityId(Management.Domain.Enums.FacilityType type, Guid actualId) { }
            public void Initialize() { }
            public void CommitFacility() { }
        }
    }
}
