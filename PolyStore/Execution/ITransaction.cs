using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using PolyStore.Core;

namespace PolyStore.Execution;

/// <summary>
/// Defines a single atomic transaction in the storage engine.
/// </summary>
public interface ITransaction : IAsyncDisposable
{
    /// <summary>
    /// Executes a single relational expression.
    /// </summary>
    /// <param name="query">The query to execute.</param>
    /// <param name="cancellationToken">The cancellation token to use.</param>
    /// <typeparam name="T">The scalar return value of the query.</typeparam>
    /// <returns>The scalar return of the expression.</returns>
    IAsyncEnumerable<T> ExecuteAsync<T>(Expression<Func<IRelationContext, IQueryable<T>>> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the transaction.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to use.</param>
    /// <returns>An awaitable task.</returns>
    ValueTask CommitAsync(CancellationToken cancellationToken = default);
}
