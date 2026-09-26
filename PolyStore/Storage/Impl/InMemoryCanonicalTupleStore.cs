using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace PolyStore.Storage.Impl;

/// <summary>
/// An in-memory implementation of <see cref="ICanonicalTupleStore{T}"/>.
/// </summary>
/// <typeparam name="T">The tuple type.</typeparam>
/// <remarks>
/// This is a proof-of-concept implementation. It does not provide thread
/// safety, persistence, or any of the other implementation concerns that
/// the architecture leaves open.
/// </remarks>
public sealed class InMemoryCanonicalTupleStore<T> : ICanonicalTupleStore<T>
{
    private readonly Dictionary<Rid, T> _tuples = new();

    /// <inheritdoc />
    public int Count => _tuples.Count;

    /// <inheritdoc />
    public Rid Insert(T value)
    {
        var rid = new Rid();
        _tuples[rid] = value;

        return rid;
    }

    /// <inheritdoc />
    public bool TryGet(Rid rid, [MaybeNullWhen(false)] out T value)
    {
        return _tuples.TryGetValue(rid, out value);
    }

    /// <inheritdoc />
    public void Delete(Rid rid)
    {
        _tuples.Remove(rid);
    }
}
