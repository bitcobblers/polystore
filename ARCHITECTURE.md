# PolyStore Architecture

PolyStore is an experimental C#-first relational storage and dataflow engine.
A relation describes logical data; access paths describe ways to find that
data. The intended engine separates logical queries, physical execution, and
authoritative storage, and maintains dependent relations transactionally.

**Reconciled:** 2026-10-11 against `main` at
`e910c86534ac28cd292fa6d53c8eb3575cdb63f9` (`chore: removed old implementations.`).
This identifies the reconciliation baseline, not coverage of later source changes.

## 1. Document Authority and Status

This document distinguishes three states:

- **Implemented:** executable behavior present in source.
- **Declared:** an API exists, but its engine behavior is absent or stubbed.
- **Intended / open:** architectural direction or a decision requiring design.

Source establishes what exists and what it currently does. A declaration,
comment, sample, or proposal does not establish implemented engine behavior.
Architectural intent constrains future work; it does not make missing APIs
exist. An explicitly approved feature design can specify a deliberate change
to the baseline. Semantic conflicts require human resolution.

The storage reset removed implementations that preceded a runtime tuple
contract. Define tuples and schemas before deriving replacement storage
interfaces. Do not restore the removed implementations as the default foundation.

## 2. Current Repository

The solution is `PolyStore.slnx`. All five projects target `net10.0`.

| Project                      | Current responsibility and state                                                                                                            |
|------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------|
| `PolyStore`                  | Relation/query contracts, path configuration stubs, hosting/compiler skeletons, and the implemented `Rid` value type.                       |
| `PolyStore.Tests`            | xUnit unit/component test project; currently no C# tests.                                                                                   |
| `PolyStore.IntegrationTests` | xUnit integration test project; currently no C# tests.                                                                                      |
| `PolyStore.Benchmarks`       | BenchmarkDotNet runner, RID creation/equality/hashing benchmarks, and dataset-size constants. Storage benchmarks and fixtures were removed. |
| `HelloWorld`                 | Authoring examples and syntax experiments; not a functioning engine demonstration.                                                          |

No canonical-store interface or implementation, heap/B-tree store implementation,
key-schema/encoding primitives, runtime tuple/schema model, query provider,
relational IR, physical planner, operator executor, or transaction implementation
remains in source.

### 2.1 Relation and query declarations

These types live in `PolyStore/Core/`:

| Type                       | Current contract                                                                                                                                                                                                |
|----------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `IRelation<T>`             | Marker; does not implement `IQueryable<T>` or enumerate data.                                                                                                                                                   |
| `ISource<T>`               | Extends `IRelation<T>`; declares `InsertAsync`, `UpdateAsync`, and `DeleteAsync`, each accepting `T` and an optional cancellation token and returning `ValueTask`.                                              |
| `IRelationContext`         | Declares `From<T>()`, `From<T, TPath>() where TPath : IAccessPath<T>`, and `FromValues<T>(params T[] values)`, returning `IQueryable<T>`.                                                                       |
| `IDerived<out T>`          | Declares `IQueryable<T> Define(IRelationContext context)`. It does not inherit `IRelation<T>`.                                                                                                                  |
| `IPropagateChanges<out T>` | Extends `IDerived<T>`; declares `IQueryable<T> Propagate(IRelationChangeContext context)`.                                                                                                                      |
| `IRelationChangeContext`   | Extends `IRelationContext`; declares `Inserts<T>()`, `Deletes<T>()`, `Updates<T>()`, and `Combine<T>(params IQueryable<T>[] paths)`. Updates return `IQueryable<Change<T>>`; the others return `IQueryable<T>`. |
| `Change<T>`                | Record containing `OldValue` and `NewValue`.                                                                                                                                                                    |
| `RelationChange<T>`        | Abstract record with sealed `Insert<T>(Value)`, `Delete<T>(Value)`, and `Update<T>(Before, After)` records. Separate from `Change<T>`; no conversion or propagation wiring is implemented.                      |
| `RelationAttribute`        | Class-targeted attribute with a writable `Name`, defaulting to the empty string. No naming-precedence or discovery implementation exists.                                                                       |

`IQueryableExtensions` declares C# extension members for relational DML.
Every member currently throws `NotImplementedException`:

