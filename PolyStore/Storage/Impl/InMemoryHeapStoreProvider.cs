using System.Collections.Generic;

namespace PolyStore.Storage.Impl;

/// <summary>
/// An in-memory implementation of a heap storage structure.
/// </summary>
/// <typeparam name="T">The tuple type.</typeparam>
/// <remarks>
/// This is a proof-of-concept implementation. It maintains an ordered list of RIDs in
/// insertion order. It does not provide thread safety, persistence, or any of the other
/// implementation concerns that the architecture leaves open.
/// </remarks>
public sealed class InMemoryHeapStoreProvider<T>
{
    private readonly List<Rid> _rids = [];

    /// <summary>
    /// Gets the number of RIDs in the heap.
    /// </summary>
    public int Count => _rids.Count;

    /// <summary>
    /// Adds a RID to the heap, appending it to the end to preserve insertion order.
    /// </summary>
    /// <param name="rid">The RID to add.</param>
    /// <remarks>Adding a RID that is already present has no effect.</remarks>
    public void Add(Rid rid)
    {
        if (!_rids.Contains(rid))
        {
            _rids.Add(rid);
        }
    }

    /// <summary>
    /// Removes a RID from the heap.
    /// </summary>
    /// <param name="rid">The RID to remove.</param>
    /// <returns><c>true</c> if the RID was present and removed; otherwise, <c>false</c>.</returns>
    public bool Remove(Rid rid)
    {
        return _rids.Remove(rid);
    }

    /// <summary>
    /// Determines whether the heap contains the specified RID.
    /// </summary>
    /// <param name="rid">The RID to locate.</param>
    /// <returns><c>true</c> if the RID is present; otherwise, <c>false</c>.</returns>
    public bool Contains(Rid rid)
    {
        return _rids.Contains(rid);
    }

    /// <summary>
    /// Enumerates the RIDs in the heap in insertion order.
    /// </summary>
    /// <returns>An enumeration of the RIDs in the heap.</returns>
    /// <remarks>
    /// The enumeration reflects the state of the heap at the time it was obtained.
    /// Subsequent modifications to the heap do not affect an already-obtained enumeration.
    /// </remarks>
    public IEnumerable<Rid> EnumerateRids()
    {
        return _rids;
    }

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
    public IEnumerable<T> EnumerateTuples(ICanonicalTupleStore<T> store)
    {
        foreach (var rid in EnumerateRids())
        {
            if (store.TryGet(rid, out var tuple))
            {
                yield return tuple;
            }
        }
    }
}
