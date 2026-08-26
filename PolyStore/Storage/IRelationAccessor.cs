using PolyStore.Core;
using PolyStore.Execution;

namespace PolyStore.Storage;

/// <summary>
/// Defines the realization of a relation.
/// </summary>
/// <remarks>
/// A relation accessor defines low level operations that can be performed
/// on a relation.
/// </remarks>
public interface IRelationAccessor<T>
{
    /// <summary>
    /// Gets the underlying relation.
    /// </summary>
    IRelation<T> Relation { get; }

    /// <summary>
    /// Gets the supported capabilities of the realization.
    /// </summary>
    RelationAccessorCapabilities Capabilities { get; }

    /// <summary>
    /// Applies a change to the realization.
    /// </summary>
    /// <param name="change">The change to apply.</param>
    /// <param name="transaction">The current transaction.</param>
    /// <param name="cancellationToken">The cancellation token to use.</param>
    /// <returns>An awaitable task.</returns>
    ValueTask ApplyAsync(
        RelationChange<T> change,
        ITransaction transaction,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines an accessor that supports scanning the relation.
/// </summary>
/// <typeparam name="T">The type of the relation.</typeparam>
public interface IScanAccessor<T> : IRelationAccessor<T>
{
    /// <summary>
    /// Scans the relation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to use.</param>
    /// <returns>A collection of objects in the relation.</returns>
    IAsyncEnumerable<T> ScanAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines an accessor that supports reading the relation from an arbitrary position.
/// </summary>
/// <typeparam name="T">The type of the relation.</typeparam>
public interface ISeekAccessor<T> : IRelationAccessor<T>
{
    /// <summary>
    /// Seeks items from the relation at a specific location.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to use.</param>
    /// <returns>A collection of objects in the relation.</returns>
    IAsyncEnumerable<T> SeekAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines an accessor that supports writing changes to the relation.
/// </summary>
/// <typeparam name="T">The type of the relation.</typeparam>
public interface IWriteAccessor<T> : IRelationAccessor<T>
{
    /// <summary>
    /// Writes a change to the relation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to use.</param>
    ValueTask WriteAsync(CancellationToken cancellationToken = default);
}