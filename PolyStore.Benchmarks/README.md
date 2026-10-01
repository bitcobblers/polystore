# PolyStore.Benchmarks

A small, extensible BenchmarkDotNet harness for PolyStore. It benchmarks the
**implemented** storage surface (the `Rid` currency, the canonical tuple store, and the
heap store) through their public types, so the same shape can later be pointed at a
B-tree access path or a second provider without harness changes.

This is a foundation, not a performance-testing platform. It has no base class, no
configuration layer, no result database, and no CI gating on absolute numbers.

The design and rationale live in [`docs/design/benchmarking.md`](../docs/design/benchmarking.md).

## Requirements

- .NET 10 (`global.json` pins the SDK).
- BenchmarkDotNet `0.15.8` (pinned for reproducible results).

## Running

Release is the only meaningful configuration (both the harness and `PolyStore` are
optimized in Release). `dotnet run` passes everything after `--` straight to BDN.

```bash
# List every benchmark (sanity check; no measurement).
dotnet run --project PolyStore.Benchmarks -c Release -- --list flat

# Run everything (default matrices: heap 1k/10k, canonical 10k/100k).
dotnet run --project PolyStore.Benchmarks -c Release

# Run one subject only. BDN's --filter is a GLOB matched against the full benchmark
# name (namespace.Class.Method); use * as a wildcard. A bare name will not match.
dotnet run --project PolyStore.Benchmarks -c Release -- --filter "*CanonicalTupleStore*"

# Run one method.
dotnet run --project PolyStore.Benchmarks -c Release -- --filter "*TryGet"

# Export a machine-readable CSV (the default run already produces HTML + GitHub markdown).
dotnet run --project PolyStore.Benchmarks -c Release -- --exporters csv
# (BDN 0.15.8 takes a single exporter per run; use --exporters json for JSON.)
```

Results (HTML/CSV/JSON/logs) are written to the gitignored `artifacts/` directory.
Nothing is committed.

## Reading results

- **Throughput** (default): the method is invoked in a tight loop; BDN reports
  `Mean`/`Error`/`StdDev` over the aggregated time.
- **Allocations**: `[MemoryDiagnoser]` adds the `Allocated` (bytes) and GC-generation
  columns.
- **A/B comparison** (within a single run): mark one method `[Baseline = true]` in a class
  and read the `Ratio` column for the others. BDN computes the ratio **within the class**
  and **within the run** — it does not persist a baseline for a later run. Cross-commit
  comparison is done externally (export CSV, diff against a committed baseline) and is not
  part of this foundation.

## Adding a benchmark

No registration, base class, or harness change is required:

1. **Pick the subsystem folder** under `Benchmarks/` (create it if new).
2. **Add a class** named `<Subject>Benchmarks` in namespace `PolyStore.Benchmarks.<Subsystem>`.
3. **Decorate the class** with `[MemoryDiagnoser]` and `[BenchmarkCategory("<subsystem>")]`;
   if it needs a dataset, add a size field with the member-level
   `[ParamsSource(nameof(Sizes))]` attribute backed by
   `public static IEnumerable<int> Sizes => [DatasetSizes.Small, DatasetSizes.Medium];`.
4. **Build the fixture in `[GlobalSetup]`** (e.g. `DatasetGenerator.Build(_size)`).
   Never build data inside a `[Benchmark]` method — setup is not measured.
5. **Write `[Benchmark]` methods** that contain only the operation under test, are
   self-contained (state-restoring), and return/accumulate a value if the work could
   otherwise be elided.
6. **Run it:** `dotnet run --project PolyStore.Benchmarks -c Release -- --filter "*<Subject>*"`.

The only shared code a new benchmark may touch is `Fixtures/DatasetGenerator`,
`Fixtures/BenchTuple`, and `Support/DatasetSizes`, and only if it needs a different
fixture or size.

## Layout

```
PolyStore.Benchmarks/
├── PolyStore.Benchmarks.csproj
├── Program.cs                     # BDN entry point; passes CLI args through
├── README.md
├── Fixtures/
│   ├── BenchTuple.cs              # the relation value type used by storage benchmarks
│   └── DatasetGenerator.cs        # deterministic dataset construction (setup, not measured)
├── Support/
│   └── DatasetSizes.cs            # the shared, deliberately short size matrix
└── Benchmarks/
    ├── Rid/
    │   └── RidBenchmarks.cs
    ├── Storage/
    │   ├── CanonicalTupleStoreBenchmarks.cs
    │   └── HeapStoreBenchmarks.cs
    ├── Execution/                 # (reserved) end-to-end transaction/executor benchmarks
    └── Query/                     # (reserved) relational query benchmarks
```
