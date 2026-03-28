using System.Threading;
using System.Threading.Tasks;

namespace Management.Domain.Interfaces
{
    public interface IUnitOfWork
    {
        Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        void ClearTracker();
        string GetChangeTrackerDebugView();
        void SetShadowProperty<TValue>(object entity, string propertyName, TValue value);
    }

    public interface IUnitOfWorkTransaction : System.IDisposable, System.IAsyncDisposable
    {
        Task CommitAsync(CancellationToken cancellationToken = default);
        Task RollbackAsync(CancellationToken cancellationToken = default);
    }
}
