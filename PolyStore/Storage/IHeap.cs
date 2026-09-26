using System.Collections.Generic;

namespace PolyStore.Storage;

/// <summary>
/// Defines a heap access path for a relation of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The tuple type.</typeparam>
/// <remarks>
/// A heap is an access path that provides sequential traversal of a relation's tuples.
/// Conceptually it is an ordered list of RIDs in insertion order. The tuples themselves
/// remain in the canonical store; the heap only tracks their RIDs. A RID obtained from
/// the heap can be resolved to its complete tuple through the canonical store.
///
/// A heap is an access path like any other. It does not own the authoritative tuple
/// representation, and a relation is not assumed to implicitly have a heap.
/// </remarks>
public interface IHeap<T>
{
    /// <summary>
    /// Gets the number of RIDs in the heap.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Adds a RID to the heap, appending it to the end to preserve insertion order.
    /// </summary>
    /// <param name="rid">The RID to add.</param>
    /// <remarks>Adding a RID that is already present has no effect.</remarks>
    void Add(Rid rid);

    /// <summary>
    /// Removes a RID from the heap.
    /// </summary>
    /// <param name="rid">The RID to remove.</param>
    /// <returns><c>true</c> if the RID was present and removed; otherwise, <c>false</c>.</returns>
    bool Remove(Rid rid);

    /// <summary>
    /// Determines whether the heap contains the specified RID.
    /// </summary>
    /// <param name="rid">The RID to locate.</param>
    /// <returns><c>true</c> if the RID is present; otherwise, <c>false</c>.</returns>
    bool Contains(Rid rid);

    /// <summary>
    /// Enumerates the RIDs in the heap in insertion order.
    /// </summary>
    /// <returns>An enumeration of the RIDs in the heap.</returns>
    /// <remarks>
    /// The enumeration reflects the state of the heap at the time it was obtained.
    /// Subsequent modifications to the heap do not affect an already-obtained enumeration.
    /// </remarks>
    IEnumerable<Rid> EnumerateRids();

    /// <summary>
    /// Enumerates the tuples in the heap by resolving each RID through the canonical store.
    /// </summary>
    /// <param name="store">The canonical tuple store used to resolve RIDs to complete tuples.</param>
    /// <returns>An enumeration of the tuples in the heap, in insertion order.</returns>
    /// <remarks>
    /// The heap does not own the tuple data; it only tracks RIDs. This method bridges the
    /// heap to the canonical store, which is the authoritative tuple representation.
    /// RIDs that do not resolve to a tuple in the canonical store are skipped.
    /// </remarks>
    IEnumerable<T> EnumerateTuples(ICanonicalTupleStore<T> store);
}
