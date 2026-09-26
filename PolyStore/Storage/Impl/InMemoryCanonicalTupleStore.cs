using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using PolyStore.Storage;

namespace PolyStore.Storage.Impl;

/// <summary>
/// An in-memory implementation of <see cref="ICanonicalTupleStore{T}"/>.
/// </summary>
/// <typeparam name="T">The tuple type.</typeparam>
/// <remarks>
/// This is a proof-of-concept implementation. It does not provide thread
/// safety, persistence, or any of the other implementation concerns that
/// the architecture leaves open. RIDs are allocated sequentially starting
/// from zero.
/// </remarks>
public sealed class InMemoryCanonicalTupleStore<T> : ICanonicalTupleStore<T>
{
    private readonly Dictionary<ulong, T> _tuples = new();
    private ulong _nextRid;

    /// <inheritdoc />
    public int Count => _tuples.Count;

    /// <inheritdoc />
    public Rid Insert(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var rid = _nextRid++;
        _tuples[rid] = value;
        return new Rid(rid);
    }

    /// <inheritdoc />
    public bool TryGet(Rid rid, [MaybeNullWhen(false)] out T value)
    {
        return _tuples.TryGetValue(rid.Value, out value);
    }

    /// <inheritdoc />
    public void Delete(Rid rid)
    {
        _tuples.Remove(rid.Value);
    }
}
