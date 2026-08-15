using PolyStore.Core;
using PolyStore.Execution;

namespace PolyStore.Hosting;

/// <summary>
/// Defines the context used for configuring a database.
/// </summary>
public abstract class DatabaseContext<TSchema>
    where TSchema : DatabaseSchema
{
    /// <summary>
    /// Gets the schema for the context.
    /// </summary>
    public abstract TSchema Schema { get; }
    
    /// <summary>
    /// Begins a new transaction.
    /// </summary>
    /// <returns>A new transaction.</returns>
    public abstract ValueTask<ITransaction> BeginTransactionAsync();

    /// <summary>
    /// Executes the relation.
    /// </summary>
    /// <param name="relation">The relation to execute.</param>
    /// <param name="cancellationToken">The cancellation token to use.</param>
    /// <typeparam name="T">The type representing the relation.</typeparam>
    /// <returns>A collection of elements.</returns>
    public abstract IAsyncEnumerable<T> ExecuteAsync<T>(
        IRelation<T> relation,
        CancellationToken cancellationToken = default);
}