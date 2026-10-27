using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopFlow.Application.Abstractions;

namespace ShopFlow.Infrastructure.Persistence;

public sealed class UnitOfWork(AppDbContext db, ILogger<UnitOfWork> logger) : IUnitOfWork
{
    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        // Nested call: join the outer transaction instead of opening a new one.
        if (db.Database.CurrentTransaction is not null)
            return await operation(cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
        logger.LogDebug("Transaction {TransactionId} started ({IsolationLevel})", transaction.TransactionId, isolationLevel);

        try
        {
            var result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            logger.LogDebug("Transaction {TransactionId} committed", transaction.TransactionId);
            return result;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            // Tracked entities still hold the rolled-back in-memory state; never let them leak into a later SaveChanges.
            db.ChangeTracker.Clear();
            logger.LogInformation("Transaction {TransactionId} rolled back: {Reason}", transaction.TransactionId, ex.Message);
            throw;
        }
    }
}
