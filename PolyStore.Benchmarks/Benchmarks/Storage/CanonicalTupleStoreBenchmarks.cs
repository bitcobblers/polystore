using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using PolyStore.Benchmarks.Fixtures;
using PolyStore.Benchmarks.Support;
using PolyStore.Storage.Impl;

namespace PolyStore.Benchmarks.Storage;

[MemoryDiagnoser]
[BenchmarkCategory("storage")]
public class CanonicalTupleStoreBenchmarks
{
    public static IEnumerable<int> Sizes => [DatasetSizes.Small, DatasetSizes.Medium];

    [ParamsSource(nameof(Sizes))]
    public int _size;

    private InMemoryCanonicalTupleStore<BenchTuple> _store = null!;
    private List<PolyStore.Storage.Rid> _rids = null!;
    private BenchTuple _tuple = null!;
    private PolyStore.Storage.Rid _knownRid;

    [GlobalSetup]
    public void Setup()
    {
        (_store, _rids) = DatasetGenerator.Build(_size);   // setup, not measured (I-SETUP)
        _tuple = DatasetGenerator.Create(0);
        _knownRid = _rids[0];
    }

    /// <summary>Pure read: resolve a known RID.</summary>
    [Benchmark]
    public bool TryGet()
    {
        return _store.TryGet(_knownRid, out _);
    }

    /// <summary>
    /// Write cycle: insert a tuple, then delete it. Self-contained and state-restoring
    /// (I-STATE). Measures the realistic insert+delete cost, including RID assignment.
    /// </summary>
    [Benchmark]
    public void InsertAndDelete()
    {
        var rid = _store.Insert(_tuple);
        _store.Delete(rid);
    }
}