- `Update<TResult>(Expression<Func<T, TResult>>)` returns `IQueryable<TResult>`.
- `Update<TTarget, TUpdate>(target, update)` returns `IQueryable<T>`.
- `Insert<TInsert>(Expression<Func<T, TInsert>>)` returns `IQueryable<TInsert>`.
- Parameterless `Insert()`, `Delete()`, and `Update()` return `IQueryable<T>`.
- `Fork<TResult>(params Expression<Func<IQueryable<T>, IQueryable<TResult>>>[] changes)`
  returns `IQueryable<TResult>`.

Signatures exist; mutation targeting, affected-row identity, expression capture,
and composition semantics still need implementation contracts. Return types
alone do not establish executable DML behavior.

### 2.2 Hosting, compilation, and transactions

- `DatabaseBuilder.Source<T>(string name)` and
  `Relation<T>(string name, Func<DatabaseContext, IRelation<T>> factory)` throw
  `NotImplementedException`.
- `DatabaseContext` implements `IRelationContext`; all three query methods throw
  `NotImplementedException`. `DatabaseModule.Configure(DatabaseBuilder)` is abstract.
- `IRelationRewriter` is a marker. `OptimizationPipeline.Add<T>()`, constrained
  to `IRelationRewriter, new()`, constructs and stores a rewriter and returns
  the pipeline. There is no rewrite or optimization execution method.
- `ITransaction : IAsyncDisposable` declares
  `IAsyncEnumerable<T> ExecuteAsync<T>(Expression<Func<IRelationContext, IQueryable<T>>> query, CancellationToken cancellationToken = default)`
  and `ValueTask CommitAsync(CancellationToken cancellationToken = default)`.
  There is no implementation, snapshot API, transaction identifier, or rollback implementation.

`HelloWorld` uses a null transaction as a syntax placeholder. Its derived
relation examples are authoring ideas, not validated propagation correctness
or an inventory of engine-supported expressions.

## 3. RID: Implemented Identity Primitive

`PolyStore/Storage/Rid.cs` defines `public readonly struct Rid() : IEquatable<Rid>`.
Its private value is a `Guid` initialized using `Guid.CreateVersion7()`.

- `new Rid()` invokes the constructor and generates a GUID-v7 value.
- `default(Rid)` is zero-initialized; it does not run that constructor.
- Typed/object equality, `==`, `!=`, and hashing delegate to the underlying GUID.
- `ToString()` uses GUID format `"N"`: 32 hexadecimal digits without separators.

There is no public GUID accessor, constructor from an existing GUID, parsing
API, ordering API, span-formatting API, or explicit validity check.
`IParsable<Rid>` is not implemented. Formatting/parsing designs must identify
intended additions rather than treating them as existing contracts.

A RID is intended to identify a stored record logically, independently of its
physical placement. The value type does not establish relation scope,
ownership, persistence encoding, version lookup, or whether the zero value
is valid as a stored identity. Those storage contracts remain open. Do not
use `default(Rid)` as an absent-source sentinel without an explicit contract.

## 4. Typed Authoring and Schema-Driven Runtime

**Intended:** C# generics provide typed query construction, registration, and
value conversion at the authoring boundary. Physical storage and execution
must support dynamic schemas without requiring a CLR entity type `T` for
every record or operator.

The existing generic relation, source, and access-path APIs are authoring
surfaces. They do not justify generic canonical stores or generic physical
operators. Do not de-genericize those authoring APIs merely to establish the
runtime boundary.

The runtime schema model and conversion boundary are not implemented.
Future designs must distinguish logical attribute identity, execution slots,
and physical encoding. CLR inheritance or a property name alone must not
silently determine storage identity. Supported CLR construction patterns,
relation naming, nullability, and comparison semantics require explicit rules.

## 5. Canonical Records, Access-Path Entries, and Execution Tuples

These responsibilities are distinct; no common concrete representation has
been selected.

| Concept           | Intended responsibility                                                                                                                                   |
|-------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------|
| Canonical record  | Authoritative stored attribute values, addressed through RID within a defined storage/relation scope.                                                     |
| Access-path entry | RID plus path-specific keys and optional included values, optimized for locating records.                                                                 |
| Execution tuple   | Values flowing between operators, potentially spanning multiple relation instances and computed expressions, with identity for needed downstream lookups. |

