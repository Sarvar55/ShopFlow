using System.Data;

namespace ShopFlow.Application.Abstractions;

/// <summary>
/// Defines an explicit transaction boundary. Everything executed inside <paramref name="operation"/>
/// (including the audit rows written during SaveChanges) commits or rolls back as one unit.
/// </summary>
public interface IUnitOfWork
{
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);
}
