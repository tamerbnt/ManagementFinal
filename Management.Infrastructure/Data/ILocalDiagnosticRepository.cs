using System.Collections.Generic;
using System.Threading.Tasks;
using Management.Domain.Models.Diagnostics;

namespace Management.Infrastructure.Data
{
    public interface ILocalDiagnosticRepository
    {
        Task SaveAsync(DiagnosticEntry entry);
        Task<IEnumerable<DiagnosticEntry>> GetPendingAsync();
        Task MarkAsAcknowledgedAsync(Guid id);
        Task DeleteAsync(Guid id);
        Task ClearAllAsync();
    }
}
