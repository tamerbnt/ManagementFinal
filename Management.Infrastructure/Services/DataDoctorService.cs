using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Management.Domain.Primitives;
using Management.Domain.Services;
using Management.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Management.Infrastructure.Services
{
    public class DataDoctorService : IDataDoctorService
    {
        private readonly GymDbContext _context;

        public DataDoctorService(GymDbContext context)
        {
            _context = context;
        }

        public async Task<Result<DataHealthReport>> ScanForIssuesAsync(Guid facilityId)
        {
            try
            {
                var report = new DataHealthReport();

                // Run scans in parallel on background threads to prevent UI lag
                await Task.Run(async () =>
                {
                    var duplicateTask = ScanForDuplicateMembersAsync(facilityId);
                    var expiredTask = ScanForExpiredActiveMembersAsync(facilityId);

                    await Task.WhenAll(duplicateTask, expiredTask);

                    report.Issues.AddRange(duplicateTask.Result);
                    report.Issues.AddRange(expiredTask.Result);

                    report.DuplicateMembersCount = duplicateTask.Result.Count;
                    report.ExpiredActiveCount = expiredTask.Result.Count;
                    report.StaleOrdersCount = 0;
                    report.ScanCompletedAt = DateTime.Now;
                });

                return Result.Success(report);
            }
            catch (Exception ex)
            {
                return Result.Failure<DataHealthReport>(new Error("SCAN_ERROR", ex.Message));
            }
        }

        private async Task<List<DataHealthIssue>> ScanForDuplicateMembersAsync(Guid facilityId)
        {
            var issues = new List<DataHealthIssue>();

            // Find duplicates by Email - using Email value object's Value property
            var emailDuplicates = await _context.Members
                .Where(m => m.Email != null && !string.IsNullOrEmpty(m.Email.Value))
                .GroupBy(m => m.Email.Value.ToLower())
                .Where(g => g.Count() > 1)
                .ToListAsync();

            foreach (var group in emailDuplicates)
            {
                issues.Add(new DataHealthIssue
                {
                    IssueType = "DuplicateEmail",
                    Description = $"Email '{group.Key}' is used by {group.Count()} members",
                    Severity = "Medium"
                });
            }

            // Find duplicates by Phone - using PhoneNumber value object's Value property
            var phoneDuplicates = await _context.Members
                .Where(m => m.PhoneNumber != null && !string.IsNullOrEmpty(m.PhoneNumber.Value))
                .GroupBy(m => m.PhoneNumber.Value)
                .Where(g => g.Count() > 1)
                .ToListAsync();

            foreach (var group in phoneDuplicates)
            {
                issues.Add(new DataHealthIssue
                {
                    IssueType = "DuplicatePhone",
                    Description = $"Phone '{group.Key}' is used by {group.Count()} members",
                    Severity = "Medium"
                });
            }

            return issues;
        }

        private async Task<List<DataHealthIssue>> ScanForExpiredActiveMembersAsync(Guid facilityId)
        {
            var issues = new List<DataHealthIssue>();

            var expiredActive = await _context.Members
                .Where(m => m.ExpirationDate < DateTime.Now 
                    && m.Status == Domain.Enums.MemberStatus.Active)
                .ToListAsync();

            foreach (var member in expiredActive)
            {
                issues.Add(new DataHealthIssue
                {
                    IssueType = "ExpiredActive",
                    Description = $"Member '{member.FullName}' expired on {member.ExpirationDate:yyyy-MM-dd} but still marked Active",
                    EntityId = member.Id,
                    Severity = "High"
                });
            }

            return issues;
        }
    }
}
