namespace PolyStore.Core;

/// <summary>
/// Defines a propagator of changes.
/// </summary>
public interface IChangePropagator
{
    /// <summary>
    /// Propagates the change.
    /// </summary>
    /// <param name="change">The change to propagate.</param>
    /// <param name="cancellationToken">The cancellation token to use.</param>
    /// <returns>An awaitable task.</returns>
    ValueTask<IReadOnlyList<IRelationChange>> PropagateAsync(
        IRelationChange change,
        CancellationToken cancellationToken = default);
}