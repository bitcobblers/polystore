using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using PolyStore.Benchmarks.Fixtures;
using PolyStore.Benchmarks.Support;
using PolyStore.Storage.Impl;

namespace PolyStore.Benchmarks.Storage;

[MemoryDiagnoser]
[BenchmarkCategory("storage")]
public class HeapStoreBenchmarks
{
    // Heap setup is O(n²) (Add does an O(n) duplicate check), so the heap uses the small
    // 1k/10k matrix — a 100k heap build would be ~5×10⁹ comparisons per method, and a 1M
    // build ~5×10¹¹ (see §5.5/§5.6.3). The O(n)-setup canonical store uses the larger one.
    public static IEnumerable<int> Sizes => [DatasetSizes.Tiny, DatasetSizes.Small];

    [ParamsSource(nameof(Sizes))]
    public int _size;
    private InMemoryCanonicalTupleStore<BenchTuple> _store = null!;
    private InMemoryHeapStoreProvider<BenchTuple> _heap = null!;
    private List<PolyStore.Storage.Rid> _rids = null!;
    private PolyStore.Storage.Rid _memberRid;   // guaranteed present in the heap (for Contains)
    private PolyStore.Storage.Rid _churnRid;    // guaranteed absent (for the Add/Remove cycle)

    [GlobalSetup]
    public void Setup()
    {
        (_store, _rids) = DatasetGenerator.Build(_size);            // setup, not measured (I-SETUP)
        _heap = new InMemoryHeapStoreProvider<BenchTuple>();
        foreach (var rid in _rids)
        {
            _heap.Add(rid);
        }

        _memberRid = _rids[_size / 2];                               // mid-list: representative, not best case
        _churnRid = new PolyStore.Storage.Rid();                     // not in the heap
    }

    /// <summary>
    /// Pure read: membership test at a representative (mid-list) position. A best/worst-case
    /// spread (_rids[0] vs. _rids[^1]) could be added as extra methods if position
    /// sensitivity matters.
    /// </summary>
    [Benchmark]
    public bool Contains() => _heap.Contains(_memberRid);

    /// <summary>
    /// Steady-state membership/churn cycle: add a RID, then remove it. Self-contained and
    /// state-restoring (I-STATE). Measures the COMBINED cost of Add + Remove; it does not
    /// isolate Add (an isolated Add benchmark is future work — §9 OQ2).
    /// </summary>
    [Benchmark]
    public void AddAndRemove()
    {
        _heap.Add(_churnRid);
        _heap.Remove(_churnRid);
    }

    /// <summary>Pure scan: enumerate all RIDs. Counts the yielded elements (cheapest
    /// observable consumption — no per-element hashing, which is §5.6.1's job).</summary>
    [Benchmark]
    public long EnumerateRids()
    {
        long count = 0;
        foreach (var _ in _heap.EnumerateRids())
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// Pure scan + resolution: enumerate RIDs and resolve each through the canonical
    /// store (the heap→canonical bridge). Accumulates a checksum to prevent elision.
    /// </summary>
    [Benchmark]
    public long EnumerateTuples()
    {
        long checksum = 0;
        foreach (var tuple in _heap.EnumerateTuples(_store))
        {
            checksum += tuple.Id;
        }

        return checksum;
    }
}
