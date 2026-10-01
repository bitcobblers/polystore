# Benchmarking Foundation

**Status:** Proposal
**Date:** 2026-09-30
**Revised:** — (initial proposal)
**Revised:** 2026-09-30 — addresses design-reviewer findings DR-1…DR-9 and performance-reviewer findings PR-1…PR-6. Correctness/clarity fixes only; the architecture, project location, scope, and the small initial benchmark set are unchanged.
**Revised:** 2026-09-30 (cycle 2) — addresses the reviewers' new BDN 0.15.x API/semantics findings: the `BenchmarkRunner.Run` entry-point overload, `[MemoryDiagnoser]` (not `[MemoryMetric]`), within-class `[Baseline]`/`Ratio` scoping, `[GlobalSetup]` per-case semantics, `[IterationSetup]`/`[IterationCleanup]`, the size-restricting CI smoke filter, O(n²) setup-cost wording, and the `ReturnValueValidator` note. Correctness-only; architecture, project location, scope, and the initial benchmark set are unchanged.
**Revised:** 2026-09-30 (cycle 3, user-directed) — six focused corrections: (1) §5.9 A/B baseline is now two explicit methods in one class sharing the size parameter (not a `[Params]`-over-strategy factory); (2) baselines removed from the initial set (`Create`, `TryGet`, `Contains`) — BDN baselines are reserved for genuine A/B comparisons (§5.9); (3) subsystem-specific size matrices (heap 1k/10k, canonical 10k/100k + 1M opt-in) with a new `Tiny` size, resolving OQ5; (4) `AddAndRemove` reframed as a combined Add+Remove steady-state benchmark, not evidence about `Add` alone; (5) `EnumerateRids` now counts elements (no per-element hashing); (6) `InvariantGlobalization` removed (reproduce the product's runtime semantics). The benchmark set, architecture, project location, and scope are unchanged.

**Scope:** This document designs a *repeatable, local-first benchmarking project and
harness* for PolyStore: where it lives, how benchmarks are organized, how fixtures and
datasets are built and separated from measured work, how to parameterize and measure, how
to run locally, how to persist and (optionally) compare results, and a small initial set
of benchmarks that validate the harness against the only implemented surface. It is **not**
a load-testing, soak, concurrency, or multi-process performance platform, and it does not
design the executor, planner, or any storage implementation. Those are `ARCHITECTURE.md`
§28 open areas that this harness will eventually *measure* once they exist.

---

## Executive Summary

PolyStore's architecture defers performance decisions to measurement: `ARCHITECTURE.md`
names canonical tuple representation, RID lookup cost, execution granularity, and
allocation as areas that require measurement before design decisions are made. Today
there is no way to measure any of this — no benchmark project, no dataset fixtures, no
repeatable invocation path, no place for results. This document establishes the smallest
coherent foundation that makes those measurements possible and repeatable.

**Decision.** A dedicated `PolyStore.Benchmarks` executable project using
BenchmarkDotNet 0.15.8, referencing the PolyStore library only. It is a local-first,
repeatable measurement harness: one command to run, results persisted to the
gitignored `artifacts/` directory, and no changes to PolyStore's public API.

**Key design choices.**

- *No shared base class.* Benchmark classes are self-contained; shared logic lives in
  small static helpers (`DatasetGenerator`, `BenchTuple`, `DatasetSizes`).
- *Setup is never measured.* Dataset construction happens in `[GlobalSetup]`; measured
  methods perform only the operation under test.
- *Measure the public surface.* Benchmarks exercise the storage API as a consumer would,
  staying provider- and access-path-independent so alternative implementations can be
  compared when they land.
- *State-restoring writes.* Write benchmarks are self-contained cycles (insert+delete,
  add+remove) that restore state each iteration.
- *Per-subsystem size matrices.* The O(n²)-setup heap uses 1k/10k; the O(n)-setup
  canonical store uses 10k/100k, with a 1M opt-in for deep runs.
- *Honest measurement.* `[MemoryDiagnoser]` reports allocations; BDN's process isolation
  and warmup handle JIT effects; the harness reproduces the product's runtime semantics
  (no `InvariantGlobalization`).
- *Baselines only for genuine A/B comparisons.* The initial set has no baselines;
  within-run `[Baseline]` ratios are reserved for Phase 3.

**Scope boundaries.** Not a load-testing, soak, concurrency, or multi-process platform;
does not design the executor, planner, or any storage implementation. CI integration is
an optional, non-gating smoke job; load testing, dashboards, and result databases are
out of scope.

**Implementation phases.** Phase 0: project skeleton + one RID micro-benchmark (validates
the toolchain). Phase 1: fixtures + the storage benchmark set. Phase 2: result
persistence + optional non-gating CI smoke. Phase 3 (future, separate): A/B comparison,
end-to-end streaming, concurrency — once those engine features exist.

**Unresolved questions.** None block the foundation: the cross-commit baseline format
and regression threshold, isolated single-write benchmarks, the end-to-end benchmark
shape, and the concurrency harness are each deferred until the relevant engine area
lands.

---

## 1. Problem Statement

PolyStore's architecture deliberately defers performance decisions to measurement.
`ARCHITECTURE.md` §26 ("Performance Philosophy") lists the areas that *require*
measurement — canonical tuple representation, RID lookup cost, row-versus-batch execution,
join implementations, access-path maintenance, write amplification, allocation — and states
that the architecture should "make efficient implementations possible without claiming in
advance which implementation will prove optimal." `ARCHITECTURE.md` §16 explicitly calls the
row-at-a-time vs. vectorized question "an implementation and *benchmarking* question." The
companion design `docs/design/tuple-representation.md` §8/§9 prescribes concrete remedies
("if benchmarks show boxing dominates…") that can only be chosen *after* a measurement
exists.

Today there is no way to measure any of this. There is no benchmark project, no dataset
fixtures, no repeatable invocation path, and no place for results. The only "performance"
signal is incidental observation. That is a gap: the next several design decisions
(canonical store encoding, RID representation, materialization placement, execution
granularity, access-path payload trade-offs) are all explicitly conditioned on measured
data, and none of them can be made responsibly without a harness.

This document establishes the smallest coherent foundation that makes those measurements
possible and repeatable. Success for the harness means:

- A developer can add a benchmark in a handful of lines and run it with one command,
  without learning or duplicating harness infrastructure.
- The same benchmark, on the same machine, with the same inputs, produces comparable
  numbers across runs and across commits.
- Measured work is cleanly separated from setup work (dataset construction is never
  measured).
- The harness is provider- and access-path-independent: it benchmarks the public
  storage/execution surface, not one implementation's internals, and it can A/B compare
  alternative implementations when they land.
- The initial benchmark set exercises the only implemented surface (the two in-memory
  stores and `Rid`) and is small enough that its *measured* work runs in seconds to minutes
  (setup is bounded separately — §5.5/§5.6.3).

---

## 2. Architectural Context

Benchmarking sits *below* the logical/physical boundary and measures the physical layer
through its public surface. It must respect the same invariants the engine does:

- **Logical/physical separation.** Benchmarks target the physical artifacts — the
  canonical tuple store, the heap access path, and (later) the executor — through their
  public surface. The initial set benchmarks the **concrete in-memory implementations**
  (`InMemoryCanonicalTupleStore<T>`, `InMemoryHeapStoreProvider<T>`) and `Rid`; it does not
  reach into private fields or depend on one implementation's layout. When a second provider
  or a B-tree path lands, the *same* benchmark shape is pointed at that concrete type without
  changing the harness (§5.9).
- **The RID model.** `Rid` is the logical tuple identifier and the currency of the storage
  layer (`ARCHITECTURE.md` §5). RID creation, equality, and hashing are the atomic
  operations every store and access path pays, so a RID micro-baseline is a useful
  reference point against which store costs can be interpreted.
- **Access paths.** The heap is the only implemented access path; the B-tree path
  (`BTreePath<T>.Column`/`Include`) is a no-op stub. The harness must be able to compare a
  future B-tree path against the heap (e.g. "scan + materialize via heap" vs. "scan +
  materialize via B-tree") without new infrastructure — this is the A/B comparison
  requirement (§5.9).
- **Planner/executor (still open).** `ITransaction` is an unimplemented interface (no
  implementation exists), and the DML extensions and `DatabaseContext.From*` are
  `NotImplementedException` stubs. End-to-end query and
  mutation benchmarks therefore *cannot exist yet*; the harness reserves a place for them
  (`Benchmarks/Execution/`, `Benchmarks/Query/`) but does not pre-commit to any executor
  API. This is a deliberate constraint, not a gap to work around.
- **Provider independence.** The in-memory store and heap are the only implementations.
  Benchmarking them directly is correct *today*; the harness must not grow an
  "IBenchmarkTarget" abstraction layer to anticipate a second provider (scope control,
  `AGENTS.md`). Comparison is achieved by pointing the same benchmark shape at a different
  concrete type, not by an indirection layer.
- **Terminology.** The existing types already use "store" (`InMemoryCanonicalTupleStore`,
  `InMemoryHeapStoreProvider`); this design does not rename them. New benchmark types use
  the established "Benchmarks" suffix and the `PolyStore.Benchmarks` namespace.

---

## 3. Requirements and Constraints

### 3.1 From `ARCHITECTURE.md`

- **R1 — Measurement is a first-class concern.** Performance questions (canonical
  representation, RID lookup, row vs. batch, joins, write amplification, allocation) are
  explicitly deferred to measurement (§26, §16). The harness must make them measurable.
- **R2 — Provider-independence.** The logical architecture is provider-independent (§15,
  §25); the harness must not couple to one physical implementation where a comparison
  surface exists.
- **R3 — A/B comparison of strategies.** Alternative access paths and execution strategies
  must be comparable (§6, §16, §26).

### 3.2 From `AGENTS.md`

- **C1 — Scope control.** Abstractions only where a current boundary requires them; no
  speculative framework. A small, coherent, extensible foundation is the target.
- **C2 — Terminology.** Relation, Source, Accessor, StorageProvider, Access Path. Do not
  rename the existing "store" types.
- **C3 — Async streaming.** Future end-to-end benchmarks must stream
  (`IAsyncEnumerable<T>`) and support cancellation; they must not buffer into
  `Task<List<T>>`.
- **C4 — Fail explicitly.** A benchmark that cannot measure what it claims to measure must
  say so, not silently degrade.
- **C5 — Generics scoped to the value type.** Benchmark fixtures are generic over the
  relation value type `T`, matching the engine.
- **C6 — Immutable value types; expression bodies; no unnecessary inheritance.**
- **C7 — Hot-path awareness.** The harness must not introduce per-tuple reflection, hidden
  buffering, or repeated compilation into the *engine*; and benchmarks must report
  allocations so such regressions are visible.
- **C8 — Tests mirror layout.** `PolyStore.Tests` mirrors `PolyStore`; the benchmark
  project should follow a similarly clean, predictable layout.

### 3.3 Derived requirements

- **D1 — Repeatable.** Same benchmark + same machine + same inputs ⇒ comparable numbers.
  Datasets must be deterministic (seeded/constructed, not wall-clock or ambient).
- **D2 — Setup is not measured.** Dataset construction, store population, and RID
  generation belong in setup, never in the measured operation.
- **D3 — Initial scope is the implemented surface only.** The two in-memory stores and
  `Rid`. No benchmarks of stubbed APIs.
- **D4 — Manageable matrix.** Parameterization (dataset size, etc.) must not produce an
  unmanageable combinatorial explosion; distinguish a fast "smoke" set from a full "sweep."
- **D5 — Useful metrics.** Throughput (ops/s), latency (mean/error/stddev), and
  allocations (bytes + GC generations).
- **D6 — One-command local run.** `dotnet run --project PolyStore.Benchmarks -c Release --
  <filter>` with no harness knowledge required to add a benchmark.
- **D7 — Results persist locally.** Exported to a gitignored location by default; cross-run
  comparison supported; cross-commit comparison is a small, optional extension.
- **D8 — CI is non-gating.** Shared-runner noise means CI must not gate on absolute
  numbers; at most a generous, non-gating smoke check.
- **D9 — Release-only.** Benchmarks must run against optimized code; the harness must make
  that the default and detect the wrong configuration.
- **D10 — Warning-clean in Release.** `Directory.Build.props` sets
  `TreatWarningsAsErrors` in Release; the benchmark project must compile warning-free.

---

## 4. Alternatives Considered

### A. BenchmarkDotNet in a dedicated benchmark project (recommended)

A new `PolyStore.Benchmarks` executable project referencing `PolyStore`, using
BenchmarkDotNet (BDN) as the engine. Benchmarks are plain C# classes with BDN attributes;
fixtures are small static helpers; results export to CSV/JSON/HTML under the gitignored
`artifacts/` directory.

**Trade-offs:**

- *Complexity:* Low. One project, one dependency, a handful of classes. BDN handles
  warmup, JIT, iteration, statistics, and export — none of which we would want to build.
- *Correctness:* BDN is the de-facto standard for .NET micro-/macro-benchmarks; its
  warmup/throughput model and statistical aggregation are well understood.
- *Performance:* By default BDN runs each benchmark case (a method at a given parameter
  combination) in its own isolated child process — a fresh optimized (Release) runtime with
  its own JIT and GC heap — while a host process builds, launches, and collects results.
  Per-benchmark warmup absorbs JIT/cold-start effects, and the process isolation keeps JIT
  and GC effects from leaking across benchmarks — exactly what we need.
- *Extensibility:* New benchmark areas are new classes in new folders; A/B comparison is a
  built-in BDN feature (`[Baseline]`, ratio columns).
- *Compatibility:* BDN 0.15.x supports .NET 10 (the repo's target). No architectural
  change to `PolyStore` is required — benchmarks consume the public surface only.

**Verdict:** Recommended.

### B. A different framework (NBench, or ad-hoc manual timing)

NBench or hand-rolled `Stopwatch` loops inside the test project.

**Trade-offs:**

- *Complexity:* Ad-hoc timing is trivial to start but forces us to re-implement warmup,
  iteration, statistics, and export — precisely the machinery BDN provides. NBench is
  lighter than BDN but has a smaller ecosystem, weaker .NET 10 story, and less
  allocation/GC reporting.
- *Correctness:* Manual timing is highly susceptible to the exact pitfalls we are trying
  to avoid (JIT, GC, warmup, first-touch).
- *Extensibility:* No built-in A/B ratio, no standard exporters, no category filtering.
- *Compatibility:* We would own more of the measurement stack, increasing maintenance and
  the risk of subtly wrong numbers.

**Verdict:** Rejected. There is no compelling technical reason to avoid BDN; it is the
default the feature request names and the strongest fit.

### C. No dedicated project — benchmarks in the test project

Add `[Benchmark]` classes to `PolyStore.Tests` and run them via the xunit host.

**Trade-offs:**

- *Complexity:* Avoids a new project, but couples benchmarking to the test harness and the
  xunit runner.
- *Correctness:* The test runner is not a benchmarking engine; we would still need BDN,
  and running BDN through xunit is unsupported and fragile.
- *Performance:* `dotnet test` builds in Debug by default and mixes benchmarks into the
  test run, making it slow and noisy; it also risks running benchmarks in an unoptimized
  configuration.
- *Extensibility:* Test discovery, filters, and CI are all oriented to tests, not
  benchmarks; we would fight the tooling.

**Verdict:** Rejected. Benchmarks have different lifecycle, configuration, and reporting
needs than tests; a dedicated project is the clean boundary and costs very little.

### D. A full performance-testing platform

A load/soak/concurrency harness, result database, dashboards, regression gating, and
multi-process scaling.

**Trade-offs:**

- *Complexity:* Very high; a platform, not a foundation.
- *Timing:* PolyStore has no executor, no transactions, no I/O, and no concurrency model
  yet (`ARCHITECTURE.md` §28). Most of a platform's surface would have nothing to measure.
- *Scope:* Directly violates C1 (scope control) and the feature request's explicit
  instruction to "avoid designing a large performance-testing platform before PolyStore has
  enough functionality to justify it."

**Verdict:** Rejected now. The harness is designed so that a load/soak/concurrency harness
can be *added later* as a separate concern (§5.10, §7) without rework, but it is not built
now.

---

## 5. Recommended Design

### 5.1 Project location and solution membership

New project at the repository root, a sibling of `PolyStore` and `PolyStore.Tests`:

```
polystore/
├── PolyStore/                 (library, unchanged)
├── PolyStore.Tests/           (xunit, unchanged)
├── PolyStore.Benchmarks/      (NEW — benchmark executable)
├── HelloWorld/                (sample, unchanged)
├── PolyStore.slnx             (add the new project)
└── ...
```

`PolyStore.slnx` gains one line (top-level, not under `/samples/`, since it is a
first-class development tool rather than a sample):

```xml
<Solution>
  <Folder Name="/samples/">
    <Project Path="HelloWorld/HelloWorld.csproj" />
  </Folder>
  <Project Path="PolyStore.Tests/PolyStore.Tests.csproj" />
  <Project Path="PolyStore.Benchmarks/PolyStore.Benchmarks.csproj" />
  <Project Path="PolyStore/PolyStore.csproj" />
</Solution>
```

`PolyStore.Benchmarks/PolyStore.Benchmarks.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <OutputType>Exe</OutputType>
        <IsPackable>false</IsPackable>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="BenchmarkDotNet" Version="0.15.8" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\PolyStore\PolyStore.csproj" />
    </ItemGroup>

</Project>
```

Decisions:

- **Executable, not a library.** BDN is driven from a `Main`; an `Exe` is the natural
  host. `IsPackable=false` — it is a development tool, not a distributable.
- **References `PolyStore` only.** It does not reference `PolyStore.Tests` or `HelloWorld`;
  fixtures are self-contained (§5.4) so the benchmark project has no dependency on the
  aspirational sample.
- **No `InvariantGlobalization`.** The benchmark process should reproduce the runtime
  semantics of the product being measured. PolyStore is not documented as running under
  invariant globalization, so the harness does not force it — this matters especially once
  benchmarks touch string keys, comparisons, ordering, predicates, collation, or
  serialization. If a future benchmark needs it, add it with a documented PolyStore
  rationale, not a generic "BDN recommends it" note.
- **Inherits `ImplicitUsings` and `Nullable` from `Directory.Build.props`.** These are set
  repository-wide (`ImplicitUsings=disable`, `Nullable=enable`), so the csproj does not
  restate them.
- **BDN pinned to a stable version** (`0.15.8`, the latest stable as of this writing;
  `0.16.0-preview.*` is deliberately not used). Pinning keeps results reproducible and
  upgrades deliberate.
- **Release is the only meaningful configuration.** The SDK sets `Optimize=true` in
  Release, and `PolyStore.csproj` also sets `Optimize=true` in Release, so both the
  harness and the library under test are optimized. `dotnet run -c Release` is the
  supported invocation (D9). Running in Debug produces misleading numbers and is
  discouraged (§5.7).

### 5.2 Project layout

The layout mirrors `PolyStore`'s subsystem folders so that a benchmark for a given
subsystem is found where the reader expects, and it scales as new subsystems appear:

```
PolyStore.Benchmarks/
├── PolyStore.Benchmarks.csproj
├── Program.cs                     # BDN entry point; passes CLI args through
├── README.md                      # how to add a benchmark, run, and read results
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

- **`Benchmarks/` is organized by subsystem** (`Rid/`, `Storage/`, later `Execution/`,
  `Query/`), matching `PolyStore`'s `Storage/`, `Execution/`, etc. This is the growth
  axis: a new subsystem is a new folder, and a new benchmark within a subsystem is a new
  class.
- **`Fixtures/` holds the relation value type and dataset construction.** These are shared
  by every storage benchmark and are the only "harness" code a new benchmark may need.
- **`Support/` holds cross-cutting constants** (the size matrix). Kept separate from
  fixtures so that "what data" and "how much data" are distinct decisions.
- **`Execution/` and `Query/` are reserved, empty folders** (created with a `.gitkeep` or
  the first benchmark). They make the intended growth explicit without pre-building any
  executor-dependent code (C1). They contain nothing until the executor exists.

### 5.3 Benchmark class conventions

The conventions a new benchmark follows. There is **no base class** in the initial
implementation — this is a deliberate decision, documented in §5.3.1.

The conventions:

1. **Namespace and name.** `PolyStore.Benchmarks.<Subsystem>`; class name
   `<Subject>Benchmarks` (e.g. `CanonicalTupleStoreBenchmarks`, `HeapStoreBenchmarks`,
   `RidBenchmarks`). The `Benchmarks` suffix mirrors the `Tests` suffix convention and
   makes benchmark classes greppable and distinguishable from production types.
2. **One class per subject.** A class benchmarks one subject (one store, `Rid`, one
   operator) across its parameters. Do not mix unrelated subjects in one class.
3. **Attributes, in order:**
   - `[MemoryDiagnoser]` — enables the `Allocated` (bytes) and GC-generation columns.
     This is **not** part of BDN's default config in 0.15.x, so the explicit declaration
     is what turns allocation measurement on (do not assume it is enabled by default).
     Note: the attribute is `[MemoryDiagnoser]` (renamed from `MemoryMetric` in BDN
     0.13.0); there is no separate `[AllocatedMemoryMetric]`.
   - `[BenchmarkCategory("<subsystem>")]` — logical grouping (`"rid"`, `"storage"`,
     later `"execution"`, `"query"`) for filtering and reporting. (BDN's attribute is
     `[BenchmarkCategory]`; this is the grouping mechanism the feature request refers to.)
   - `[ParamsSource(nameof(Sizes))]` or `[Params(...)]` — the parameter matrix (§5.5).
     These are **member-level** attributes: BDN targets them at a field or property, never
     the class. They decorate the parameter field (e.g.
     `[ParamsSource(nameof(Sizes))] public int _size;`), not the class declaration.
   - `[Benchmark]` on each measured method. `[Benchmark(Baseline = true)]` is reserved
     for a genuine A/B comparison of two implementations of the same operation (§5.9) —
     it is not used in the initial set.
4. **Setup vs. measured.** Dataset construction and store population are `[GlobalSetup]`
   (*not* measured). BDN runs `[GlobalSetup]` **once per benchmark case** — that is, once
   per `([Benchmark] method × parameter combination)`, on a fresh class instance for that
   case — so it is *not* shared across the class's methods: a class with N benchmark
   methods pays the setup cost N times. Per-iteration state restoration (if any) is
   `[IterationSetup]`/`[IterationCleanup]` (*not* measured). The `[Benchmark]` method
   contains *only* the operation under test (§5.4, invariant I-SETUP).
5. **No result-throwaway.** A benchmark that returns `void` and whose side effect could be
   optimized away must return or accumulate a value (e.g. a checksum) so the JIT cannot
   eliminate the work.
6. **Cancellation and async.** When (later) benchmarking `IAsyncEnumerable` execution, the
   benchmark method may be `async Task` and must drain the stream with a
   `CancellationToken`; it must not collect into a `List<T>` (C3).

#### 5.3.1 Why there is no `BaseBenchmark`

The feature request lists "BaseBenchmark" among the conventions. We considered a shared
abstract base and deliberately chose **not** to introduce one, for these reasons:

- `AGENTS.md` C1/C6: "avoid unnecessary inheritance" and "introduce an abstraction when at
  least one current architectural boundary requires it." A base class would centralize
  only the `[MemoryDiagnoser]` attribute (one line) — not a boundary.
- A base class that also carried a shared dataset-size `[Params]` would be *wrong*: `Rid`
  benchmarks need no dataset size, while store benchmarks do. Forcing a shared parameter
  couples unrelated subjects.
- BDN's own guidance favors self-contained benchmark classes; parameters, setup, and
  fixtures differ per subject.
- The duplication that actually matters (building a deterministic dataset) is eliminated by
  the shared static `DatasetGenerator` (§5.4), which is concrete, testable, and carries no
  inheritance.

**Trigger to reconsider:** if three or more benchmark classes begin duplicating the same
non-trivial setup logic (beyond calling `DatasetGenerator`), extract that logic into a
static helper first; introduce a base class only if the helper itself needs per-class
behavior. Until then, explicit per-class attributes plus shared static helpers is the
smallest coherent design.

### 5.4 Fixtures and datasets

The relation value type used by storage benchmarks is a small, fixed-shape record with a
mix of value-type (`Id`, `Amount`) and reference-type (`Name`) attributes, so allocation
behavior is observable:

```csharp
namespace PolyStore.Benchmarks.Fixtures;

/// <summary>
/// The relation value type used by storage benchmarks. A fixed, small shape that
/// exercises both value-type (Id, Amount) and reference-type (Name) attributes.
/// </summary>
public sealed record BenchTuple
{
    public long Id { get; init; }
    public string? Name { get; init; }
    public decimal Amount { get; init; }
}
```

Why a dedicated `BenchTuple` rather than `HelloWorld.Customer`:

- The benchmark project must not depend on the sample project.
- A controlled shape (value + reference + nullable) makes allocation behavior observable
  and deterministic.
- `Customer` is an aspirational sample that may change; a benchmark fixture should be
  stable and owned by the benchmark project.

Dataset construction is deterministic by construction (no wall-clock, no ambient state) and
lives in a static helper so every benchmark builds data the same way:

```csharp
using System.Collections.Generic;
using PolyStore.Storage;
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
    public static (InMemoryCanonicalTupleStore<BenchTuple> Store, List<Rid> Rids) Build(int count)
    {
        var store = new InMemoryCanonicalTupleStore<BenchTuple>();
        var rids = new List<Rid>(count);
        for (var i = 0; i < count; i++)
        {
            rids.Add(store.Insert(Create(i)));
        }

        return (store, rids);
    }
}
```

Notes:

- **Deterministic (D1).** Values are derived from the index; no `Random`, no
  `DateTime.Now`. If a future benchmark needs randomized data, it must use a fixed seed
  (e.g. `new Random(12345)`) so runs are reproducible.
- **Setup, not measured (D2, I-SETUP).** `Build` is called only from `[GlobalSetup]`.
- **Returns the RIDs** so benchmarks can target known RIDs (for `TryGet`, `Contains`)
  without re-deriving them.
- **Heap population is O(n²) (setup cost).** `Build` itself is O(n) (the canonical store's
  `Insert` is O(1)), but *populating a heap* from those RIDs is O(n²): the heap's `Add`
  performs an O(current-size) duplicate check, so building a heap of `n` RIDs costs O(n²)
  comparisons. This is why the heap's default matrix is small (1k/10k — ~10⁸ comparisons
  at 10k, sub-second) while the O(n)-setup canonical store can use larger sizes — see §5.5
  and §5.6.3.

The size constants are shared, but each subsystem picks its own matrix: sizes are added
only when a benchmark needs them, and the O(n²)-setup heap uses a smaller matrix than the
O(n)-setup canonical store (D4):

```csharp
namespace PolyStore.Benchmarks.Support;

/// <summary>
/// The dataset-size matrix for storage benchmarks. Kept deliberately short so the
/// benchmark matrix stays manageable (D4). Add a size only when a benchmark needs it.
/// </summary>
public static class DatasetSizes
{
    /// <summary>Tiny: one thousand rows. The heap's default floor (O(n²) setup is cheap here).</summary>
    public const int Tiny = 1_000;

    /// <summary>Small: ten thousand rows. The heap's default top and the canonical store's floor.</summary>
    public const int Small = 10_000;

    /// <summary>
    /// Medium: one hundred thousand rows. The canonical store's default top. The O(n)-setup
    /// canonical store builds this in O(n); the O(n²)-setup heap does not use this size.
    /// </summary>
    public const int Medium = 100_000;

    /// <summary>
    /// Large: one million rows. Opt-in deep runs only, for the O(n)-setup canonical store.
    /// The O(n²)-setup heap never offers this (a 1M heap build is ~5×10¹¹ comparisons).
    /// </summary>
    public const int Large = 1_000_000;
}
```

**Invariant I-SETUP (setup is not measured).** Any work that constructs the dataset,
populates a store, or generates RIDs is setup. It runs in `[GlobalSetup]` — which BDN
invokes **once per benchmark case** (once per `[Benchmark] method × parameter combination`,
on a fresh class instance for that case), so a class with N benchmark methods pays the setup
cost N times — or in `[IterationSetup]`/`[IterationCleanup]` (per iteration). A
`[Benchmark]` method contains only the operation under test. A benchmark that cannot
separate the two must say so (C4) rather than silently measure setup.

**Invariant I-STATE (benchmarks are self-contained).** A `[Benchmark]` method must not
leave the fixture in a state that (a) breaks a subsequent benchmark, (b) grows or depletes
unboundedly across the (large) number of invocations BDN performs in throughput mode, or
(c) depends on another benchmark's side effects. Stateful operations are therefore measured
as self-contained, state-restoring cycles (§5.6), and pure reads target stable, pre-built
state.

### 5.5 Parameterization

Dataset size is the primary parameter. It is applied via `[ParamsSource]` on the size
*field* (a member-level attribute — BDN targets it at a field or property, not the class)
over the shared `DatasetSizes` constants, but each subsystem picks its own matrix — the
O(n²)-setup heap uses a smaller one than the O(n)-setup canonical store (D4):

- **Subsystem-specific default matrices (smoke + sweep):** the heap uses
  `[DatasetSizes.Tiny, DatasetSizes.Small]` (1k/10k) and the canonical store uses
  `[DatasetSizes.Small, DatasetSizes.Medium]` (10k/100k). This is what `dotnet run`
  executes by default and is what a "smoke" CI run (§5.8) would use. The heap's smaller
  matrix reflects its O(n²) setup cost (§5.4, §5.6.3).
- **Deep runs (opt-in):** `Large` (1M) is defined but *not* in any default matrix; it is
  available only as an opt-in for the O(n)-setup canonical store (a dedicated class whose
  `Sizes` includes `DatasetSizes.Large`) when a deep sweep is wanted. The heap never offers
  it. This keeps the default matrices from combinatorially exploding as more benchmarks are
  added.

Guidelines to avoid an unmanageable matrix:

- **One parameter axis per class where possible.** Store benchmarks vary by size only. Do
  not also vary by tuple width, RID count, and operation in the same class — that is a
  cartesian product. If a second axis is genuinely needed, it is a separate class or a
  small `[Params]` of 2–3 values.
- **Prefer `[ParamsSource]` over many `[Params]`** so the set is defined once and can be
  trimmed.
- **Reserve the largest sizes for explicit runs.** The default run's *measured* work should
  finish in seconds-to-minutes on a developer machine; the *setup* is bounded separately
  (the heap's 10k setup is ~10⁸ comparisons, sub-second, per method — §5.5/§5.6.3), and
  multi-minute sweeps are opt-in.
- **Setup cost is part of "manageable."** The heap's setup is O(n²) (each `Add` does an
  O(n) duplicate check), so the heap's matrix stops at `Small` (10k) and does **not**
  offer `Medium` (100k) or `Large` (1M); the O(n)-setup canonical store may offer `Large`
  as an opt-in. `[GlobalSetup]` runs once per benchmark method (per-case, §5.3/§5.4), so
  the setup cost is multiplied by the number of benchmark methods in the class (≈4× for the
  heap at 10k). "Seconds-to-minutes" refers to *measured* work; setup time is separate and
  must also be bounded (D4).
- **`Rid` benchmarks take no dataset parameter** — they are micro-baselines of a single
  value type. This is why a shared size parameter cannot live in a base class (§5.3.1).

### 5.6 The initial benchmark set (validates the harness)

This is the concrete, implementation-ready initial set. It uses only the implemented
surface (D3) and is small. It establishes useful baselines without coupling the harness to
one access path: the canonical store, the heap, and `Rid` are benchmarked through their
public types, and the same shape can be pointed at a future B-tree path or a second
provider.

A note on what is and is not measurable today: `ICanonicalTupleStore<T>` exposes
`Count`, `Insert`, `TryGet`, `Delete` — it has **no enumeration API**, so there is no
canonical-store "scan" to benchmark. Scans are a heap concern (`EnumerateRids`,
`EnumerateTuples`). This is accurate to the current API and is not a gap to paper over.

**Stateful operations are measured as self-contained, state-restoring cycles** (I-STATE).
In BDN's default throughput mode the benchmark method is invoked many times per iteration;
a bare `Insert` would grow the store unboundedly and a bare `Delete` would deplete a
pre-built pool, both of which distort the measurement. The honest, robust choice is to
measure the realistic write cycle (`Insert`+`Delete`, `Add`+`Remove`) and to name it so.
Pure reads (`TryGet`, `Contains`, `Enumerate*`) are clean single operations. If isolated
single-write measurements are later needed, that is a refinement (e.g. a
`Monitored`-strategy variant) and is deferred, not built speculatively.

#### 5.6.1 `Rid` micro-baseline — `Benchmarks/Rid/RidBenchmarks.cs`

```csharp
using BenchmarkDotNet.Attributes;
using PolyStore.Storage;

namespace PolyStore.Benchmarks.Rid;

[MemoryDiagnoser]
[BenchmarkCategory("rid")]
public class RidBenchmarks
{
    private Rid _a;
    private Rid _b;

    [GlobalSetup]
    public void Setup()
    {
        _a = new Rid();
        _b = new Rid();
    }

    [Benchmark]
    public Rid Create() => new Rid();

    [Benchmark]
    public bool Equality() => _a.Equals(_b);

    [Benchmark]
    public int Hashing() => _a.GetHashCode();
}
```

`Create` measures `Guid.CreateVersion7()` computation + struct construction — the cost every
store insert pays. `Equality` and `Hashing` are the dictionary-key costs. These are
standalone micro-benchmarks (no baseline among them); they give a scale for interpreting the
store numbers, which pay these RID costs on top of their own work.

#### 5.6.2 Canonical tuple store — `Benchmarks/Storage/CanonicalTupleStoreBenchmarks.cs`

```csharp
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using PolyStore.Benchmarks.Fixtures;
using PolyStore.Benchmarks.Support;
using PolyStore.Storage;
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
    private List<Rid> _rids = null!;
    private BenchTuple _tuple = null!;
    private Rid _knownRid;

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
```

#### 5.6.3 Heap store — `Benchmarks/Storage/HeapStoreBenchmarks.cs`

```csharp
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using PolyStore.Benchmarks.Fixtures;
using PolyStore.Benchmarks.Support;
using PolyStore.Storage;
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
    private List<Rid> _rids = null!;
    private Rid _memberRid;   // guaranteed present in the heap (for Contains)
    private Rid _churnRid;    // guaranteed absent (for the Add/Remove cycle)

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
        _churnRid = new Rid();                                       // not in the heap
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
```

This set is small and exercises both implemented stores and the RID currency. The *measured*
work runs in seconds-to-minutes, and the heap's *setup* stays cheap because its matrix
stops at `Small` (10k): the heap's O(n²) setup (each `Add` performs an O(current-size)
duplicate check) is ~10⁸ comparisons at 10k (sub-second) and would be ~5×10⁹ at 100k or
~5×10¹¹ at 1M, so the heap deliberately uses the small 1k/10k matrix while the O(n)-setup
canonical store uses 10k/100k (and may offer the 1M opt-in). `AddAndRemove` measures the
combined steady-state cost of `Add` + `Remove` (not `Add` in isolation — see §9 OQ2), which
is exactly the kind of real cost the harness exists to surface.

### 5.7 Warmup, JIT, caching, and misleading measurements

- **JIT and warmup are handled by BDN.** By default BDN runs each benchmark case in its own
  isolated child process — a fresh optimized (Release) runtime with its own JIT and GC heap —
  and runs warmup iterations before measuring it, so first-touch JIT and cold-cache effects
  are absorbed into that benchmark's warmup, not its measurement, and JIT/GC state cannot
  leak from one benchmark into another. The harness must *not* add manual pre-warming in
  `[GlobalSetup]` — that would be redundant and would blur the setup/measured boundary
  (I-SETUP).
- **Throughput mode is the default and is correct here.** BDN's default run strategy
  invokes the benchmark method in a tight loop and reports mean/error/stddev over the
  aggregated time. This is appropriate for these CPU-bound, in-memory operations and is why
  the statefulness rules (I-STATE) matter: the method is called many times, so it must be
  self-contained.
- **`Monitored` strategy (future, not default).** For a benchmark where per-invocation GC
  or allocation noise dominates, BDN's `Monitored` run strategy measures each invocation
  separately and is more robust to GC jitter. It is slower. It is an opt-in set on the job
  (`Job.Default.WithStrategy(RunStrategy.Monitoring)`, or a per-benchmark `[Job]`), not the
  harness default.
- **GC and allocation noise.** `[MemoryDiagnoser]` reports allocated bytes and GC generations
  per operation, making allocation-driven regressions visible (C7). For allocation-heavy
  paths, compare the `Allocated` column, not just the time column.
- **Return-value validator (expected, not a harness bug).** BDN ships a `ReturnValueValidator`
  execution validator that inspects the values benchmark methods return. Our benchmarks
  deliberately return a value on every iteration to stop the JIT from eliding the measured
  work; for most methods that value is stable and deterministic (a `bool`, an `int`/checksum),
  which is expected, not a sign of a broken harness. `RidBenchmarks.Create` is the one
  exception: it returns a *fresh* `Rid` each invocation (that is its elision guard), and it
  would be the method the validator flags if the validator were explicitly enabled. In BDN 0.15.8 this
  validator is **not** part of the default validator set (it flags *inconsistent* return
  values across a class's methods, and runs only when explicitly enabled — e.g. via the
  `[ReturnValueValidator]` attribute or a config that adds it), so it will not appear on a
  default run. If it is enabled and its message is noisy, suppress per class with
  `[ReturnValueValidator(false)]` (downgrades to a non-failing warning) or remove it from
  the validator set.
- **Caching.** These benchmarks are in-memory; there is no disk cache to invalidate. The
  realistic noise sources are CPU frequency scaling, background processes, and GC. Mitigations:
  run on a quiet machine, keep the .NET SDK pinned (`global.json` already pins
  `10.0.1xx`), and rely on BDN's statistical aggregation (mean + error + stddev) rather than
  a single number. Do not chase a single "best" run.
- **Configuration correctness (D9, D10).** Always `-c Release`. The project compiles
  warning-free in Release (`TreatWarningsAsErrors`). A benchmark that is accidentally run
  in Debug measures unoptimized code and is misleading; the `README.md` states Release is
  required.

### 5.8 Local execution and result persistence

**Local run.** The entry point passes CLI arguments straight through to BDN:

```csharp
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

// Minimal config: write BDN's artifacts (results + logs) under the already-gitignored
// artifacts/ directory. BDN's own default is BenchmarkDotNet.Artifacts/, which the repo's
// .gitignore does NOT cover — so without this, results would be committed (D7/A4).
// A single ArtifactsPath setting is a justified, minimal config, not a configuration layer (C1).
var config = DefaultConfig.Instance.WithArtifactsPath("artifacts");

// Discover and run every [Benchmark] class in this assembly.
// Command-line arguments (--filter, --list, --exporters, …) are passed through from
// `dotnet run -- …`.
// Overload: BenchmarkRunner.Run(Assembly, IConfig?, string[]?) — note the args are the
// LAST parameter (the invalid form is Run(args, config); there is no such overload).
BenchmarkRunner.Run(typeof(Program).Assembly, config, args);
```

`Program.cs` is the entire harness entry point — no custom argument parsing, no service
locator, no configuration layer (C1). The single `ArtifactsPath` setting is the one
deliberate exception: it keeps results in the gitignored `artifacts/` directory instead of
BDN's default `BenchmarkDotNet.Artifacts/` (which the repo does not ignore). The same can
be achieved per-invocation with the `--artifacts artifacts` CLI flag if a config object is
ever dropped.

Supported invocations:

```bash
# List every benchmark (sanity check; no measurement).
dotnet run --project PolyStore.Benchmarks -c Release -- --list flat

# Run everything (default matrices: heap 1k/10k, canonical 10k/100k).
dotnet run --project PolyStore.Benchmarks -c Release

# Run one subject only.
dotnet run --project PolyStore.Benchmarks -c Release -- --filter "*CanonicalTupleStore*"

# Run one method. BDN's --filter is a GLOB matched against the full benchmark name
# (namespace.Class.Method); * is a wildcard, so a bare name will not match.
dotnet run --project PolyStore.Benchmarks -c Release -- --filter "*TryGet"

# Export a machine-readable CSV (the default run already produces HTML + GitHub markdown).
dotnet run --project PolyStore.Benchmarks -c Release -- --exporters csv
# (BDN 0.15.8 takes a single exporter per run; use --exporters json for JSON.)

# Within-run A/B comparison: available when a class marks one method [Baseline = true] and
# compares another against it (see §5.9). The initial set has NO baselines — every method
# is a standalone measurement; baselines appear only for a genuine A/B comparison. The ratio
# is computed WITHIN this single run — BDN does not persist a baseline for a later run.
dotnet run --project PolyStore.Benchmarks -c Release -- --filter "*CanonicalTupleStore*"
```

**Result persistence.** BDN writes results under `BenchmarkDotNet.Artifacts/` by default,
which the repo's `.gitignore` does **not** cover. We therefore point it at the already-
gitignored `artifacts/` directory (via `ArtifactsPath` in `Program.cs`, or `--artifacts
artifacts` on the command line), so local runs leave no tracked artifacts (D7). The
recommended persistence story, smallest first:

1. **Local, default:** results land in `artifacts/` (gitignored). CSV/JSON/HTML exporters
   make them inspectable and scriptable. Nothing is committed.
2. **Within-run A/B comparison (recommended, built-in):** BDN's `Ratio`/`RatioSD` columns
   are computed **within each benchmark class**, comparing that class's benchmarks against
   the one marked `[Baseline]`. This is the primary mechanism for "is implementation A faster
   than B?" (§5.9) — drive it by marking the reference method `[Baseline = true]` and
   reading the `Ratio` column in a normal `dotnet run`. BDN does **not** persist a baseline
   for a later run; the ratio is a within-run comparison, not a cross-run one.
3. **Cross-run / cross-commit comparison (optional, future):** BDN has **no** built-in
   cross-run baseline; this is done externally. Export the run to CSV/JSON, commit a
   curated baseline CSV (e.g. `PolyStore.Benchmarks/Baselines/baseline.csv`), and diff a
   new run against it with a script or CI step. This is a small extension and is **not**
   built in the initial implementation; it is marked future work. The harness's exporters
   already produce the CSV needed, so no architectural change is required to add it later.

The principle: **persist locally by default, compare within a run using BDN's `[Baseline]`
ratio, and add external committed-baseline diffing only when cross-commit tracking becomes
a real need** (C1).

### 5.9 Comparing alternative implementations and strategies

When a B-tree path, a second provider, or a different execution strategy lands, the harness
compares it against the current one with no new infrastructure, using BDN's built-in
baseline/ratio mechanism.

**Scoping fact (important):** BDN's `[Baseline]`/`Ratio` is computed **within a single
benchmark class** (or within a logical group of methods inside one class). It does **not**
span separate classes: a `[Baseline = true]` method in `HeapStoreBenchmarks` will not
produce a `Ratio` column for a distinct `BTreePathBenchmarks` class — that class's `Ratio`
column stays empty. So the BDN-idiomatic within-run A/B path keeps both implementations in
**one class**:

- **Preferred: two explicit methods in one class, sharing the size parameter.** A single
  class declares one `[Benchmark(Baseline = true)]` method for the current implementation
  and one `[Benchmark]` method for the alternative, both taking the same size parameter so
  they run at equivalent sizes. BDN reports the alternative method's `Ratio` (e.g. `1.85` =
  1.85× slower) in the same table. Because both methods run in one class against the same
  fixture and size, the `Ratio` is computed correctly and the comparison is apples-to-apples.
  (This couples one class to two implementations — the price of a within-run `Ratio`; the
  fixture is shared, so self-containment (I-STATE) is preserved.)

    ```csharp
    [ParamsSource(nameof(Sizes))]
    public int _size;   // shared size parameter — both run at the same size

    [Benchmark(Baseline = true)]
    public long HeapScan() => /* current implementation */;

    [Benchmark]
    public long BTreeScan() => /* alternative implementation */;   // Ratio reported relative to HeapScan
    ```
- **Also valid: separate classes, compared externally.** Keep `BTreePathBenchmarks` and
  `HeapStoreBenchmarks` as separate classes (cleaner one-subject-per-class ownership) and
  compare them with the external CSV/JSON diff described in §5.8/§5.11 (a committed baseline
  plus a ratio computed by a script or CI step). This path does **not** rely on BDN's
  `Ratio` column.
- **Grouping within one class.** If one class must hold several method groups,
  `[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]` keeps the summary table
  readable; this is a presentation aid, not a cross-class baseline mechanism.
- **Execution-strategy comparison (future).** Row-at-a-time vs. vectorized
  (`ARCHITECTURE.md` §16) is compared the same way: two methods in one class (a baseline
  method for the current strategy and a method for the alternative, sharing the size
  parameter), or two classes compared externally. The harness does not pre-build either
  strategy.

This satisfies R3 (A/B comparison) with zero new abstraction: within-run comparison is
BDN's `[Baseline]`/`Ratio` applied to two methods inside one class (the baseline method
versus the alternative method), and cross-class/cross-run comparison is the external
CSV/JSON diff. Neither requires new infrastructure.

### 5.10 What BenchmarkDotNet can and cannot measure

BDN is the right tool for the *single-process, CPU-bound, allocation-sensitive* measurements
this foundation targets. It is not the right tool for everything PolyStore will eventually
need to measure. Being explicit about the boundary now prevents the harness from being
misused later.

**BDN measures reliably (and the initial set uses these):**

- Single-threaded **throughput** (operations/second) and **latency** (mean, error, stddev)
  for a method invoked in a loop.
- **Allocations** (bytes per operation) and **GC generation** counts via `[MemoryDiagnoser]`.
- **Relative comparison** of two implementations via `[Baseline]` + ratio columns.
- Statistical confidence (error/stddev) across many iterations, with warmup/JIT isolation.

**BDN does not measure well (and must not be forced to):**

- **End-to-end wall-clock under realistic I/O.** BDN measures managed-method time in an
  isolated process; it does not model disk/network latency, page cache behavior, or
  concurrent I/O. When PolyStore has a persistent store, "query latency under a cold cache"
  is a different measurement than BDN provides.
- **Concurrency and throughput under contention.** BDN is single-threaded per benchmark.
  PolyStore's open concurrency-control and MVCC questions (`ARCHITECTURE.md` §28) require a
  multi-threaded workload driver measuring contention, lock behavior, and transaction
  isolation — not a BDN loop.
- **GC/OS-level behavior and memory footprint over time.** BDN reports per-operation
  allocation, not heap growth over a long run, GC pause distributions, or RSS. That is
  `dotnet-trace` / PerfView / a soak harness.
- **Soak / stability / leak behavior.** Long-running behavior (memory leaks, handle leaks,
  degradation over hours) is outside BDN's short-iteration model.
- **Multi-process / multi-node scaling.** Out of scope for a single-process BDN run.

**Mapping to PolyStore's open areas (`ARCHITECTURE.md` §28):**

| Future need | Right tool | Relationship to this harness |
|---|---|---|
| RID lookup cost, canonical representation, allocation | **BDN (this harness)** | In scope now / near-term. |
| Row vs. batch execution (§16) | **BDN** (one class, two methods sharing the size parameter, one baseline — or two classes compared externally per §5.9) | In scope once the executor exists. |
| Concurrency control, MVCC, transaction isolation | Dedicated multi-threaded workload harness | Separate concern; add later, do not build now. |
| I/O, buffer management, persistence format | I/O-aware harness + `dotnet-trace`/PerfView | Separate concern; add when a persistent store exists. |
| Memory leaks / soak / stability | Soak harness + `dotnet-counters`/PerfView | Separate concern; add later. |

The harness is designed so these are *additions* (new projects or new tooling), not
rework: the fixture/generator conventions, the Release-only rule, and the
provider-independent benchmark shape all carry over.

### 5.11 CI

**Recommendation: do not gate CI on absolute benchmark numbers, and do not add a benchmark
job to the default pipeline in the initial implementation.** Shared GitHub-hosted runners
are noisy (variable CPU, neighbors, throttling), and absolute microbenchmark numbers on such
infrastructure are not a reliable regression signal. Gating on them would produce false
failures and train the team to ignore the job — the worst outcome.

The staged plan:

1. **Initial (Phase 0–1): no benchmark job in CI.** Benchmarks run locally. CI continues to
   build and test only. This is the recommended starting state.
2. **Next (Phase 2, optional): a non-gating "smoke" benchmark job** to catch *pathological*
   regressions (e.g. an order-of-magnitude slowdown from an accidental O(n²) or a
   per-tuple reflection regression), with a generous threshold and as a *report*, not a
   gate. Concretely: run only the small size (`--filter` to the 10k matrix), a short
   subset, export CSV, and (in an external diff step) flag only an extreme ratio (e.g. >
   5×) relative to a committed baseline. This is a sketch of the CI design to adopt when
   benchmarks enter CI:

   ```yaml
   # .github/workflows/cicd.yaml — ADD (Phase 2, non-gating smoke)
   benchmark-smoke:
     if: ${{ github.event_name == 'pull_request' }}
     runs-on: ubuntu-latest
     timeout-minutes: 30
     steps:
       - name: Checkout
         uses: actions/checkout@v7
         with:
           fetch-depth: 0
       - name: Setup .NET
         uses: actions/setup-dotnet@v6
         with:
           dotnet-version: |
             10.0.x
       - name: Restore
         run: dotnet restore
       # Smoke: short, non-gating. Exports a CSV for inspection.
       # BDN's --filter is a GLOB matched against the benchmark's base name
       # (namespace.Class.Method) — verified against 0.15.8 in Phase 0. The size
       # parameter is NOT in the filterable name (it appears as a `_size` table column
       # but cannot be filtered), so a size-restricting filter is not possible with a
       # single glob. The smoke therefore runs a small, size-independent subset: the
       # RID micro-benchmarks (no size) and one store method at every size.
       - name: Run benchmark smoke (non-gating)
         continue-on-error: true
         run: |
           dotnet run --project PolyStore.Benchmarks -c Release \
             -- --filter "*RidBenchmarks*" --exporters csv
             # (Optionally add a store method for a size-independent store smoke,
             #  e.g. -- --filter "*TryGet" — this runs it at every configured size.)
       # EXTERNAL diffing (NOT a BDN built-in): compare the fresh CSV against the committed
       # baseline with a generous threshold (flag only > 5x regressions). Report only.
       - name: Diff against committed baseline (report only)
         if: always()
         continue-on-error: true
         run: |
           ./scripts/diff-benchmark-baseline.sh \
             --baseline PolyStore.Benchmarks/Baselines/baseline.csv \
             --threshold 5
       - name: Upload results (for inspection)
         if: always()
         uses: actions/upload-artifact@v4
         with:
           name: benchmark-smoke
           path: artifacts/
   ```

   Noise-avoidance reasoning: pinned .NET (`10.0.x`), a small size and short subset to
   limit variance exposure, `continue-on-error` so it reports rather than gates, and a
   generous threshold in the diff step so only order-of-magnitude regressions are flagged.
   Absolute numbers on a shared runner are never the gate.

   Note: cross-run / cross-commit comparison is **external diffing** — BDN has no built-in
   cross-run baseline, and the `diff-benchmark-baseline.sh` step above is a Phase-2
   artifact, not a BDN feature. The exact BDN CLI flags used for any cross-commit
   automation must be verified against the pinned `0.15.8` during Phase 2. This snippet is
   a sketch; the diff script and its threshold are the parts that must be written and
   validated when Phase 2 lands.
3. **Later (when it matters): a full sweep on a dedicated/self-hosted or pinned runner**
   where the hardware is stable, with committed-baseline diffing as a report. This is the
   only environment where absolute-number regression gating is defensible, and it is
   deferred until the engine has enough surface to make the sweep meaningful.

The invariant: **CI never gates on absolute benchmark numbers on shared infrastructure.**
At most it runs a generous, non-gating smoke check to catch pathological regressions.

### 5.12 Developer ergonomics: adding a new benchmark

The whole process, with no harness knowledge required beyond reading `README.md`:

1. **Pick the subsystem folder** under `Benchmarks/` (create it if new, e.g.
   `Benchmarks/Execution/`).
2. **Add a class** named `<Subject>Benchmarks` in namespace
   `PolyStore.Benchmarks.<Subsystem>`.
3. **Decorate the class** with `[MemoryDiagnoser]` and
   `[BenchmarkCategory("<subsystem>")]`, and (if it needs a dataset) add a size field
   decorated with the **member-level** `[ParamsSource(nameof(Sizes))]` attribute (BDN
   targets it at a field, not the class) backed by
   `public static IEnumerable<int> Sizes => [DatasetSizes.Small, DatasetSizes.Medium];`.
4. **Build the fixture in `[GlobalSetup]`** by calling `DatasetGenerator.Build(_size)` (or
   construct a small fixture directly). Do not build data in a `[Benchmark]` method.
5. **Write `[Benchmark]` methods** that contain only the operation under test, are
   self-contained (I-STATE), and return/accumulate a value if the work could otherwise be
    elided. Mark a method `[Benchmark(Baseline = true)]` only when doing a genuine A/B
    comparison of two implementations of the same operation (§5.9) — the initial set has no
    baselines.
6. **Run it:** `dotnet run --project PolyStore.Benchmarks -c Release -- --filter
   "<Subject>"`.

That is the entire contract. There is no registration step, no base class to inherit, no
configuration file, and no harness code to modify. The only shared code a new benchmark may
touch is `DatasetGenerator`/`BenchTuple`/`DatasetSizes`, and only if it needs a different
fixture or size.

---

## 6. Implications

- **Storage.** None of the engine's storage types change. Benchmarks consume the concrete
  in-memory implementations (`InMemoryCanonicalTupleStore<T>`, `InMemoryHeapStoreProvider<T>`)
  and `Rid` through their public surface. The initial set is expected to surface real
  characteristics (e.g. the heap `Add` O(n) duplicate check), which may motivate later storage
  work — that is the harness doing its job, not a harness defect.
- **Access paths.** The harness is access-path-agnostic: it benchmarks the heap today and
  can benchmark a B-tree path tomorrow by pointing the same benchmark shape at it (§5.9).
  No access-path code changes are required.
- **Planner / executor.** No planner or executor exists; the harness reserves
  `Benchmarks/Execution/` and `Benchmarks/Query/` but pre-builds nothing (C1). When the
  executor lands, end-to-end benchmarks are added as new classes using the same
  conventions, streaming `IAsyncEnumerable<T>` with cancellation (C3).
- **APIs.** No public API of `PolyStore` changes. The benchmark project is additive and
  references the library only.
- **Serialization.** None. Results are BDN's CSV/JSON/HTML under the gitignored
  `artifacts/` directory (configured via `ArtifactsPath`; BDN's own default is
  `BenchmarkDotNet.Artifacts/`, which the repo does not ignore).
- **Transactions / concurrency.** Out of scope for the initial set (no transaction
  implementation exists). The harness explicitly does not attempt concurrency measurement
  (§5.10); that is a future, separate concern.
- **Testing.** The benchmark project is not part of `dotnet test` and has no xunit
  dependency. `DatasetGenerator` is plain, deterministic C# and is trivially inspectable; if
  a regression in fixture construction matters, a small test in `PolyStore.Tests` can assert
  its determinism, but that is optional and not required for the foundation.
- **Diagnostics.** BDN's HTML/CSV/JSON exporters and ratio columns are the diagnostic
  surface. No new logging or diagnostic infrastructure is added.
- **Future vectorization / batching.** The row-vs-batch comparison (`ARCHITECTURE.md` §16)
  is a first-class use case the harness supports via the A/B mechanism (§5.9) once both
  strategies exist. Nothing in the harness pre-commits to row-at-a-time.
- **Provider implementations.** A second storage provider is benchmarked by adding classes
  that target its concrete types, sharing the fixture (§5.9). No indirection layer is
  introduced.

---

## 7. Performance Considerations

- **The harness must not distort the measurement.** Setup is excluded (I-SETUP); benchmarks
  are self-contained (I-STATE); results are aggregated statistically (mean/error/stddev),
  not taken from a single run.
- **Allocation visibility is a feature.** `[MemoryDiagnoser]` makes per-operation allocation
  explicit, which is how C7 regressions (per-tuple reflection, hidden buffering, repeated
  compilation surfacing as allocations) are caught.
- **No per-tuple reflection in the engine under test.** The fixtures use plain records and
  direct method calls; they do not introduce reflection into the measured path.
- **Matrix size is bounded** (subsystem-specific: heap 1k/10k, canonical 10k/100k, with
  the O(n)-setup canonical store able to offer the 1M opt-in) so a full local run stays in
  seconds-to-minutes for *measured* work; the heap's O(n²) setup is why it stops at 10k and
  does not offer the 100k/1M sizes (D4).
- **Empirical, not asserted.** Any claim like "the heap Add is O(n)" is a *hypothesis the
  benchmark tests*, not a conclusion this document asserts. The numbers come from running
  the harness.

---

## 8. Assumptions

- **A1.** BenchmarkDotNet `0.15.8` (latest stable) supports .NET 10 and the features used
  here (`[MemoryDiagnoser]`, `[BenchmarkCategory]`, `[ParamsSource]`, the within-run
  `[Baseline]` ratio, `ArtifactsPath`/`--artifacts`, and CSV/JSON/HTML exporters). If a
  pinned version lacks a feature, pin the nearest version that has it; do not adopt a
  preview.
- **A2.** The in-memory store and heap remain the only implemented storage surface for the
  lifetime of this initial set. If a B-tree path or second provider lands first, the
  initial set is extended (not replaced) to include it.
- **A3.** Local developer machines are the primary benchmark environment for the initial
  phases; CI benchmarking (if added) is non-gating and smoke-only (§5.11).
- **A4.** `artifacts/` remains gitignored, and BDN is configured to write there (its own
  default, `BenchmarkDotNet.Artifacts/`, is not gitignored); no benchmark result is
  committed in the initial implementation. Committed-baseline diffing is a future, optional
  extension.
- **A5.** The `BenchTuple` shape (value + reference + nullable attributes) is representative
  enough for storage benchmarks. If a specific relation shape matters, a benchmark may
  define its own fixture; `BenchTuple` is the default, not a mandate.
- **A6.** BDN's default throughput run strategy is appropriate for these CPU-bound,
  in-memory operations. `Monitored` is available per-benchmark if GC noise dominates.
- **A7.** The benchmark project is a development tool, not a product artifact; it is never
  packed or published.

---

## 9. Open Questions

None of these block the foundation; each is flagged for when the relevant area lands.

1. **Committed-baseline format and diffing.** If cross-commit comparison is adopted, what
   is the canonical baseline artifact (CSV? JSON?), where does it live, and what threshold
   and statistical test (e.g. a ratio with a confidence bound) defines a "regression"?
   Deferred until cross-commit tracking is a real need (§5.8, §5.11).
2. **Isolated single-write benchmarks.** Whether to add `Monitored`-strategy variants that
   measure a single `Insert` or `Add` in isolation (as opposed to the state-restoring
   cycle). Deferred; the cycle is the honest initial measurement (§5.6).
3. **End-to-end benchmark shape.** When the executor exists, what is the canonical
   end-to-end benchmark (a fixed query over a fixed dataset? a mutation + read-back
   cycle?), and how is async streaming + cancellation expressed in a BDN method? Deferred
   to the executor design.
4. **Concurrency measurement.** What is the multi-threaded workload harness for
   concurrency-control/MVCC measurement, and does it live in this project or a new one?
   Deferred to the transactions/concurrency design (§5.10).
5. **Size-matrix policy (resolved).** Sizes are per-subsystem: the O(n²)-setup heap uses
   1k/10k and the O(n)-setup canonical store uses 10k/100k (with the 1M opt-in). The current
   implementations already demonstrate that a single shared matrix is wrong — the heap's
   O(n²) setup makes a 100k default impractical while the canonical store handles it in O(n)
   (§5.4, §5.5, §5.6.3). Revisit only if a new subsystem's setup cost changes this.

---

## 10. Implementation Outline

A plan for a later implementation agent; **not** an implementation. Phases are ordered by
dependency; each is independently shippable and small (C1).

**Phase 0 — Project skeleton + one benchmark (validates the toolchain).**
Add `PolyStore.Benchmarks/` (csproj per §5.1), add it to `PolyStore.slnx`, add
`Program.cs` (§5.8), and add `RidBenchmarks` (§5.6.1). Verify:
`dotnet run --project PolyStore.Benchmarks -c Release -- --filter "*RidBenchmarks*"` produces a BDN
table with `Mean`, `Error`, `StdDev`, and `Allocated` columns. *Depends on: nothing.* This
phase proves BDN + .NET 10 + the solution wiring before any fixture work.

**Phase 1 — Fixtures + the storage benchmark set.**
Add `BenchTuple`, `DatasetGenerator`, `DatasetSizes` (§5.4, §5.5) and
`CanonicalTupleStoreBenchmarks` + `HeapStoreBenchmarks` (§5.6.2, §5.6.3). Add
`Benchmarks/Execution/` and `Benchmarks/Query/` as reserved empty folders. Add `README.md`
(§5.12). Verify: a full default run (`dotnet run --project PolyStore.Benchmarks -c
Release`) completes in seconds-to-minutes of *measured* work (setup is bounded separately — §5.5/§5.6.3) and reports the canonical store's two benchmarks across 10k/100k,
the heap's four benchmarks across 1k/10k, and the RID set. *Depends on: Phase 0.*

**Phase 2 — Result persistence + (optional) CI smoke.**
Confirm CSV/JSON/HTML export to the configured `artifacts/` directory (via `ArtifactsPath`)
and the within-run `[Baseline]` ratio workflow (§5.8). *Optionally* add the non-gating
`benchmark-smoke` CI job (§5.11) with an external committed-baseline CSV diff and a
generous, non-gating threshold. *Depends on: Phase 1.* This phase is the only one that
touches CI, and it is explicitly non-gating.

**Phase 3 — (future, separate) A/B comparison + end-to-end + concurrency.**
When a B-tree path, second provider, or executor lands: add the comparison classes
(§5.9), end-to-end streaming benchmarks (C3), and — as a separate concern — the
concurrency/soak harness (§5.10). Each is its own design/implementation step, not part of
this foundation. *Depends on: the respective engine feature existing.*

Out of scope for all phases above: load testing, multi-process scaling, dashboards, result
databases, and any gating of CI on absolute benchmark numbers — all deferred until PolyStore
has the functionality to make them meaningful (`ARCHITECTURE.md` §28).
