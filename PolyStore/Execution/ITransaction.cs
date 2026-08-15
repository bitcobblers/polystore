using PolyStore.Core;

namespace PolyStore.Execution;

/// <summary>
/// Represents a data source for a relation of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the data in the source.</typeparam>
public interface ITransaction : IAsyncDisposable
{
    /// <summary>
    /// Inserts a new value into the source.
    /// </summary>
    /// <param name="source">The source to write to.</param>
    /// <param name="value">The value to insert.</param>
    void Insert<T>(ISource<T> source, T value);

    /// <summary>
    /// Inserts a new value into the source.
    /// </summary>
    /// <param name="source">The source to write to.</param>
    /// <param name="value">The value to insert.</param>
    void Update<T>(ISource<T> source, T value);

    /// <summary>
    /// Inserts a new value into the source.
    /// </summary>
    /// <param name="source">The source to write to.</param>
    /// <param name="value">The value to insert.</param>
    void Delete<T>(ISource<T> source, T value);

    /// <summary>
    /// Commits the transaction.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to use.</param>
    /// <returns>An awaitable task.</returns>
    ValueTask CommitAsync(CancellationToken cancellationToken = default);
}