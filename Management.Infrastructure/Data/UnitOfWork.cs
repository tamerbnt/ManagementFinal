using System;
using System.Threading;
using System.Threading.Tasks;
using Management.Domain.Interfaces;
using Management.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace Management.Infrastructure.Data
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;

        public UnitOfWork(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            return new UnitOfWorkTransaction(transaction);
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }

        public void ClearTracker()
        {
            _context.ChangeTracker.Clear();
        }

        public string GetChangeTrackerDebugView()
        {
            return _context.ChangeTracker.DebugView.LongView;
        }

        public void SetShadowProperty<TValue>(object entity, string propertyName, TValue value)
        {
            _context.Entry(entity).Property(propertyName).CurrentValue = value;
        }
    }

    public class UnitOfWorkTransaction : IUnitOfWorkTransaction
    {
        private readonly IDbContextTransaction _transaction;

        public UnitOfWorkTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            return _transaction.CommitAsync(cancellationToken);
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            return _transaction.RollbackAsync(cancellationToken);
        }

        public void Dispose()
        {
            _transaction.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            return _transaction.DisposeAsync();
        }
    }
}
