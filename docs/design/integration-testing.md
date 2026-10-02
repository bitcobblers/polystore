# Integration Testing Foundation

**Status:** Proposal
**Date:** 2026-10-01
**Revised:** 2026-10-01 (review reconciliation)

**Scope:** This document designs a *dedicated integration-test project and the
unit/integration test boundary* for PolyStore: where the project lives, what it is for,
what it is explicitly not for, the rule a contributor applies to decide which project a
test belongs in, the project structure and test conventions, how it plugs into the
existing build/test/CI workflow, a small initial set of integration tests that exercise
only behavior the current implementation actually supports, and the model by which future
features contribute integration and regression coverage without duplicating unit tests.
It is **not** a test platform, a test-data framework, a performance or load-testing
facility, and it does not design or implement any engine feature. Those are
`ARCHITECTURE.md` §28 open areas that this project will eventually *test* once they exist.

## Revision history

- **2026-10-01 — Initial proposal.**
- **2026-10-01 — Review reconciliation.** Addressed the consolidated findings from the
  correctness and stress reviews: rule identifiers made unique (duplication rules are
  now Dup1–Dup3, contribution rules F1–F6); Executive Summary written; §5.2 test count
  corrected (8, not 7); §5.1 access-path row corrected; §8.3 rule 6 clarified; §8.4
  test-5 rationale tightened; §8.5 local invocation corrected; the review-only detection
  gap acknowledged in §10; and the §12 scale rule narrowed to performance claims.

---

## Executive Summary

**Verdict: adopt the dedicated project.** PolyStore gains
`PolyStore.IntegrationTests`, a sibling of `PolyStore.Tests` that references `PolyStore`
only and carries an identical package set. The unit/integration boundary is defined
once — in this document, pointed to from `AGENTS.md` — in terms of the five
architectural boundaries `ARCHITECTURE.md` already names (AB1–AB5), with a two-step
decision procedure a contributor can apply mechanically.

Of those boundaries, exactly one is implemented today: AB1, the access-path ↔
canonical-store RID bridge. The initial test set covers exactly that one boundary:
six tests in `Storage/HeapCanonicalStoreBridgeTests` — two migrated from
`PolyStore.Tests` and four new — all passing against the current implementation.

The existing build, test, format, and CI workflows require **zero changes**: the
project is picked up by the solution-level invocations already in place. No new
abstractions, no external infrastructure, no changes to `PolyStore`'s public API.

---

## 1. Problem Statement

PolyStore has exactly one test project (`PolyStore.Tests`) and no defined boundary
between unit/component tests and integration tests. The consequences are already visible
and will worsen as the engine grows:

- **The boundary is undefined in practice.** The two tests in
  `PolyStore.Tests/Storage/Impl/InMemoryHeapStoreProviderTests.cs` that exercise
  `EnumerateTuples` (`EnumerateTuples_ResolvesRidsThroughCanonicalStore`,
  `EnumerateTuples_SkipsRidsAbsentFromCanonicalStore`) observe behavior that emerges
  only from the *interaction* of two components across the access-path ↔ canonical-store
  boundary (`ARCHITECTURE.md` §4/§6). They sit in a unit-test class next to tests of the
  heap in isolation. There is no rule that says which side of the line they are on.
- **There is no home for cross-boundary behavior.** When the hosting, execution,
  compiler, and propagation layers land (today they are `NotImplementedException` stubs
  or unimplemented interfaces — §5), their observable cross-subsystem behavior (commit
  visibility, propagation correctness per `ARCHITECTURE.md` §22, query execution) will
  have nowhere to go that is distinct from single-component tests.
- **Duplication is unguarded.** Without a rule, the same cross-boundary behavior can be
  asserted in both projects, or an "integration" test that actually asserts a
  single-component invariant can be added to either project. Neither failure mode is
  detectable by the build.
- **Regression coverage has no placement rule.** `AGENTS.md` requires a regression test
  for bugs "when practical" but does not say which project a regression test for a
  cross-boundary bug belongs in.

Success for this foundation means:

- A dedicated `PolyStore.IntegrationTests` project exists, is separate from
  `PolyStore.Tests`, and is picked up by `dotnet build`, `dotnet test`, `dotnet format`,
  and CI with **no workflow changes**.
- A contributor can decide, without judgment or discussion, which project a proposed test
  belongs in (§8.1 decision procedure).
- The initial test set exercises the only cross-boundary behavior the implementation
  supports today — the access-path ↔ canonical-store RID bridge — and every test in it
  passes against the current code.
- Future features have an explicit contribution model (§8.6) that keeps integration and
  unit coverage from drifting or duplicating.

---

## 2. Architectural Context

Integration testing sits *above* the logical/physical boundary and observes PolyStore
through its public surface. It must respect the same invariants the engine does:

- **Logical/physical separation (`ARCHITECTURE.md` §29).** Integration tests assert
  *observable* behavior — what a consumer of the public API can see — and never
  implementation detail. A test that asserts call sequences or private state is not an
  integration test under this design.
- **The RID model (`ARCHITECTURE.md` §5).** The RID is the logical currency between
  access paths and the canonical store. The access-path ↔ canonical-store boundary is the
  one architectural boundary that is *implemented today* and therefore the only one the
  initial test set can exercise (§5, §8.4).
- **Canonical authority (`ARCHITECTURE.md` §4, §6).** "The canonical store answers two
  fundamental questions: which representation of a tuple is authoritative, and given a
  tuple identifier, how can the complete tuple be obtained. Access paths do not replace
  this representation." The initial integration tests pin exactly this contract: the heap
  owns ordering and membership of RIDs; the canonical store owns the tuples; the bridge
  (`EnumerateTuples`) resolves RIDs and skips dangling ones.
- **No implicit fallback (`AGENTS.md`, `ARCHITECTURE.md` §22).** Tests must assert the
  observable outcome, not the absence of failure. A cross-boundary test that passes
  vacuously (asserting only that construction succeeded) is not a test of the boundary.
- **Scope control (`AGENTS.md`).** "Introduce an abstraction when at least one current
  architectural boundary requires it, not merely because a future implementation might."
  This project introduces no base classes, no fixture framework, no test-data
  generators, and no shared test-support assembly (§6, §8.3).
- **Auxiliary-project conventions.** `PolyStore.Benchmarks` establishes the house style
  for auxiliary projects: a root-level sibling of `PolyStore`, `IsPackable=false`,
  references `PolyStore` only (not the sample or the test project), a project-local
  `README.md`, subsystem folders, and no speculative infrastructure. This project follows
  the same shape.
- **Existing test conventions (`AGENTS.md`, `PolyStore.Tests`).** xunit 2.9.3, test
  classes named after the subject with a `Tests` suffix, AAA structure with code grouped
  by Arrange/Act/Assert, folder layout mirroring the source, tests that establish
  semantics rather than implementation detail, and regression tests for bugs. This
  design extends those conventions to scenario-based integration tests (§8.3).

