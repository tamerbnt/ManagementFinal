using Management.Application.DTOs;
using Management.Application.Interfaces;
using Management.Domain.Interfaces;
using Management.Domain.Models.Restaurant;
using Management.Infrastructure.Data;
using OrderStatus = Management.Domain.Models.Restaurant.OrderStatus;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Management.Infrastructure.Services.Dashboard.Aggregators
{
    public class ActivityAggregator : BaseAggregator
    {
        private readonly ISaleRepository _saleRepository;
        private readonly IAccessEventRepository _accessEventRepository;
        private readonly AppDbContext _dbContext;

        public ActivityAggregator(ISaleRepository saleRepository, IAccessEventRepository accessEventRepository, AppDbContext dbContext)
        {
            _saleRepository = saleRepository;
            _accessEventRepository = accessEventRepository;
            _dbContext = dbContext;
        }

        public override int Priority => 50;

        public override bool CanHandle(DashboardContext context) => true;

        public override async Task AggregateAsync(DashboardSummaryDto dto, DashboardContext context)
        {
            var facilityId = context.FacilityId;

            var transactions = new List<TransactionDto>();
            var allActivities = new List<(DateTime SortDate, ActivityItemDto Item)>();

            // 1. Sales
            var recentSales = await _saleRepository.GetByDateRangeAsync(facilityId, context.UtcDayStart.AddDays(-3), context.UtcNow);

            transactions.AddRange(recentSales.Select(s => new TransactionDto
            {
                MemberName = s.MemberId != Guid.Empty ? "Client" : "Walk-In",
                Amount = s.TotalAmount.Amount,
                Type = s.TransactionType,
                Timestamp = s.Timestamp
            }));

            allActivities.AddRange(recentSales.Select(s => (s.Timestamp, new ActivityItemDto
            {
                Title = "Sale Completed",
                Subtitle = $"{s.TotalAmount.Amount:N2} DA",
                Timestamp = s.Timestamp.ToLocalTime().ToString("o"), // Use ISO8601 for robust parsing
                Type = "Revenue",
                Icon = "DollarSign"
            })));

            // 1b. Access Events (Gym Check-ins)
            if (context.IsGym)
            {
                var recentAccess = await _accessEventRepository.GetByDateRangeAsync(facilityId, context.UtcDayStart.AddDays(-3), context.UtcNow);
                allActivities.AddRange(recentAccess.Select(a => (a.Timestamp, new ActivityItemDto
                {
                    Title = a.IsAccessGranted ? "Check-in Granted" : "Check-in Denied",
                    Subtitle = string.IsNullOrWhiteSpace(a.FailureReason) ? $"Card: {a.CardId}" : a.FailureReason,
                    Timestamp = a.Timestamp.ToLocalTime().ToString("o"), // Use ISO8601
                    Type = "Access",
                    Icon = a.IsAccessGranted ? "Check" : "AlertCircle"
                })));
            }

            // 2. Restaurant Orders
            if (context.IsRestaurant)
            {
                var recentOrders = await _dbContext.RestaurantOrders
                    .AsNoTracking()
                    .Where(o => o.FacilityId == facilityId && 
                                (o.Status == OrderStatus.Completed || o.Status == OrderStatus.Paid))
                    .OrderByDescending(o => o.CompletedAt ?? o.CreatedAt)
                    .Take(10)
                    .ToListAsync();


                transactions.AddRange(recentOrders.Select(o => new TransactionDto
                {
                    MemberName = $"Table {o.TableNumber}",
                    Amount = o.Subtotal + o.Tax,
                    Timestamp = o.CompletedAt ?? o.CreatedAt,
                    Type = "Order"
                }));

                // Inventory (Raw SQL Bypass)
                try
                {
                    using var conn = _dbContext.Database.GetDbConnection();
                    if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT r.name, p.total_price, p.date FROM inventory_purchases p JOIN inventory_resources r ON r.id = p.resource_id WHERE p.facility_id = @fid ORDER BY p.date DESC LIMIT 10";
                    var param = cmd.CreateParameter();
                    param.ParameterName = "@fid";
                    param.Value = facilityId.ToString();
                    cmd.Parameters.Add(param);

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        transactions.Add(new TransactionDto
                        {
                            MemberName = reader.GetString(0),
                            Amount = -reader.GetDecimal(1),
                            Type = "Inventory",
                            Timestamp = DateTime.Parse(reader.GetString(2))
                        });
                    }
                }
                catch { /* Ignore inventory errors for now */ }
            }

            dto.RecentTransactions = transactions.OrderByDescending(t => t.Timestamp).Take(10).ToList();

            dto.Activities = allActivities.OrderByDescending(a => a.SortDate).Take(10).Select(a => a.Item).ToList();
        }
    }
}
