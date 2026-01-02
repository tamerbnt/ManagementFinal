using System;
using Management.Application.Services;
using System.Collections.Generic;
using Management.Application.Services;
using System.Threading.Tasks;
using Management.Application.Services;
using Management.Application.DTOs;
using Management.Application.Services;
using Management.Domain.Primitives;
using Management.Application.Services;

namespace Management.Application.Services
{
    public class DataHealthIssue
    {
        public string IssueType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Guid? EntityId { get; set; }
        public string Severity { get; set; } = "Low"; // "Low", "Medium", "High"
    }

    public class DataHealthReport
    {
        public List<DataHealthIssue> Issues { get; set; } = new();
        public int DuplicateMembersCount { get; set; }
        public int ExpiredActiveCount { get; set; }
        public int StaleOrdersCount { get; set; }
        public DateTime ScanCompletedAt { get; set; }
    }

    public interface IDataDoctorService
    {
        /// <summary>
        /// Performs comprehensive async scan for data health issues.
        /// </summary>
        Task<Result<DataHealthReport>> ScanForIssuesAsync(Guid facilityId);
    }
}