---

## 3. Requirements

### 3.1 From the feature request

- **R1 — Separate project.** A dedicated `PolyStore.IntegrationTests` project, separate
  from `PolyStore.Tests`, for integration and regression testing of end-to-end
  observable behavior across subsystem boundaries.
- **R2 — Boundary definition.** A clear, actionable rule defining the boundary between
  unit/component tests and integration tests, including concrete examples of tests that
  belong in each project.
- **R3 — Project structure.** Directory layout and project file consistent with existing
  conventions (net10.0, xunit, same package versions as `PolyStore.Tests` unless there is
  a reason to differ), project references, and solution membership.
- **R4 — Test conventions.** Naming (including a convention for scenario-based tests),
  AAA structure, fixture strategy (construction per test, disposal semantics, isolation),
  and a rule against duplicating ordinary unit tests.
- **R5 — Workflow integration.** `dotnet build` / `dotnet test` pick up the project; CI
  workflow changes if any; documentation updates (AGENTS.md tests section, README).
- **R6 — Initial test set.** A concrete first set of integration tests, each mapped to
  representative end-to-end behavior the current implementation *actually* supports,
  specific enough to implement without further design decisions. No invented behavior.
- **R7 — Contribution model.** How future features contribute integration/regression
  coverage without duplicating ordinary unit tests.
- **R8 — No external infrastructure.** No testcontainers, no external databases, no
  third-party test frameworks beyond xunit, unless current PolyStore behavior actually
  requires it. (It does not — §6.)
- **R9 — Scope control.** A small, coherent design; no speculative framework
  (`AGENTS.md`).

### 3.2 From `AGENTS.md`

- **C1 — Tests establish semantics**, not internal call sequences; both passing and
  failing scenarios are validated.
- **C2 — Naming.** Test classes carry a `Tests` suffix; `PolyStore.Tests` mirrors
  `PolyStore`'s layout; the integration project follows a similarly predictable layout.
- **C3 — Regression tests** are added for bugs when practical.
- **C4 — Fail explicitly.** No silent fallbacks; a test that cannot assert the intended
  contract must not be weakened to pass.
- **C5 — Async where I/O may occur.** Future integration tests over `IAsyncEnumerable`
  must stream and support cancellation; they must not buffer into `Task<List<T>>`.
- **C6 — Documentation.** New conventions are documented (AGENTS.md, project README);
  this document is the authoritative definition of the boundary.

### 3.3 Derived requirements

- **D1 — Implemented behavior only.** Every test in the project must pass against the
  current implementation. Tests of stubbed or unimplemented behavior are forbidden
  (§8.1 step 2, §10).
- **D2 — Public surface only.** Tests observe behavior through public types and members
  of `PolyStore`; no reflection into internals, no `InternalsVisibleTo`.
- **D3 — Deterministic.** No wall-clock dependence, no ambient state, no unseeded
  randomness.
- **D4 — Isolated.** Tests do not depend on execution order or on other tests' state.
- **D5 — Zero CI change.** The project must be discoverable by the existing
  solution-level `dotnet test` and `dotnet format` invocations (§8.5).
