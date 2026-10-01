using System.Collections.Generic;
using PolyStore.Storage.Impl;

namespace PolyStore.Benchmarks.Fixtures;

/// <summary>
/// Deterministic dataset construction for benchmarks. This is setup work: it is invoked
/// from [GlobalSetup] and is never part of a measured operation.
/// </summary>
public static class DatasetGenerator
{
    /// <summary>Creates a single tuple with a stable, index-derived value.</summary>
    public static BenchTuple Create(int index) => new()
    {
        Id = index,
        Name = $"name-{index}",
        Amount = index * 10m,
    };

    /// <summary>
    /// Builds a canonical store of <paramref name="count"/> tuples and returns the store
    /// plus the RIDs in insertion order. Deterministic for a given count.
    /// </summary>
    public static (InMemoryCanonicalTupleStore<BenchTuple> Store, List<PolyStore.Storage.Rid> Rids) Build(int count)
    {
        var store = new InMemoryCanonicalTupleStore<BenchTuple>();
        var rids = new List<PolyStore.Storage.Rid>(count);

        for (var i = 0; i < count; i++)
        {
            rids.Add(store.Insert(Create(i)));
        }

        return (store, rids);
    }
}
