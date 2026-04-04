using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Management.Application.DTOs;
using Management.Domain.Models.Restaurant;
using Management.Domain.Models.Salon;
using Management.Infrastructure.Data;
using Management.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Management.Infrastructure.Services
{
    public interface ISearchService
    {
        Task<IEnumerable<SearchResultDto>> SearchAsync(string query, Management.Domain.Enums.FacilityType facilityType, System.Threading.CancellationToken ct = default);
    }

    public class SearchService : ISearchService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private static readonly System.Threading.SemaphoreSlim _searchLock = new(1, 1);

        public SearchService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public async Task<IEnumerable<SearchResultDto>> SearchAsync(string query, Management.Domain.Enums.FacilityType facilityType, System.Threading.CancellationToken ct = default)
        {
            var results = new List<SearchResultDto>();
            var q = (query ?? "").Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(q)) return results;

            await _searchLock.WaitAsync(ct);
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();
                    var tenantId = tenantService.GetTenantId() ?? Guid.Empty;
                    
                    System.Diagnostics.Debug.WriteLine($"[SEARCH_DIAG] Query: '{q}', Tenant: {tenantId}, Mode: {facilityType}");

                // 1. Staff Search (Common to all facilities)
                    await SearchStaffAsync(results, q, dbContext, ct);

                    if (ct.IsCancellationRequested) return results;

                    // 2. Facility Specific Searches (Absolute Separation)
                    switch (facilityType)
                    {
                        case Management.Domain.Enums.FacilityType.General:
                            // In Dashboard/Global mode - Search both but categorized separately
                            await SearchMembersAsync(results, q, Management.Domain.Enums.FacilityType.Gym, dbContext, tenantId, ct);
                            if (ct.IsCancellationRequested) return results;
                            await SearchMembersAsync(results, q, Management.Domain.Enums.FacilityType.Salon, dbContext, tenantId, ct);
                            if (ct.IsCancellationRequested) return results;
                            await SearchProductsAsync(results, q, "ShopView", dbContext, ct);
                            break;

                        case Management.Domain.Enums.FacilityType.Gym:
                            // Strict Gym View
                            await SearchMembersAsync(results, q, facilityType, dbContext, tenantId, ct);
                            await SearchProductsAsync(results, q, "ShopView", dbContext, ct);
                            break;

                        case Management.Domain.Enums.FacilityType.Salon:
                            // Strict Salon View
                            await SearchMembersAsync(results, q, facilityType, dbContext, tenantId, ct);
                            if (ct.IsCancellationRequested) return results;
                            await SearchProductsAsync(results, q, "ShopView", dbContext, ct);
                            await SearchAppointmentsAsync(results, q, dbContext, ct);
                            await SearchSalonServicesAsync(results, q, dbContext, ct);
                            break;

                        case Management.Domain.Enums.FacilityType.Restaurant:
                            await SearchMenuItemsAsync(results, q, dbContext, ct);
                            await SearchProductsAsync(results, q, "MenuManagementView|Inventory", dbContext, ct);
                            break;
                    }
                }
            }
            finally
            {
                _searchLock.Release();
            }

            return results;
        }

        private async Task SearchMembersAsync(List<SearchResultDto> results, string q, Management.Domain.Enums.FacilityType facilityType, AppDbContext dbContext, Guid tenantId, System.Threading.CancellationToken ct)
        {
            System.Diagnostics.Debug.WriteLine($"[SEARCH_DIAG] --- Member Search Start ---");
            System.Diagnostics.Debug.WriteLine($"[SEARCH_DIAG] Query: '{q}'");
            System.Diagnostics.Debug.WriteLine($"[SEARCH_DIAG] FacilityType context: {facilityType}");
            System.Diagnostics.Debug.WriteLine($"[SEARCH_DIAG] TenantId parameter: {tenantId}");

            try
            {
                // Absolute Separation Logic:
                // 1. We bypass strict global filters to find "Global" members (FacilityId == Guid.Empty)
                // 2. We filter by industry discriminator stored in SegmentDataJson.
                
                var segmentDiscriminator = facilityType == Management.Domain.Enums.FacilityType.Gym ? "Gym" : "Salon";
                var pattern = $"%{q}%";
                var tagPattern = $"%\"{segmentDiscriminator}\"%"; // Look for the key in JSON

                System.Diagnostics.Debug.WriteLine($"[SEARCH_DIAG] Segment Discriminator: {segmentDiscriminator}");
                System.Diagnostics.Debug.WriteLine($"[SEARCH_DIAG] Tag Pattern (JSON): {tagPattern}");
                var membersQuery = dbContext.Members
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(m => !m.IsDeleted);

                // --- Robust Tenant Filtering ---
                if (tenantId != Guid.Empty)
                {
                    membersQuery = membersQuery.Where(m => m.TenantId == tenantId || m.TenantId == Guid.Empty);
                }

                // --- Industry/Segment Filtering ---
                membersQuery = membersQuery.Where(m => 
                    // Either explicit JSON match
                    (m.SegmentDataJson != null && EF.Functions.Like(m.SegmentDataJson, tagPattern)) || 
                    // Or fallback for unsegmented members (legacy or default)
                    (facilityType == Management.Domain.Enums.FacilityType.Gym && (string.IsNullOrEmpty(m.SegmentDataJson) || m.SegmentDataJson == "{}" || !m.SegmentDataJson.Contains("\"SegmentType\":\"Salon\"")))
                );

                // --- Identity Filtering ---
                var members = await membersQuery
                    .Where(m => EF.Functions.Like(m.FullName, pattern) || 
                               (m.Email != null && EF.Functions.Like(EF.Property<string>(m, "Email"), pattern)) ||
                               (m.PhoneNumber != null && EF.Functions.Like(EF.Property<string>(m, "PhoneNumber"), pattern)) ||
                               (m.CardId != null && EF.Functions.Like(m.CardId, pattern)))
                .Take(10)
                .Select(m => new SearchResultDto
                    {
                        Title = m.FullName,
                        Subtitle = facilityType == Management.Domain.Enums.FacilityType.Gym 
                            ? (m.CardId != null ? $"Member • Card: {m.CardId}" : "Member") + (m.Email != null ? $" • {(string)(object)m.Email}" : "")
                            : (m.PhoneNumber != null ? $"Client • {(string)(object)m.PhoneNumber}" : "Client") + (m.Email != null ? $" • {(string)(object)m.Email}" : ""),
                        Group = facilityType == Management.Domain.Enums.FacilityType.Gym ? "MEMBERS" : "CLIENTS",
                        ActionKey = "Nav",
                        ActionParameter = $"MembersView|{m.Id}"
                    })
                .ToListAsync(ct);

                System.Diagnostics.Debug.WriteLine($"[SEARCH_DIAG] Found {members.Count} members matching criteria.");
                foreach(var m in members) System.Diagnostics.Debug.WriteLine($"[SEARCH_DIAG]   -> Match: {m.Title}");

                results.AddRange(members);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SEARCH_DIAG] !!! EXCEPTION in SearchMembersAsync: {ex.Message}");
                if (ex.InnerException != null) System.Diagnostics.Debug.WriteLine($"[SEARCH_DIAG]   -> Inner: {ex.InnerException.Message}");
            }
            finally
            {
                System.Diagnostics.Debug.WriteLine($"[SEARCH_DIAG] --- Member Search End ---");
            }
        }

        private async Task SearchStaffAsync(List<SearchResultDto> results, string q, AppDbContext dbContext, System.Threading.CancellationToken ct)
        {
            try
            {
                var staff = await dbContext.StaffMembers
                    .AsNoTracking()
                    .Where(s => s.FullName != null && s.FullName.ToLower().Contains(q))
                    .Take(3)
                    .Select(s => new SearchResultDto
                    {
                        Title = s.FullName,
                        Subtitle = $"Staff • {s.Role}",
                        Group = "STAFF",
                        ActionKey = "Nav",
                        ActionParameter = $"FinanceAndStaffView|{s.Id}"
                    })
                    .ToListAsync(ct);
                results.AddRange(staff);
            }
            catch { }
        }

        private async Task SearchProductsAsync(List<SearchResultDto> results, string q, string viewTarget, AppDbContext dbContext, System.Threading.CancellationToken ct)
        {
            try
            {
                var products = await dbContext.Products
                    .AsNoTracking()
                    .Where(p => (p.Name != null && p.Name.ToLower().Contains(q)) || (!string.IsNullOrEmpty(p.SKU) && p.SKU.ToLower().Contains(q)))
                    .Take(5)
                    .Select(p => new SearchResultDto
                    {
                        Title = p.Name,
                        Subtitle = $"Product • SKU: {p.SKU} • Stock: {p.StockQuantity}",
                        Group = "INVENTORY",
                        ActionKey = "Nav",
                        ActionParameter = $"{viewTarget}|{p.Id}"
                    })
                    .ToListAsync(ct);
                results.AddRange(products);
            }
            catch { }
        }

        private async Task SearchMenuItemsAsync(List<SearchResultDto> results, string q, AppDbContext dbContext, System.Threading.CancellationToken ct)
        {
            try
            {
                var menuItems = await dbContext.MenuItems
                    .AsNoTracking()
                    .Where(m => (m.Name != null && m.Name.ToLower().Contains(q)) || (m.Category != null && m.Category.ToLower().Contains(q)))
                    .Take(5)
                    .Select(m => new SearchResultDto
                    {
                        Title = m.Name,
                        Subtitle = $"Menu • {m.Category} • {m.Price:N2} DA",
                        Group = "MENU",
                        ActionKey = "Nav",
                        ActionParameter = $"MenuManagementView|{m.Id}" 
                    })
                    .ToListAsync(ct);
                results.AddRange(menuItems);
            }
            catch { }
        }

        private async Task SearchAppointmentsAsync(List<SearchResultDto> results, string q, AppDbContext dbContext, System.Threading.CancellationToken ct)
        {
            try
            {
                var appointments = await dbContext.Appointments
                    .AsNoTracking()
                    .Where(a => (a.ClientName != null && a.ClientName.ToLower().Contains(q)) || (a.ServiceName != null && a.ServiceName.ToLower().Contains(q)))
                    .Take(5)
                    .Select(a => new SearchResultDto
                    {
                        Title = a.ClientName,
                        Subtitle = $"Appointment • {a.ServiceName} • {a.StartTime:g}",
                        Group = "SCHEDULE",
                        ActionKey = "Nav",
                        ActionParameter = $"SchedulerView|{a.Id}"
                    })
                    .ToListAsync(ct);
                results.AddRange(appointments);
            }
            catch { }
        }

        private async Task SearchSalonServicesAsync(List<SearchResultDto> results, string q, AppDbContext dbContext, System.Threading.CancellationToken ct)
        {
            try
            {
                var services = await dbContext.SalonServices
                    .AsNoTracking()
                    .Where(s => (s.Name != null && s.Name.ToLower().Contains(q)) || (s.Category != null && s.Category.ToLower().Contains(q)))
                    .Take(3)
                    .Select(s => new SearchResultDto
                    {
                        Title = s.Name,
                        Subtitle = $"Service • {s.Category} • {s.DurationMinutes}m",
                        Group = "SERVICES",
                        ActionKey = "Nav",
                        ActionParameter = $"SchedulerView|{s.Id}"
                    })
                    .ToListAsync(ct);
                results.AddRange(services);
            }
            catch { }
        }
    }
}