- **D6 — Fast.** The initial set runs in milliseconds; integration tests do not carry
  performance dataset-scale workloads (that is `PolyStore.Benchmarks`' territory);
  correctness scale beyond a few thousand tuples requires PR justification (§12).

---

## 4. Non-Goals

- **Not a test platform.** No base classes, no custom attributes, no test-data
  generators, no scenario DSL, no shared test-support assembly, no configuration layer.
  xunit + plain C# is the entire harness (R9, C1).
- **Not a relocation of unit tests.** `PolyStore.Tests` keeps its unit/component tests.
  Only tests that cross an architectural boundary move (§8.1 Dup3), and only the two that
  already do today.
- **Not coverage of unimplemented behavior.** No tests against `ITransaction`,
  `DatabaseBuilder`, `DatabaseContext`, the DML extensions, or propagation — none of
  these is implemented (§5). Speculative tests that would fail with
  `NotImplementedException` are explicitly out of scope (D1).
- **Not performance, load, soak, or concurrency testing.** That is
  `PolyStore.Benchmarks` and the future harnesses it defers
  (`docs/design/benchmarking.md` §5.10).
- **No external infrastructure.** No testcontainers, no PostgreSQL/SQLite test
  instances, no network. The current implementation is entirely in-memory; introducing
  external infrastructure would test infrastructure, not PolyStore (§6, R8).
- **No changes to `PolyStore`'s public API.** This design is additive to the repository
  and its only subtraction is the two tests that move between projects (§8.4).
- **Not a redefinition of what "integration test" means in the industry.** The term is
  defined precisely for this repository in §8.1 and that definition governs.

---

## 5. Existing Implementation

Verified against the source tree at the time of writing. This is the evidence base for
what the initial test set may and may not exercise.

### 5.1 Implementation state by subsystem

| Subsystem | Implemented (executable today) | Stub / unimplemented |
|---|---|---|
| `Storage` | `Rid` (create, equality, hash, `ToString`); `ICanonicalTupleStore<T>` + `InMemoryCanonicalTupleStore<T>` (`Insert`, `TryGet`, `Delete`, `Count`); `IAccessPath<T>` (`Configure`); `InMemoryHeapStoreProvider<T>` (`Add`, `Remove`, `Contains`, `EnumerateRids`, `EnumerateTuples(store)`, `Count`) — a standalone sealed class that does **not** implement `IAccessPath<T>`; `HeapPath<T>` / `BTreePath<T>` (implement `IAccessPath<T>`; declarative shells) | `HeapPath<T>.Include` (no-op); `BTreePath<T>.Column`/`Include` (no-ops); `RowStorageRelationStore<T>` / `ColumnStorageRelationStore<T>` (empty classes) |
| `Core` | `IRelation<T>` (marker), `ISource<T>` (interface), `IRelationContext` (interface), `IDerived<T>` / `IPropagateChanges<T>` / `IRelationChangeContext` (interfaces), `Change<T>`, `RelationChange<T>` records, `RelationAttribute` | `IQueryableExtensions` — every DML method (`Update`, `Insert`, `Delete`, `Fork`) throws `NotImplementedException` |
| `Compiler` | `OptimizationPipeline.Add<T>()` (registration only) | `IRelationRewriter` (empty marker); no pipeline application, no rewriters |
| `Execution` | — | `ITransaction` (interface; **no implementation exists**) |
| `Hosting` | `DatabaseModule` (abstract shell) | `DatabaseBuilder.Source`/`Relation` throw `NotImplementedException`; `DatabaseContext.From<T>`, `From<T, TPath>`, `FromValues<T>` throw `NotImplementedException` |

Consequence (D1, R6): **the only cross-boundary behavior executable today is the
access-path ↔ canonical-store bridge** — `InMemoryHeapStoreProvider<T>.EnumerateTuples`
resolving RIDs through `InMemoryCanonicalTupleStore<T>`. The initial integration test
set (§8.4) is therefore a storage-bridge set, and nothing more.

### 5.2 Existing test project

`PolyStore.Tests`:

- xunit `2.9.3`, `Microsoft.NET.Test.Sdk` `18.10.1`, `xunit.runner.visualstudio` `4.0.0`,
  `coverlet.collector` `10.0.1`, `GitHubActionsTestLogger` `3.0.5`; net10.0;
  `ImplicitUsings=enable` (overriding the repo default of `disable`), `Nullable=enable`,
  `IsPackable=false`; `<Using Include="Xunit"/>`; references `PolyStore` only.
- Two test files, both under `Storage/Impl/`, mirroring the source layout:
  - `InMemoryHeapStoreProviderTests` (8 tests; two of them — the `EnumerateTuples` pair —
    cross the AB1 boundary, §8.1);
  - `InMemoryCanonicalTupleStore` (5 tests). **Note:** this class does not carry the
    `Tests` suffix that `AGENTS.md` prescribes — a pre-existing inconsistency, tracked in
    §15 OQ1.
- Conventions in use: `Method_Condition_ExpectedBehavior` naming (e.g.
  `Add_DuplicateRid_IsTrackedOnlyOnce`, `Remove_ReturnsFalseForUnknownRid`), AAA grouping
  via blank lines, a local `private record TestTuple { long Id; string? Name; }` fixture,
  namespace mirroring the folder (`PolyStore.Tests.Storage.Impl`).

### 5.3 Build, test, and CI workflow

- `PolyStore.slnx` lists four projects: `HelloWorld` (under `/samples/`),
  `PolyStore.Tests`, `PolyStore.Benchmarks`, `PolyStore`.
- `Directory.Build.props` sets net10.0, `Nullable=enable`, `ImplicitUsings=disable`,
  `TreatWarningsAsErrors` in Release.
- CI (`.github/workflows/cicd.yaml`) runs, at the repository root against the solution:
  `dotnet restore` → `dotnet format --no-restore --verify-no-changes` →
  `dotnet build` → `dotnet test --collect:"XPlat Code Coverage"
  --logger:GitHubActions`. A `publish` job re-runs build/test in Release on `main`.
  **Both `dotnet test` and `dotnet format` operate on the solution, so any test project
  added to `PolyStore.slnx` is picked up automatically — no workflow edit is required
  (D5).**
- `.pre-commit-config.yaml` runs `dotnet format --verify-no-changes` (pre-commit) and
  `dotnet test` (pre-push), likewise at solution scope.
- `PolyStore.Benchmarks` is the model auxiliary project: root-level sibling, `Exe`,
  `IsPackable=false`, references `PolyStore` only, project-local `README.md`, subsystem
  folders, no base class, and an explicit "foundation, not a platform" stance.

---

## 6. Alternatives Considered

### A. Dedicated `PolyStore.IntegrationTests` project + a written boundary rule (recommended)

A new xunit test project, sibling of `PolyStore.Tests`, referencing `PolyStore` only,
with the unit/integration boundary defined once in this document (and pointed to from
`AGENTS.md`), an initial test set covering the one implemented cross-boundary behavior,
and a contribution model for future features.

- *Complexity:* Low. One project, one csproj, one slnx line, one README, six tests.
  Identical package set to `PolyStore.Tests`.
- *Correctness:* The boundary rule is grounded in `ARCHITECTURE.md`'s named layers, so
  placement decisions are checkable against the architecture rather than taste.
- *Extensibility:* New architectural boundaries (AB2–AB5, §8.1) become new folders as
  they are implemented; no harness change is needed.
- *Failure behavior:* A test of unimplemented behavior fails loudly
  (`NotImplementedException`); the rule forbids adding such tests (D1), so the project
  never contains a red-by-construction test.
- *Compatibility:* Zero changes to CI, `PolyStore`, or `PolyStore.Tests` beyond moving
  two tests that already cross a boundary.

**Verdict:** Recommended.

### B. A single test project with an `Integration/` folder

Keep one test project; put cross-boundary tests under `PolyStore.Tests/Integration/`.

- *Complexity:* Lowest — no new project.
- *Correctness:* The folder is a convention with no enforcement; nothing distinguishes
  integration tests in discovery, reporting, or policy. The exact ambiguity this feature
  exists to remove (the `EnumerateTuples` tests sitting in a unit class) would persist.
- *Extensibility:* As the engine grows, one project would hold two different test
  cultures (fast single-component vs. cross-boundary scenario tests) with no structural
  seam between them.
- *Compatibility:* Meets R1 only partially — the feature explicitly requests a separate
  project.

**Verdict:** Rejected. It defers the boundary problem rather than solving it, and
contradicts R1.

### C. Dedicated project + a shared `PolyStore.TestSupport` fixture assembly

As A, plus a third project holding shared test fixtures (tuple records, dataset
builders) used by both test projects.

- *Complexity:* A third project, a fourth slnx entry, and a cross-project fixture API to
  design and version — for a fixture that today is a two-field record.
- *Correctness:* Correct, but premature: there is exactly one integration test class,
  and it needs the same local record `PolyStore.Tests` already defines privately.
- *Compatibility:* `PolyStore.Benchmarks` deliberately did **not** take its fixtures
  from the sample project for the same reason (independence, stability); the benchmark
  design documents the same scope-control reasoning (`docs/design/benchmarking.md` §5.4).

**Verdict:** Rejected now. The trigger to reconsider is defined in §8.6 (F5): three or
more test classes in either project duplicating non-trivial fixture construction.

---

## 7. Recommendation

Adopt alternative A.

A dedicated `PolyStore.IntegrationTests` xunit project, structurally identical to
`PolyStore.Tests` (same target framework, same package versions, `PolyStore`-only
reference), added to `PolyStore.slnx` and picked up by the existing solution-level
build/test/format/CI with no workflow changes. The unit/integration boundary is defined
once, in terms of the architectural boundaries `ARCHITECTURE.md` already names, with a
two-step decision procedure a contributor can apply mechanically (§8.1). The initial test
set covers exactly the one cross-boundary behavior that is implemented — the
access-path ↔ canonical-store RID bridge — including migrating the two existing tests
that already cross that boundary, so the convention is demonstrated rather than merely
stated (§8.4). Future features contribute coverage under an explicit model: cross-boundary
behavior gets an integration test added *with* the feature; single-component behavior
gets a unit test; neither duplicates the other (§8.6).

No new abstractions, no external infrastructure, no changes to `PolyStore`'s public API.

---

## 8. Proposed Design

### 8.1 The unit/integration boundary

**Definitions.**

- A **unit/component test** verifies the behavior of a single PolyStore component. Its
  assertions do not depend on the behavior of a component on the other side of an
  architectural boundary.
- An **integration test** verifies *observable behavior that emerges from interaction
  across an architectural boundary*: behavior that would not exist if either side of the
  boundary were removed. It observes that behavior only through the public surface of the
  components involved.

**Architectural boundaries.** The boundaries are the layers `ARCHITECTURE.md` treats as
independent, each with its own contract:

| ID | Boundary | Architectural basis | State (verified, §5.1) |
|---|---|---|---|
| AB1 | Access path ↔ canonical tuple store (RID indirection) | §4, §5, §6 | **Implemented** — `InMemoryHeapStoreProvider<T>` + `InMemoryCanonicalTupleStore<T>`; testable now |
| AB2 | Relational expressions (Core) ↔ optimization/rewriting (Compiler) | §2, §10 | Not testable — DML extensions throw; `OptimizationPipeline` registers but never applies |
| AB3 | Execution (planner/executor) ↔ storage | §14, §15 | Not testable — no executor; `ITransaction` unimplemented |
| AB4 | Public API/hosting ↔ engine (Core/Compiler/Execution/Storage) | §25 | Not testable — `DatabaseBuilder`/`DatabaseContext` throw |
| AB5 | Transaction ↔ change propagation (derived relations) | §19–§21 | Not testable — `IPropagateChanges<T>`/`IRelationChangeContext` are unimplemented interfaces |

The set is open: a new architectural boundary named by `ARCHITECTURE.md` or an approved
design document is a new row here, and the first test that crosses it gets a folder
(§8.6 F4).

**Decision procedure.** For a proposed test, answer in order:

1. **Boundary?** Does the assertion depend on behavior that emerges only when two
   components on either side of one of AB1–AB5 interact (one side's output or state is
   consumed or observed through the other)?
   - No → **unit test** in `PolyStore.Tests`.
   - Yes → step 2.
2. **Implemented?** Is the behavior under test implemented — not a
   `NotImplementedException` stub, not an unimplemented interface?
   - No → **no test yet.** The test is written together with the feature that implements
     the behavior (§8.6 F1). Writing a test now would pin a red-by-construction
     expectation or invite a fake that tests the fake instead of the engine (D1, C4).
   - Yes → **integration test** in `PolyStore.IntegrationTests`.

**Observable behavior** — what an integration test may assert:

- values returned through the public API;
- state observable through the public API (`TryGet`, `Contains`, `Count`, enumeration
  contents and order);
- the type of exception thrown where failure is part of the contract.

What it may not assert:

- internal object identity, private state, or internal call sequences;
- implementation choices the API does not expose (e.g., how RIDs are assigned — except
  that the RID `Insert` returns is the one that resolves, which the API does expose).

**Concrete placement examples.**

| Test | Project | Why |
|---|---|---|
| `InMemoryHeapStoreProviderTests.Add_DuplicateRid_IsTrackedOnlyOnce` | `PolyStore.Tests` | Single component; heap dedup in isolation |
| `InMemoryHeapStoreProviderTests.Add_Then_EnumerateRids_ReturnsRids` | `PolyStore.Tests` | Single component; the canonical store is never touched |
| `HeapCanonicalStoreBridgeTests.EnumerateTuples_ResolvesRidsThroughCanonicalStore` | `PolyStore.IntegrationTests` | AB1 — tuple observable only via heap RIDs resolved through the canonical store |
| `HeapCanonicalStoreBridgeTests.EnumerateTuples_FollowsHeapOrder_NotCanonicalInsertionOrder` | `PolyStore.IntegrationTests` | AB1 — ordering owned by the heap, tuples owned by the store; neither holds alone |
| (future, AB3/AB4) `TransactionTests.Insert_Then_Commit_IsVisibleToSubsequentRead` | `PolyStore.IntegrationTests` | Mutation through the API, observed through the API |
| (future, AB5) `DerivedRelationPropagationTests.PropagatedState_MatchesDefine` | `PolyStore.IntegrationTests` | The `ARCHITECTURE.md` §22 correctness property, end to end |

**Duplication rules.** Labelled Dup1–Dup3 so that rule identifiers are unique across
this document: the feature-request requirements are R1–R9 (§3.1), the AGENTS.md-derived
conventions are C1–C6 (§3.2), the derived requirements are D1–D6 (§3.3), and the
contribution rules are F1–F6 (§8.6).

- **Dup1 — No unit copies.** An integration test must not re-assert a single-component
  invariant that a unit test already asserts (e.g. a bridge test does not assert
  `heap.Count` semantics; it asserts the combined observable outcome).
- **Dup2 — No integration pretenders.** A test that crosses no architectural boundary
  does not belong in `PolyStore.IntegrationTests`, however "end-to-end" it feels.
- **Dup3 — Move, don't copy.** A test that already exists in `PolyStore.Tests` but
  crosses an architectural boundary is *moved* to `PolyStore.IntegrationTests`. The two
  `EnumerateTuples` tests are the only such tests today and are migrated in §8.4.

### 8.2 Project structure

New project at the repository root, sibling of `PolyStore.Tests`:

```
polystore/
├── PolyStore/                     (library, unchanged)
├── PolyStore.Tests/               (unit/component tests; two tests move out, §8.4)
├── PolyStore.IntegrationTests/    (NEW — integration tests)
├── PolyStore.Benchmarks/          (unchanged)
├── HelloWorld/                    (sample, unchanged)
├── PolyStore.slnx                 (one line added)
└── ...
```

`PolyStore.slnx` gains one line, immediately after `PolyStore.Tests` (grouping the test
projects; top-level, not under `/samples/`):

```xml
<Solution>
  <Folder Name="/samples/">
    <Project Path="HelloWorld/HelloWorld.csproj" />
  </Folder>
  <Project Path="PolyStore.Tests/PolyStore.Tests.csproj" />
  <Project Path="PolyStore.IntegrationTests/PolyStore.IntegrationTests.csproj" />
  <Project Path="PolyStore.Benchmarks/PolyStore.Benchmarks.csproj" />
  <Project Path="PolyStore/PolyStore.csproj" />
</Solution>
```

`PolyStore.IntegrationTests/PolyStore.IntegrationTests.csproj` — deliberately identical
to `PolyStore.Tests/PolyStore.Tests.csproj` (same target framework, same package
versions, same reference surface; there is no reason to differ, R3):

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <IsPackable>false</IsPackable>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="coverlet.collector" Version="10.0.1">
          <PrivateAssets>all</PrivateAssets>
          <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
        </PackageReference>
        <PackageReference Include="GitHubActionsTestLogger" Version="3.0.5" />
        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.10.1" />
        <PackageReference Include="xunit" Version="2.9.3"/>
        <PackageReference Include="xunit.runner.visualstudio" Version="4.0.0">
          <PrivateAssets>all</PrivateAssets>
          <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
        </PackageReference>
    </ItemGroup>

    <ItemGroup>
        <Using Include="Xunit"/>
    </ItemGroup>

    <ItemGroup>
      <ProjectReference Include="..\PolyStore\PolyStore.csproj" />
    </ItemGroup>

</Project>
```

Decisions:

- **References `PolyStore` only.** Not `PolyStore.Tests` (no dependency on unit-test
  internals or fixtures), not `HelloWorld` (the sample is aspirational and may change;
  fixtures are owned by the test project, §8.3).
- **`coverlet.collector` and `GitHubActionsTestLogger` included** so the existing CI
  `--collect:"XPlat Code Coverage"` and `--logger:GitHubActions` flags behave identically
  for this project as for `PolyStore.Tests` (R5, D5).
- **No hosting, no testcontainers, no database packages.** Nothing in the current
  implementation requires external infrastructure (§5.1); adding any would violate R8.
- **Layout mirrors the source tree, one level per architectural boundary** (C2):

  ```
  PolyStore.IntegrationTests/
  ├── PolyStore.IntegrationTests.csproj
  ├── README.md                          # purpose, boundary rule, how to add a test, how to run
  └── Storage/
      └── HeapCanonicalStoreBridgeTests.cs
  ```

  Folders are created **when the first test for that boundary lands** (AB2 → `Compiler/`
  or `Core/`, AB3 → `Execution/`, AB4 → `Hosting/`, AB5 → `Execution/` or `Core/` —
  chosen at that time by the entry-point rule below). Unlike `PolyStore.Benchmarks`,
  which reserves empty `Execution/` and `Query/` folders to document a fixed harness
  shape, this project reserves nothing: an empty folder would imply coverage that does
  not exist, and D1 forbids tests of unimplemented behavior.

- **Folder placement rule.** A test lives in the folder of the subsystem that is the
  *entry point* of the scenario — the topmost component the test starts from. The
  storage-bridge tests start from the heap/canonical store → `Storage/`. A future test
  starting from `ITransaction.ExecuteAsync` → `Execution/`; one starting from
  `DatabaseContext`/`DatabaseBuilder` → `Hosting/`.

### 8.3 Test conventions

The conventions a new integration test follows. There is **no base class** — xunit
attributes and plain C# are the entire harness (R9).

1. **Class naming.** Two forms, both carrying the `Tests` suffix (C2):
   - **Boundary tests** (one architectural boundary, two components):
     `<ComponentA><ComponentB>BridgeTests` — e.g. `HeapCanonicalStoreBridgeTests`.
   - **Scenario tests** (cross-cutting behavior through multiple boundaries, named for
     the observable behavior, not a class): `<Scenario>Tests` where the scenario is a
     short noun phrase — e.g. `TransactionCommitVisibilityTests`,
     `DerivedRelationPropagationTests`.
   One class per boundary or scenario; do not mix unrelated boundaries in one class.
2. **Method naming.** `<interaction>_<expected outcome>`, consistent with the existing
   `Method_Condition_ExpectedBehavior` style:
   `EnumerateTuples_ResolvesRidsThroughCanonicalStore`,
   `DeleteFromCanonicalStore_HidesTupleFromHeapEnumeration`. For scenario tests, the
   interaction is the given/when: `Insert_Then_Commit_IsVisibleToSubsequentRead`.
3. **AAA.** Arrange / Act / Assert grouped by blank lines, exactly as in
   `PolyStore.Tests` (C1). A short comment is permitted only where the *reason* for an
   arrangement is non-obvious (the existing dangling-RID comment is the model).
4. **Namespace mirrors folder** — `PolyStore.IntegrationTests.Storage` for
   `Storage/HeapCanonicalStoreBridgeTests.cs`.
5. **Fixtures.**
   - Relation value types are local `private record` fixtures defined in the test class,
     matching the existing `TestTuple` pattern (small, deterministic, value + reference
     attributes).
   - Components are constructed **in the test method** via their public constructors.
     xunit creates a fresh test-class instance per test, so no shared state exists
     (D4).
   - **No `IClassFixture`/`ICollectionFixture` in the initial set** — there are no
     shared resources to share. The trigger to introduce one is a fixture that is
     expensive to build *and* read-only across a class; until then, per-test
     construction is the rule.
   - **Disposal semantics.** No current component is `IDisposable`, so there is nothing
     to dispose. The standing rule for the future: a fixture that is `IDisposable` or
     `IAsyncDisposable` (e.g. an `ITransaction` implementation) is disposed in the test
     method via `using`/`await using` (or the test class implements `IDisposable` /
     `IAsyncLifetime`). Tests never rely on finalizers or on xunit's disposal of
     undisposed objects.
   - **Determinism (D3).** Values are derived from literals or indices; no
     `DateTime.Now`, no `Random` without a fixed seed, no ambient state.
6. **Async and streaming (C5).** When AB3/AB4/AB5 tests land over
   `IAsyncEnumerable<T>`, they drain the stream with a `CancellationToken` (pass
   `CancellationToken.None` or a source token) and assert on the streamed sequence.
   C5 constrains the *engine's* API surface — a production API must stream and must
   not return `Task<List<T>>`; it does not forbid a test from collecting the
   enumerated values into a local collection in order to assert on them.
7. **No tests of unimplemented behavior (D1).** A proposed test whose Act or Assert
   would reach a `NotImplementedException` stub or an unimplemented interface is not
   added, regardless of how valuable it would be. It is written when the feature lands
   (§8.6 F1). This keeps the project green-by-construction and keeps `NotImplemented`
   failures from masquerading as regressions.

### 8.4 The initial integration test set

Class: `Storage/HeapCanonicalStoreBridgeTests.cs`, namespace
`PolyStore.IntegrationTests.Storage`. Six tests, all crossing AB1, all passing against
the current implementation (§5.1). Two are migrated from
`PolyStore.Tests/Storage/Impl/InMemoryHeapStoreProviderTests.cs` (Dup3); four are new and
pin cross-boundary invariants the unit tests do not cover.

| # | Test | Behavior exercised | Why it is integration, not unit |
|---|---|---|---|
| 1 | `EnumerateTuples_ResolvesRidsThroughCanonicalStore` *(migrated)* | Tuples inserted into the canonical store are observable through the heap in heap insertion order | The tuple is visible only via RID resolution across AB1 |
| 2 | `EnumerateTuples_SkipsRidsAbsentFromCanonicalStore` *(migrated)* | A heap RID with no canonical tuple is skipped, not faulted | The skip is the bridge's contract, not either component's alone |
| 3 | `DeleteFromCanonicalStore_HidesTupleFromHeapEnumeration` | Deleting the canonical tuple removes it from heap enumeration while the heap still tracks the RID | Asserts canonical authority: the store owns the tuple, the heap does not (ARCHITECTURE.md §4) |
| 4 | `RemoveFromHeap_HidesTupleFromEnumeration_ButKeepsCanonicalTuple` | Removing the RID from the heap hides the tuple from enumeration while `TryGet` still returns it | Asserts the access path is not the representation (ARCHITECTURE.md §6) — the symmetric half of #3 |
| 5 | `Add_DuplicateRid_EnumeratesTupleOnce` | A RID added to the heap twice yields the tuple exactly once through the bridge | RID→tuple resolution preserves the heap's multiplicity: a RID the heap tracks once yields its tuple exactly once — the bridge neither duplicates nor drops the tuple (the heap's own dedup is asserted in `PolyStore.Tests`) |
| 6 | `EnumerateTuples_FollowsHeapOrder_NotCanonicalInsertionOrder` | Enumeration order follows the heap's RID order, not the canonical store's insertion order | Pins the separation: the heap owns traversal order, the store owns tuples (ARCHITECTURE.md §1.1, §6.1) |

Reference shape (conventions of §8.3; the two migrated tests keep their names and
semantics, re-homed):

```csharp
using PolyStore.Storage;
using PolyStore.Storage.Impl;

namespace PolyStore.IntegrationTests.Storage;

public class HeapCanonicalStoreBridgeTests
{
    private record TestTuple
    {
        public long Id { get; init; }
        public string? Name { get; init; }
    }

    [Fact]
    public void EnumerateTuples_ResolvesRidsThroughCanonicalStore()
    {
        var store = new InMemoryCanonicalTupleStore<TestTuple>();
        var heap = new InMemoryHeapStoreProvider<TestTuple>();

        var alice = new TestTuple { Id = 1, Name = "Alice" };
        var bob = new TestTuple { Id = 2, Name = "Bob" };
        heap.Add(store.Insert(alice));
        heap.Add(store.Insert(bob));

        Assert.Equal([alice, bob], heap.EnumerateTuples(store).ToArray());
    }

    [Fact]
    public void EnumerateTuples_SkipsRidsAbsentFromCanonicalStore()
    {
        var store = new InMemoryCanonicalTupleStore<TestTuple>();
        var heap = new InMemoryHeapStoreProvider<TestTuple>();

        var alice = new TestTuple { Id = 1, Name = "Alice" };
        heap.Add(store.Insert(alice));
        heap.Add(new Rid()); // A RID with no canonical tuple.

        Assert.Equal([alice], heap.EnumerateTuples(store).ToArray());
    }

    [Fact]
    public void DeleteFromCanonicalStore_HidesTupleFromHeapEnumeration()
    {
        var store = new InMemoryCanonicalTupleStore<TestTuple>();
        var heap = new InMemoryHeapStoreProvider<TestTuple>();

        var alice = new TestTuple { Id = 1, Name = "Alice" };
        var rid = store.Insert(alice);
        heap.Add(rid);

        store.Delete(rid);

        Assert.Empty(heap.EnumerateTuples(store).ToArray());
        Assert.True(heap.Contains(rid)); // The heap still tracks the RID; it is not authoritative.
    }

    [Fact]
    public void RemoveFromHeap_HidesTupleFromEnumeration_ButKeepsCanonicalTuple()
    {
        var store = new InMemoryCanonicalTupleStore<TestTuple>();
        var heap = new InMemoryHeapStoreProvider<TestTuple>();

        var alice = new TestTuple { Id = 1, Name = "Alice" };
        var rid = store.Insert(alice);
        heap.Add(rid);

        heap.Remove(rid);

        Assert.Empty(heap.EnumerateTuples(store).ToArray());
        Assert.True(store.TryGet(rid, out var tuple));
        Assert.Equal(alice, tuple); // The canonical store still owns the tuple.
    }

    [Fact]
    public void Add_DuplicateRid_EnumeratesTupleOnce()
    {
        var store = new InMemoryCanonicalTupleStore<TestTuple>();
        var heap = new InMemoryHeapStoreProvider<TestTuple>();

        var alice = new TestTuple { Id = 1, Name = "Alice" };
        var rid = store.Insert(alice);
        heap.Add(rid);
        heap.Add(rid);

        Assert.Equal([alice], heap.EnumerateTuples(store).ToArray());
    }

    [Fact]
    public void EnumerateTuples_FollowsHeapOrder_NotCanonicalInsertionOrder()
    {
        var store = new InMemoryCanonicalTupleStore<TestTuple>();
        var heap = new InMemoryHeapStoreProvider<TestTuple>();

        var alice = new TestTuple { Id = 1, Name = "Alice" };
        var bob = new TestTuple { Id = 2, Name = "Bob" };
        var carol = new TestTuple { Id = 3, Name = "Carol" };
        var aliceRid = store.Insert(alice);
        var bobRid = store.Insert(bob);
        var carolRid = store.Insert(carol);

        heap.Add(carolRid);
        heap.Add(aliceRid);
        heap.Add(bobRid);

        Assert.Equal([carol, alice, bob], heap.EnumerateTuples(store).ToArray());
    }
}
```

Corresponding change in `PolyStore.Tests`: the two migrated tests
(`EnumerateTuples_ResolvesRidsThroughCanonicalStore`,
`EnumerateTuples_SkipsRidsAbsentFromCanonicalStore`) are removed from
`InMemoryHeapStoreProviderTests`. No other test moves.

### 8.5 Build and test workflow integration

- **`dotnet build` / `dotnet test` / `dotnet format`.** All operate on the solution at
  the repository root; adding the project to `PolyStore.slnx` is the entire wiring.
  `dotnet test` discovers the project via `Microsoft.NET.Test.Sdk`; `dotnet format
  --verify-no-changes` (CI and pre-commit) covers the new sources automatically.
- **CI (`.github/workflows/cicd.yaml`).** **No changes required.** The `test` job's
  `dotnet test --collect:"XPlat Code Coverage" --logger:GitHubActions` runs the new
  project with coverage and GitHub Actions annotations, exactly as it runs
  `PolyStore.Tests`, because both projects carry `coverlet.collector` and
  `GitHubActionsTestLogger` (§8.2). The `publish` job's Release test run is likewise
  unaffected.
- **Local invocation.** `dotnet test` (all), or scoped:
  `dotnet test PolyStore.IntegrationTests` (the project is a positional argument;
  `--project` is not a valid switch on the pinned SDK), or filtered:
  `dotnet test --filter "FullyQualifiedName~HeapCanonicalStoreBridgeTests"`.
- **Documentation updates.**
  - `AGENTS.md`, Tests section — append the boundary rule and pointer, e.g.:

    > Tests that verify observable behavior crossing an architectural boundary
    > (access path ↔ canonical store, expressions ↔ optimization, executor ↔ storage,
    > API ↔ engine, transaction ↔ propagation) live in
    > `PolyStore.IntegrationTests`. Unit and component tests live in
    > `PolyStore.Tests`. The decision rule and conventions are defined in
    > `docs/design/integration-testing.md`.

  - `PolyStore.IntegrationTests/README.md` — purpose, the boundary rule (short form +
    pointer to this document), the §8.3 conventions, how to add a test, and how to run
    (mirroring `PolyStore.Benchmarks/README.md` in shape).
  - This document remains the authoritative definition of the boundary.

### 8.6 Future contribution model

The standing rules by which future features keep integration coverage honest, labelled
F1–F6 (see the labelling note in §8.1):

- **F1 — Cross-boundary behavior ships with a test.** A feature that changes observable
  behavior across an architectural boundary adds (or updates) an integration test in
  `PolyStore.IntegrationTests` exercising that boundary through the public API, in the
  same change as the implementation. The test is written *after* the behavior exists
  (D1) — never before, and never against a stub.
- **F2 — Single-component behavior ships with a unit test.** A feature that changes
  behavior within one component adds unit tests in `PolyStore.Tests`. A feature touching
  both adds both, asserting non-overlapping invariants (Dup1).
- **F3 — Regression tests follow the boundary of the bug.** A bug in the bridge
  contract (e.g. RID resolution, ordering ownership) gets its regression test in
  `PolyStore.IntegrationTests`; a bug in one component's internal contract (e.g. heap
  dedup) gets its regression test in `PolyStore.Tests` (AGENTS.md: regression test when
  practical).
- **F4 — New boundary, new folder.** When AB2–AB5 (or a newly named boundary) becomes
  implemented, the first integration test for it creates its folder per the entry-point
  rule (§8.2). No reserved empty folders; no tests of unimplemented behavior.
- **F5 — Shared fixtures stay local until duplication is real.** Fixtures remain local
  records in each test class. The trigger to extract a shared test-support assembly is
  three or more test classes in either project duplicating non-trivial fixture
  construction — at which point the extraction is its own small design decision (the
  rejected alternative C, §6, becomes viable).
- **F6 — Anti-drift check.** In review, a test in the "wrong" project is a defect
  against the §8.1 decision procedure, not a style preference: a unit test that crosses
  a boundary is moved (Dup3); an integration test that crosses none is rejected (Dup2).

Worked examples of what F1 produces as boundaries land (illustrative, not pre-built):

| When this lands | Integration test added | Boundary |
|---|---|---|
| `ITransaction` implementation + executor over the in-memory stores | `Execution/TransactionTests`: insert via `ExecuteAsync`, commit, read back; uncommitted state not visible | AB3/AB4 |
| `DatabaseBuilder.Source` + `DatabaseContext.From<T>` | `Hosting/SourceRegistrationTests`: a source registered via the builder is queryable through the context | AB4 |
| `IPropagateChanges` engine | `Execution/DerivedRelationPropagationTests`: apply a change set, assert propagated state equals `Define()` re-evaluated (the ARCHITECTURE.md §22 property) | AB5 |
| `OptimizationPipeline` application + a rewriter | remains a unit test in `PolyStore.Tests` unless the rewriter crosses AB2 observably | AB2 |

---

## 9. Data / Execution Flow

The behavior the initial set pins, across AB1:

```text
                 canonical store owns tuples          access path owns RID order
                        │                                       │
   tuple ──Insert──► InMemoryCanonicalTupleStore ──(RID)──► InMemoryHeapStoreProvider
                        │                                       │
                        │            EnumerateTuples(store)     │
                        └────────── resolve each RID ───────────┘
                                       │
                                       ▼
                        tuples, in heap order; dangling RIDs skipped
```

The test lifecycle every integration test follows:

```text
Arrange:  construct components + fixture data via public API (deterministic, local)
Act:      drive the cross-boundary interaction through the public surface
Assert:   observe the combined outcome through the public surface (values, order, state)
Dispose:  using / await using for any IDisposable fixture (none in the initial set)
```

xunit instantiates a fresh test class per test; there is no shared state, no ordering
dependence, and no setup/teardown beyond what the test method performs (D4).

---

## 10. Failure Behavior

- **Red-by-construction tests are forbidden (D1).** A test whose Act or Assert would
  reach a `NotImplementedException` stub fails; the rule is that such a test is not
  written until the behavior exists. The project is therefore green against the current
  implementation by construction, and a `NotImplementedException` observed in a test is
  always a genuine signal (a stub was reached that should not have been, or a test was
  added ahead of its feature).
- **No vacuous passes.** An integration test must assert an observable cross-boundary
  outcome (value, order, state, or exception type). A test that only verifies
  construction succeeded asserts no boundary and is rejected (C4; the benchmarking
  design's C4 "Fail explicitly"; AGENTS.md Error Handling).
- **Failure means cross-boundary regression.** A failing integration test indicates the
  combined contract broke, possibly in either component. The remedy is to fix the
  engine or correct the expectation against `ARCHITECTURE.md` — never to weaken the
  assertion to restore green (C4).
- **CI behavior is unchanged.** Test failure fails the `test` job exactly as today;
  there are no new gates, retries, or special handling.
- **Coverage gaps are review-detected, not build-detected.** Two omissions are visible
  only in review, not in the build or CI. First, if a boundary (AB2–AB5) is implemented
  and its author simply omits the integration test, nothing fails: F1 is a review-time
  convention, and D1 means the project never carries an expected-but-failing
  placeholder that would surface the gap. That is an accepted limitation at the current
  stage; the trigger to close it mechanically is the first of AB2–AB5 landing, at which
  point a small boundary-coverage-index test — asserting that each implemented boundary
  has at least one test in `PolyStore.IntegrationTests` — is justified. Second, the
  mirror case: if the implementer forgets to remove the two migrated tests from
  `PolyStore.Tests` (§8.4), the resulting duplication is likewise caught only by review
  against Dup3, not by the build.

---

## 11. Implications

- **Testing (primary).** `PolyStore.Tests` loses two tests (moved, §8.4) and gains a
  neighbor project. The §8.1 decision procedure and §8.6 contribution rules become part
  of review criteria.
- **CI / build.** None beyond solution membership: `dotnet build`, `dotnet test`,
  `dotnet format`, coverage collection, and the pre-commit/pre-push hooks all pick the
  project up automatically (§8.5). No workflow file changes.
- **APIs.** No `PolyStore` public API changes. No `InternalsVisibleTo`.
- **Storage / access paths.** No engine code changes. The initial set *pins* the
  canonical-authority and ordering-ownership invariants (ARCHITECTURE.md §4/§6), which
  constrains future storage work in exactly the way the architecture intends.
- **Planner / executor / transactions / propagation.** Unaffected today; §8.6 F1/F4
  define how their future implementations acquire integration coverage.
- **Serialization.** None.
- **Concurrency.** Out of scope; no concurrent tests are introduced (the in-memory
  stores document that they do not provide thread safety).
- **Diagnostics.** Test output flows through the existing `GitHubActionsTestLogger`
  wiring; no new diagnostic surface.
- **Future vectorization / batching.** Irrelevant to this project; performance
  measurement remains `PolyStore.Benchmarks`' territory (§12).
- **Provider implementations.** A second storage provider or a B-tree path is covered by
  adding boundary tests (e.g. a `BTreePathCanonicalStoreBridgeTests` class) under the
  same conventions — no harness change (F1, F4).

---

## 12. Performance Considerations

- **The initial set is millisecond-scale.** Six tests, fixtures of two or three tuples,
  in-memory components. There is no dataset to build and no I/O; `InMemoryHeapStoreProvider`
  O(n) duplicate checks are irrelevant at n ≤ 3. CI time impact is negligible.
- **Small fixtures by default; scale requires justification.** The initial set uses
  two- or three-tuple fixtures, and that remains the default. A proposed integration
  test that needs a dataset beyond a few thousand tuples must justify the scale in the
  PR: some *correctness* properties are only meaningful at moderate scale (e.g. the
  ordering of a range scan once a B-tree path lands, or propagation correctness for a
  large change set — `ARCHITECTURE.md` §21–§22). What never belongs in this project is
  *performance*: dataset-scale measurement is `PolyStore.Benchmarks`' territory.
- **No performance assertions in tests.** Timing, allocation, and throughput claims
  belong to the benchmarking design (`docs/design/benchmarking.md`); tests assert
  semantics only (C1).
- **Parallelism is safe.** xunit runs test classes in parallel by default; there is no
  shared mutable state (D4), so no synchronization is introduced or needed.

---

## 13. Testing Strategy

The strategy for this feature is deliberately small and self-verifying:

1. **Harness validation.** The project builds, is discovered by solution-level
   `dotnet test`, reports six passing tests, and appears in coverage output —
   establishing that the wiring (csproj, slnx, package set) is correct (D5).
2. **Convention validation by example.** The migrated pair demonstrates Dup3 (move, don't
   copy); the four new tests demonstrate boundary naming, AAA, local fixtures, and
   public-surface-only assertions. The convention is shown, not just stated.
3. **Invariant pinning.** The initial set pins the AB1 contract — canonical authority,
   access-path ordering ownership, dedup-through-the-bridge, and dangling-RID
   tolerance — so the first engine work that touches the store/heap interaction is
   checked against the architecture's stated invariants (ARCHITECTURE.md §4/§6).
4. **Ongoing coverage.** §8.6 F1–F6 define how every subsequent feature contributes;
   the §8.1 decision procedure is the review-time check that keeps the two projects
   from drifting or duplicating.
5. **Verification commands** (per AGENTS.md): `dotnet build`, `dotnet test`,
   `dotnet format --verify-no-changes` — all at the repository root, all green.

---

## 14. Assumptions

- **A1.** The package set and versions of `PolyStore.Tests` (xunit 2.9.3,
  Microsoft.NET.Test.Sdk 18.10.1, xunit.runner.visualstudio 4.0.0, coverlet.collector
  10.0.1, GitHubActionsTestLogger 3.0.5) are appropriate for the new project. There is
  no reason to differ (R3); they are the repository's current, CI-verified set.
- **A2.** The only cross-boundary behavior implemented today is the AB1 bridge
  (§5.1). This was verified by reading every `PolyStore` source file; if a later audit
  finds additional implemented cross-boundary behavior, the initial set is *extended*,
  not restructured.
- **A3.** Solution-level `dotnet test` discovers every test project in `PolyStore.slnx`
  (standard MSBuild/test-sdk behavior, already relied on by the existing CI).
- **A4.** xunit 2.x semantics (fresh class instance per test, no implicit shared state)
  continue to apply, as they do for `PolyStore.Tests`.
- **A5.** `HelloWorld` remains an aspirational sample and is not a dependency of any
  test project; tests own their fixtures.
- **A6.** The two migrated tests retain their names and semantics; only their project
  (and therefore namespace) changes.
- **A7.** No reviewer or contributor requires integration tests to run in a separate CI
  job or with a separate policy; one `dotnet test` invocation covering both test
  projects is the intended workflow.

---

## 15. Open Questions

None block the design; each is flagged for when the relevant area lands.

1. **OQ1 — Pre-existing naming inconsistency.** The `PolyStore.Tests` class
   `InMemoryCanonicalTupleStore` lacks the `Tests` suffix `AGENTS.md` prescribes.
   Recommended: rename it to `InMemoryCanonicalTupleStoreTests` as a standalone
   housekeeping commit, *separate from* this feature, so this change's diff stays
   focused. Not part of this design.
2. **OQ2 — Folder ownership of AB5 tests.** Propagation tests may start from
   `ITransaction` (→ `Execution/`) or from a relation definition (→ `Core/`). The
   entry-point rule (§8.2) resolves this per test when AB5 lands; no decision is
   needed now.
3. **OQ3 — Separate CI job for integration tests.** If the integration suite grows
   large or slow enough to matter, a dedicated CI job (still solution-scoped, still
   gating) could be added for reporting clarity. Not needed at six tests; deferred.
4. **OQ4 — `InternalsVisibleTo` for testability.** If a future boundary is genuinely
   untestable through the public surface, exposing internals to the test assembly is a
   per-feature decision that must be justified against the public-surface rule (D2).
   Not anticipated for AB1–AB5 as currently designed.

---

## 16. Implementation Outline

A plan for a later implementation agent; **not** an implementation. Phases are ordered
by dependency; each is independently shippable and small (R9).

**Phase 1 — Project + initial test set.**

1. Create `PolyStore.IntegrationTests/PolyStore.IntegrationTests.csproj` (§8.2).
2. Add the project to `PolyStore.slnx` after `PolyStore.Tests` (§8.2).
3. Add `Storage/HeapCanonicalStoreBridgeTests.cs` with the six tests of §8.4.
4. Remove the two migrated tests from
   `PolyStore.Tests/Storage/Impl/InMemoryHeapStoreProviderTests.cs` (Dup3).
5. Verify: `dotnet build` (solution) green; `dotnet test` (solution) reports the
   `PolyStore.Tests` suite minus two tests plus six new passing tests; `dotnet format
   --verify-no-changes` clean.

*Depends on: nothing.*

**Phase 2 — Documentation.**

1. Add `PolyStore.IntegrationTests/README.md` (purpose, boundary rule short form +
   pointer, §8.3 conventions, how to add/run a test).
2. Append the boundary paragraph to the `AGENTS.md` Tests section (§8.5).
3. Verify: the README and AGENTS.md text agree with this document; no contradictions
   with `docs/design/benchmarking.md`.

*Depends on: Phase 1* (the README references the initial test class by name).

Commit style (one coherent commit per phase, per AGENTS.md):

```text
test(integration): add integration test project and storage bridge coverage
docs(testing): define unit/integration boundary in AGENTS.md and project README
```

Out of scope for all phases: shared fixture assemblies, reserved folders, external
infrastructure, separate CI jobs, and any test of unimplemented behavior — each
deferred to the trigger that would make it meaningful (§8.6, §15).
