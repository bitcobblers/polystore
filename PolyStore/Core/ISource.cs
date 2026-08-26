namespace PolyStore.Core;

/// <summary>
/// Represents a data source for a relation of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the data in the source.</typeparam>
/// <remarks>
/// Sources are are mutable ingress points. Unlike other objects they can be
/// written to, updated, and deleted depending on their provider's capabilities.
/// </remarks>
public interface ISource<in T> : IRelation<T>
{
    /// <summary>
    /// Inserts a new value into the source.
    /// </summary>
    /// <param name="value">The value to insert.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other asynchronous operations to cancel the current operation.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask InsertAsync(T value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing value in the source.
    /// </summary>
    /// <param name="value">The value to update.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other asynchronous operations to cancel the current operation.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask UpdateAsync(T value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a value from the source.
    /// </summary>
    /// <param name="value">The value to delete.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other asynchronous operations to cancel the current operation.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask DeleteAsync(T value, CancellationToken cancellationToken = default);
}
