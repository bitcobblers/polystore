using System.Collections.Generic;

namespace PolyStore.Storage.Impl;

/// <summary>
/// An in-memory implementation of <see cref="IHeap{T}"/>.
/// </summary>
/// <typeparam name="T">The tuple type.</typeparam>
/// <remarks>
/// This is a proof-of-concept implementation. It maintains an ordered list of RIDs in
/// insertion order. It does not provide thread safety, persistence, or any of the other
/// implementation concerns that the architecture leaves open.
/// </remarks>
public sealed class InMemoryHeap<T> : IHeap<T>
{
    private readonly List<Rid> _rids = [];

    /// <inheritdoc />
    public int Count => _rids.Count;

    /// <inheritdoc />
    public void Add(Rid rid)
    {
        if (!_rids.Contains(rid))
        {
            _rids.Add(rid);
        }
    }

    /// <inheritdoc />
    public bool Remove(Rid rid)
    {
        return _rids.Remove(rid);
    }

    /// <inheritdoc />
    public bool Contains(Rid rid)
    {
        return _rids.Contains(rid);
    }

    /// <inheritdoc />
    public IEnumerable<Rid> EnumerateRids()
    {
        return _rids.ToArray();
    }

    /// <inheritdoc />
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
