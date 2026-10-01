using BenchmarkDotNet.Attributes;

namespace PolyStore.Benchmarks.Rid;

[MemoryDiagnoser]
[BenchmarkCategory("rid")]
public class RidBenchmarks
{
    private PolyStore.Storage.Rid _a;
    private PolyStore.Storage.Rid _b;

    [GlobalSetup]
    public void Setup()
    {
        _a = new PolyStore.Storage.Rid();
        _b = new PolyStore.Storage.Rid();
    }

    [Benchmark]
    public PolyStore.Storage.Rid Create() => new PolyStore.Storage.Rid();

    [Benchmark]
    public bool Equality() => _a.Equals(_b);

    [Benchmark]
    public int Hashing() => _a.GetHashCode();
}
