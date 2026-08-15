using PolyStore.Core;
using PolyStore.Execution;

namespace PolyStore.Storage;

/// <summary>
/// Defines a storage provider for relation.
/// </summary>
/// <remarks>
/// Storage providers are responsible for handling the backing
/// storage used by the relation.
/// </remarks>
public interface IStorageProvider
{
    /// <summary>
    /// Gets the name of the provider.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Checks if the relation can be realized.
    /// </summary>
    /// <param name="relation">The relation to check.</param>
    /// <returns>True if the relation can be realized.</returns>
    bool Supports(IRelation relation);

    /// <summary>
    /// Opens the relation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to use.</param>
    /// <returns></returns>
    ValueTask<IRelationAccessor<T>> OpenAsync<T>(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new realization.
    /// </summary>
    /// <param name="relation">The relation to realize.</param>
    /// <param name="options">The options for the realization.</param>
    /// <param name="cancellationToken">The cancellation token to use.</param>
    /// <returns></returns>
    ValueTask<IRelationAccessor<T>> CreateAsync<T>(
        IRelation relation,
        StoreOptions options,
        CancellationToken cancellationToken = default);
}