A canonical record need not share an execution tuple's layout. Access paths
must not become competing authorities for logical record contents.
Authoritative storage remains architectural intent; its API and physical
representation are deliberately absent after the reset.

Before implementing a replacement store, define the relevant schema/value
contracts, RID scope/ownership, lookup behavior, and failure semantics. Do
not infer a replacement `ICanonicalTupleStore` interface from the removed
generic interface or the old tuple proposal.

## 6. Access Paths and Included Values

**Declared:** `IAccessPath<T>` contains only `void Configure()`.
`BTreePath<T>` and `HeapPath<T>` implement it with empty virtual methods.

`BTreePath<T>` exposes protected `Column(Expression<Func<T, object?>>)` and
`Include(Expression<Func<T, object?>>)` methods. Their intended meanings are
ordered key components and non-key payload respectively. Both are currently
no-ops: they record no expressions, keys, or coverage metadata.

`HeapPath<T>` exposes a no-op protected `Include(...)`, `PageSize` defaulting
to `8192`, and `FillFactor` defaulting to `0.90`. These are configuration
properties, not implemented pages, packing, or validation behavior.

**Intended:** heap entries can contain only a RID; B-tree entries contain keys
and RIDs; either path can carry included attributes. Included values can
avoid canonical lookup at the cost of space and maintenance work. A relation
may have multiple paths; no heap or other path is implicitly created.
Columnar/vector paths remain possibilities with no declarations or implementations.

An explicit path constrains initial access, not the complete set of readable
attributes. Missing values can be fetched through the corresponding RID once
canonical lookup and materialization contracts exist.

## 7. Query Binding, Provenance, and Materialization

**Intended:** late materialization preserves source identity through operators
so missing attributes can be retrieved after filtering or joining.

Identity must distinguish **query relation instances**, including two uses of
the same relation in a self-join. A single RID slot keyed only by relation type
or name is insufficient. Bind source-instance identities before planning and
execution, and preserve their meaning through nested joins and rewrites. The
exact identity type and remapping rules remain to be designed.

Hidden RID columns in physical output layouts are the current design direction
under consideration. They could let ordinary slot operations carry provenance
without a separate per-row collection. This is not implemented or a completed
tuple contract.

The replacement design must account for:

- Mapping each recoverable attribute to the correct source instance and RID.
- Distinguishing an unavailable attribute, an available SQL null, and an absent
  source instance on an outer-join row.
- Declaring which values each operator supplies. Uniform availability may be
  described by the output layout; a per-row mask is not an established requirement.
- Materializing predicate, key, and projection inputs before their consuming
  operation. Delaying lookup must respect those dependencies.
- Preserving hidden identity only while needed, without making it part of
  user-visible projection, equality, distinctness, or grouping semantics.
- Defining provenance retention/loss separately for aggregation and each set
  operation. Do not combine different rows' provenance or advertise attributes
  absent from an emitted row merely because both branches occur in a plan.
- Performing canonical lookup in the correct transaction/version context.

Plan-time source metadata and runtime RID values have different meanings.
Their concrete types, tuple ownership/lifetime, buffer reuse, null representation,
and row/batch layouts remain open.

## 8. Query Planning and Execution

**Intended:** expression trees describe relational intent. An internal
representation and planner will select paths/operators, track required values,
and insert materialization. Neither that representation nor an executable
planning pipeline currently exists.

`From<T, TPath>()` declares explicit path selection. Its intended meaning is
a requirement rather than an advisory hint: an impossible constraint must
produce a diagnostic. Nested-loop constraints were previously discussed, but
there is no PolyStore `NestedLoop` API in source.

Physical operators should consume a common runtime representation across
heterogeneous paths. Pull/Volcano execution remains a conceptual starting
point; there is no `Open`/`Next`/`Current`/`Close` operator contract. Row,
vectorized, and hybrid execution remain design and measurement choices.
Cancellation, streaming, memory budgets, and async I/O need explicit contracts.

## 9. Mutations and Transactional Propagation

**Intended:** mutations remain relationally composable. Derived relations have
a full definition and may have incremental maintenance over inserts, updates,
and deletes. Existing signatures in §2 do not resolve targeting or execution.

