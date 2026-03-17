using System.Threading;
using System.Threading.Tasks;

namespace Management.Domain.Interfaces
{
    public interface IUnitOfWork
    {
        Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    }

    public interface IUnitOfWorkTransaction : System.IDisposable, System.IAsyncDisposable
    {
        Task CommitAsync(CancellationToken cancellationToken = default);
        Task RollbackAsync(CancellationToken cancellationToken = default);
    }
}
