namespace PolyStore.Execution;

/// <summary>
/// Represents a data source for a relation of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the data in the source.</typeparam>
public interface ITransaction : IAsyncDisposable
{
    /// <summary>
    /// Executes a single relational expression.
    /// </summary>
    /// <param name="query">The query to execute.</param>
    /// <param name="cancellationToken">The cancellation token to use.</param>
    /// <typeparam name="T">The scalar return value of the query.</typeparam>
    /// <returns>The scalar return of the expression.</returns>
    IAsyncEnumerable<T> ExecuteAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the transaction.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to use.</param>
    /// <returns>An awaitable task.</returns>
    ValueTask CommitAsync(CancellationToken cancellationToken = default);
}
