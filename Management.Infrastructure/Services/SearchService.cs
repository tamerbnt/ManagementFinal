using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Management.Application.DTOs;
using Management.Domain.Models.Restaurant;
using Management.Domain.Models.Salon;
using Management.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Management.Infrastructure.Services
{
    public interface ISearchService
    {
        Task<IEnumerable<SearchResultDto>> SearchAsync(string query, Management.Domain.Enums.FacilityType facilityType);
    }

    public class SearchService : ISearchService
    {
        private readonly AppDbContext _dbContext;

        public SearchService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IEnumerable<SearchResultDto>> SearchAsync(string query, Management.Domain.Enums.FacilityType facilityType)
        {
            var results = new List<SearchResultDto>();
            var q = (query ?? "").Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(q)) return results;

            // 1. Staff Search (Common to all facilities)
            await SearchStaffAsync(results, q);

            // 2. Facility Specific Searches
            switch (facilityType)
            {
                case Management.Domain.Enums.FacilityType.Gym:
                    await SearchMembersAsync(results, q);
                    await SearchProductsAsync(results, q, "ShopView");
                    break;

                case Management.Domain.Enums.FacilityType.Salon:
                    await SearchMembersAsync(results, q); // Clients
                    await SearchAppointmentsAsync(results, q); // Schedule
                    await SearchProductsAsync(results, q, "ShopView");
                    await SearchSalonServicesAsync(results, q);
                    break;

                case Management.Domain.Enums.FacilityType.Restaurant:
                    await SearchMenuItemsAsync(results, q);
                    await SearchProductsAsync(results, q, "MenuManagementView|Inventory"); // Inventory deep-link
                    break;
            }

            return results;
        }

        private async Task SearchMembersAsync(List<SearchResultDto> results, string q)
        {
            try
            {
                var members = await _dbContext.Members
                    .AsNoTracking()
                    .Where(m => (m.FullName != null && m.FullName.ToLower().Contains(q)) || 
                                (m.Email != null && m.Email.Value != null && m.Email.Value.ToLower().Contains(q)) || 
                                (m.PhoneNumber != null && m.PhoneNumber.Value != null && m.PhoneNumber.Value.Contains(q)) || 
                                (m.CardId != null && m.CardId.Contains(q)))
                    .Take(5)
                    .Select(m => new SearchResultDto
                    {
                        Title = m.FullName,
                        Subtitle = $"Member • Card: {m.CardId ?? "None"} • {m.Email.Value}",
                        Group = "MEMBERS",
                        ActionKey = "Nav",
                        ActionParameter = $"MembersView|{m.Id}"
                    })
                    .ToListAsync();
                results.AddRange(members);
            }
            catch { }
        }

        private async Task SearchStaffAsync(List<SearchResultDto> results, string q)
        {
            try
            {
                var staff = await _dbContext.StaffMembers
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
                    .ToListAsync();
                results.AddRange(staff);
            }
            catch { }
        }

        private async Task SearchProductsAsync(List<SearchResultDto> results, string q, string viewTarget)
        {
            try
            {
                var products = await _dbContext.Products
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
                    .ToListAsync();
                results.AddRange(products);
            }
            catch { }
        }

        private async Task SearchMenuItemsAsync(List<SearchResultDto> results, string q)
        {
            try
            {
                var menuItems = await _dbContext.MenuItems
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
                    .ToListAsync();
                results.AddRange(menuItems);
            }
            catch { }
        }

        private async Task SearchAppointmentsAsync(List<SearchResultDto> results, string q)
        {
            try
            {
                var appointments = await _dbContext.Appointments
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
                    .ToListAsync();
                results.AddRange(appointments);
            }
            catch { }
        }

        private async Task SearchSalonServicesAsync(List<SearchResultDto> results, string q)
        {
            try
            {
                var services = await _dbContext.SalonServices
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
                    .ToListAsync();
                results.AddRange(services);
            }
            catch { }
        }
    }
}