Required downstream maintenance belongs to the same committed transaction
as source changes. Observers must not see a committed upstream change while
required internal dependents still reflect an older state. Async execution
does not permit fire-and-forget maintenance after commit.

The engine is intended to own dependency ordering over an acyclic relation
graph. Discovery, cycle validation, change accumulation, rollback, concurrency
control, and visibility are not implemented.

Where full definition and incremental maintenance both exist, their resulting
logical relations should agree for the same source state. An empty incremental
result can be valid; it must not automatically trigger recomputation.
Transaction-aware reads remain an intended concern; no current API establishes
an as-of transaction contract or MVCC representation.

## 10. Validation and Performance

Validate schemas, expressions, path coverage, constraints, and graph structure
at the earliest stage with sufficient information. Unsupported behavior must
fail explicitly, without silently dropping rows or constraints. These engine
validation mechanisms still need implementation.

Use unit/component tests for individual semantics and integration tests for
observable behavior emerging across implemented architectural boundaries.
The test projects exist but contain no tests at this baseline. An empty
successful run is not evidence of engine coverage.

The benchmark runner discovers workloads through its assembly and writes
results beneath `artifacts/`. Only RID creation/equality/hashing benchmarks
currently exist. No measured tuple-layout or storage-performance guarantees
are established here. Measure allocation, copying, boxing, lookup, and batching
costs when choosing representations.

## 11. Design Documents and Decision Maintenance

`docs/design/` contains earlier tuple, integration-testing, and benchmarking
proposals. Their status headers remain proposal-oriented, and their baseline
descriptions reference removed code. The integration/benchmark READMEs also
list removed tests, fixtures, or benchmarks. Historical numbered architecture
references may refer to the previous version of this document.

- [Tuple representation](docs/design/tuple-representation.md) needs revision
  before implementation. Its store migration, `TupleValue`, masks, codec,
  registry, and provenance APIs are proposed structures, not current source
  or a replacement contract endorsed by this reconciliation.
- [Integration testing](docs/design/integration-testing.md) provides useful
  boundary/convention rationale; its old heap/canonical bridge examples are
  not current behavior requirements.
- [Benchmarking](docs/design/benchmarking.md) provides methodology; its removed
  store workloads and fixtures are not the current benchmark inventory.

Human approval may have occurred outside a document's status header. Resolve
the exact approved revision for a feature; neither a proposal header nor a
past approval makes obsolete source claims accurate.

When a design is approved, summarize its durable decisions here and link the
exact design, marking accepted but unimplemented decisions accordingly.
When implementation lands, update current-state descriptions and remove
resolved open questions. Replace or explicitly mark superseded decisions;
do not leave conflicting alternatives beside each other as current guidance.

## 12. Open Decisions and Dependency Order

The immediate dependency order is:

1. Define runtime schemas, execution values/layouts, and source-instance identity.
2. Define canonical record representation and lookup/identity contracts.
3. Derive storage and access-path runtime interfaces from those contracts.
4. Implement storage, translation, planning, and execution in approved increments.

| Area                     | Unresolved contract                                                                                                                                                              |
|--------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Schema / CLR boundary    | Attribute identity and slot assignment, registration naming precedence, CLR construction, conversion, null and comparison semantics. No shared key semantics remain implemented. |
| Execution tuples         | Layouts, ownership/lifetime, availability metadata, hidden RID representation, outer-join absence, and provenance through projections/sets/aggregates.                           |
| Canonical storage        | Record layout, RID scope/ownership/default validity, requested-attribute lookup, missing-record behavior, persistence encoding, recovery, and schema evolution.                  |
| Access paths             | Expression recording/validation, key comparison/ordering, coverage, maintenance, creation/rebuild, and additional path types.                                                    |
| Planner / executor       | IR, constraints, costing/statistics, rewrites, row/batch execution, async I/O, parallelism, and memory limits.                                                                   |
| Mutations / transactions | Target identity, DML semantics, change-record integration, version visibility, isolation, rollback, logging, and recovery.                                                       |
| Propagation              | Dependency discovery, cycle rejection, old/new transitions, aggregation deltas, batching, and explicit recomputation policy.                                                     |

These are design tasks. Coding agents must not settle them implicitly while
implementing an unrelated feature.
