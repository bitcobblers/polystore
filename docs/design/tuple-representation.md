# Runtime Tuple Representation

**Status:** Proposed (revised)
**Date:** 2026-09-27
**Revised:** 2026-09-27 — addressed design and performance review findings (DR-1…DR-10, PR-1…PR-10);
separated logical attribute identity (name), runtime slot (ordinal), and physical
encoding/layout (§5.2, I-IDENT/I-SLOT/I-PHYSICAL); added the relation naming invariant
(I-NAME, §5.2); replaced the single-source RID invariant with a provenance model
(§5.3, §5.8) enabling late materialization after combining operators.

**Revised:** 2026-09-27 (second round) — addressed DR-1/DR-3 (added `Instance` discriminator to
`ProvenanceEntry`; `GetRid` single-arg throws `AmbiguousProvenanceException` on duplicate
source, two-arg disambiguates; §5.3, §5.8, §10.2, §12); DR-2 (mask invariant reworded to
"fixed mask (input ∪ materialized)"); DR-4 (shared `ulong[]` mask backing specified as
copy-on-write, never mutated after publication); PR-1 (`TupleProvenance` backing specified:
inline first entry + overflow array; zero heap allocations for single-source, one for
multi-source; §5.3, §8); PR-2 (selective merge batches all *m* attributes of a source into a
single O(width) pass; `WithMaterializedMany` added; §5.3, §5.8 rule 3, §8).

**Revised:** 2026-09-27 (third round) — addressed design-reviewer findings: added the
`TupleProvenance.Create` factory (besides the empty default and `Union`), which validates
unique (Source, Instance) pairs (`Union` enforces the same); clarified that instance IDs
must be **unique per source** across a provenance, with 0/1 as the common 2-way join case
and nested joins / multi-way self-joins requiring unique-per-source assignment (§5.3, §5.8,
§10.2, §12).

**Revised:** 2026-09-27 (fourth round) — addressed performance-reviewer finding PR-1:
added non-`params` `Create(ProvenanceEntry)` and `Create(ProvenanceEntry, ProvenanceEntry)`
overloads so the common scan (0 heap allocations) and 2-way join (1 allocation) hot paths do
not pay the `params` array allocation at the call site; the `params` overload remains for the
general multi-way case (§5.3, §8, §10.2).

**Revised:** 2026-09-27 (fifth round) — addressed design-consistency-reviewer findings
CR-1…CR-7: documented that `OutputSignature.Provenance` uses placeholder RIDs at plan
time — only (Source, Instance) pairs are meaningful (§5.8); §5.12 projection output edges
now reference the deferred projection reconstruction API (§12 item 9) instead of the
identity-only codec; corrected the §8 "purely to carry provenance" phrasing (the null
values array represents the empty set of materialized values); §5.12 join example now uses
`OrderId = o.Id` (the `Order` model's attribute is `Id`), the order scan signature uses
`Id`, and the join signature no longer lists `CustomerId`; Phase 2 now includes
`AmbiguousProvenanceException` (§13); §8's multi-source allocation claim is qualified per
overload (two-entry `Create`: 1 allocation; `params`: 2 allocations).

**Scope:** This document designs the *runtime representation of relation tuples* and the
boundary between the typed authoring layer and the physical execution layer. It is **not** a
planner or executor design: no operator tree, costing model, or translation pipeline is
specified beyond the representation they must operate on and the invariants they must
preserve.

---

## 1. Problem Statement

PolyStore's authoring model is fully generic over the relation's CLR type `T`:
`IRelation<T>`, `ISource<T>`, `IQueryable<T>`, `IDerived<T>`, `IPropagateChanges<T>`,
`BTreePath<T>`, `HeapPath<T>`, and `ICanonicalTupleStore<T>` all take `T` as their unit of
value.

The architecture commits to a model in which `T` alone is insufficient for the physical layer:

1. **Access paths expose attribute subsets.** A B-tree path stores `Key -> RID + payload`
   (`ARCHITECTURE.md` §6–§7); a heap stores only RIDs. A scan over such a path yields a
   tuple in which most of the relation's attributes are *not present*.
2. **Missing attributes must be recoverable.** The planner may introduce a canonical
   lookup through the RID to obtain attributes the access path does not carry
   (`ARCHITECTURE.md` §8).
3. **Operators must compose across heterogeneous paths** without knowing each path's
   internal representation (`ARCHITECTURE.md` §15).
4. **The canonical store is the authoritative tuple representation** and is addressed by
   RID (`ARCHITECTURE.md` §4–§5); it must not be coupled to a particular CLR type.

Today, none of these can be expressed:

- `ICanonicalTupleStore<T>.TryGet` (`PolyStore/Storage/ICanonicalTupleStore.cs`) returns a
  complete `T`. There is no representation for "a tuple of which only some attributes are
  materialized."
- There is no schema or attribute-identity model. An attribute is implicitly "a property of
  `T`," discoverable only by reflection at the point of use.
- `BTreePath<T>.Column`/`Include` and `HeapPath<T>.Include`
  (`PolyStore/Storage/BTreePath.cs`, `PolyStore/Storage/HeapPath.cs`) record nothing; the
  planner has no way to know which attributes a path provides.
- `InMemoryHeapStoreProvider<T>.EnumerateTuples`
  (`PolyStore/Storage/Impl/InMemoryHeapStoreProvider.cs`) resolves **every** RID to a full
  `T` and *silently skips* RIDs that do not resolve — conflating "scan" and "materialize"
  and quietly converting a consistency failure into a missing row.
- There is no value that flows between physical operators. `T` is the only currency, so
  every operator is implicitly generic over the relation's CLR type.

The central question this document answers:

> What is the runtime value that physical operators produce and consume, how is its shape
> described, how is partial materialization represented and tracked, and where exactly do
> the generic CLR types end?

---

## 2. Current State

Verified against the repository (this is the full surface relevant to the design):

| Area | File | What exists today |
|---|---|---|
| Relation marker | `PolyStore/Core/IRelation.cs` | `public interface IRelation<T>;` — marker only. |
| Source | `PolyStore/Core/ISource.cs` | `ISource<T> : IRelation<T>` with `ValueTask InsertAsync(T, CancellationToken)`, `ValueTask UpdateAsync(T, CancellationToken)`, `ValueTask DeleteAsync(T, CancellationToken)` (all cancellation parameters optional). |
| Query context | `PolyStore/Core/IRelationContext.cs` | `From<T>()`, `From<T, TPath>() where TPath : IAccessPath<T>`, `FromValues<T>(params T[])`. |
| Change context | `PolyStore/Core/IRelationChangeContext.cs` | `Inserts<T>() -> IQueryable<T>`, `Updates<T>() -> IQueryable<Change<T>>`, `Deletes<T>() -> IQueryable<T>`, `Combine<T>(...)`. |
| Derived relations | `PolyStore/Core/IDerived.cs`, `IPropagateChanges.cs` | `Define(IRelationContext)`, `Propagate(IRelationChangeContext)` — both return `IQueryable<T>`. |
| Change values | `PolyStore/Core/Change.cs`, `RelationChange.cs` | `record Change<T>(T OldValue, T NewValue)`; `Insert<T>`, `Delete<T>`, `Update<T>` records carrying full `T` values. |
| DML stubs | `PolyStore/Core/IQueryableExtensions.cs` | `Update`, `Insert`, `Delete`, `Fork` — all `NotImplementedException` (C# 14 extension block). |
| Relation marker attr | `PolyStore/Core/RelationAttribute.cs` | `[Relation(Name = ...)]` on the CLR type. |
| Access path iface | `PolyStore/Storage/IAccessPath.cs` | `IAccessPath<T>` with a single `Configure()`. |
| B-tree path | `PolyStore/Storage/BTreePath.cs` | `protected Column(Expression<Func<T, object?>>)`, `protected Include(...)` — **no-op stubs**. |
| Heap path | `PolyStore/Storage/HeapPath.cs` | `protected Include(...)` — **no-op stub**; `PageSize`, `FillFactor`. |
| Canonical store | `PolyStore/Storage/ICanonicalTupleStore.cs` | `ICanonicalTupleStore<T>`: `Count`, `Insert(T) -> Rid`, `TryGet(Rid, out T)`, `Delete(Rid)`. |
| RID | `PolyStore/Storage/Rid.cs` | `readonly struct Rid` wrapping a `Guid.CreateVersion7()`; value equality. (Doc comment says "non-negative integer"; the code is authoritative — it is a Guid v7.) |
| In-memory store | `PolyStore/Storage/Impl/InMemoryCanonicalTupleStore.cs` | `Dictionary<Rid, T>`. |
| In-memory heap | `PolyStore/Storage/Impl/InMemoryHeapStoreProvider.cs` | `List<Rid>`; `EnumerateTuples(ICanonicalTupleStore<T>)` resolves each RID to a full `T`, skipping unresolvable RIDs. |
| Placeholders | `PolyStore/Storage/Impl/RowStorageRelationStore.cs`, `ColumnStorageRelationStore.cs` | Empty classes. |
| Transaction | `PolyStore/Execution/ITransaction.cs` | `IAsyncEnumerable<T> ExecuteAsync<T>(Expression<Func<IRelationContext, IQueryable<T>>>, CancellationToken)`, `ValueTask CommitAsync(CancellationToken)`. |
| Compiler | `PolyStore/Compiler/IRelationRewriter.cs`, `OptimizationPipeline.cs` | Empty marker + rewriter list. |
| Hosting | `PolyStore/Hosting/DatabaseBuilder.cs`, `DatabaseContext.cs`, `DatabaseModule.cs` | Stubs: `Source<T>(name)`, `Relation<T>(name, factory)`. |
| Sample | `HelloWorld/Models.cs` | `Customer` record with nested `Heap : HeapPath<Customer>` and `ById : BTreePath<Customer>` (`Column(x => x.Id)`, `Include(x => x.FirstName)`); `ArchivedCustomer : Customer` (note: **inherits** from the source type); `OrderByCustomer` using `GroupBy`, `LeftJoin` (not defined anywhere — the sample is aspirational and does not currently compile), `Fork`. |
| Tests | `PolyStore.Tests/Storage/Impl/` | Tests for the two in-memory stores. (Note: the canonical-store test class is named `InMemoryCanonicalTupleStore`, missing the `Tests` suffix required by `AGENTS.md`.) |

Environment facts: `net10.0`, nullable enabled, `ImplicitUsings` disabled in `PolyStore`,
C# 14 features in use (extension blocks). `docs/design/` is empty; this is the first design
document.

---

## 3. Requirements and Constraints

### 3.1 From `ARCHITECTURE.md`

- **R1 — Canonical identity.** Every logical tuple has a canonical representation in a
  key-value store addressed by RID; access paths do not replace it (`ARCHITECTURE.md` §4, §5, §27).
- **R2 — Partial materialization is first-class.** Access-path scans produce "RID +
  available tuple values"; the planner tracks which attributes are available and may insert
  a canonical lookup (`ARCHITECTURE.md` §8, §27).
- **R3 — Payload columns are a physical decision.** They do not determine which operations
  are legal; missing values are obtained through the RID (`ARCHITECTURE.md` §7).
- **R4 — Common operator representation.** Storage-specific behavior stays as low in the
  operator tree as practical; a join must not care that one child is a B-tree and the other
  columnar (`ARCHITECTURE.md` §15).
- **R5 — Explicit constraints fail explicitly.** An unsatisfiable physical constraint is a
  diagnostic, never a silent substitution (`ARCHITECTURE.md` §12, §13).
- **R6 — Execution granularity is open.** Row-at-a-time vs. vectorized is an unresolved
  question; the representation must not make vectorization impossible (`ARCHITECTURE.md` §16, §28).
- **R7 — Propagation operates on sets of changes** within a transaction (`ARCHITECTURE.md` §19, §21, §23).
- **R8 — C# types describe logical shape, not physical storage** (`ARCHITECTURE.md` §3, §25); the engine must
  not rely on language-level guarantees for architectural correctness (`ARCHITECTURE.md` §24).

### 3.2 From `AGENTS.md`

- **C1 — Scope control.** Abstractions only where a current architectural boundary requires
  them; no speculative frameworks.
- **C2 — Terminology.** Relation, Source, Change, Transaction, Accessor, StorageProvider,
  Realize, Projection. Avoid Repository/Entity/DAO/Manager.
- **C3 — Async streaming.** `IAsyncEnumerable<T>` for asynchronous sequences; cancellation
  everywhere; no `Task<List<T>>` as a primary result.
- **C4 — Fail explicitly.** No silent fallback from seek to scan, no swallowed
  inconsistencies.
- **C5 — Generics scoped to the value type; database-wide abstractions non-generic.**
- **C6 — Immutable value types where practical; records/readonly structs for value-like
  data; expression bodies preferred.**
- **C7 — Hot-path awareness.** No per-tuple reflection, no hidden buffering, no repeated
  expression compilation.
- **C8 — Public API changes are conservative and motivated.** The project is experimental,
  so churn is acceptable when justified — each change must be.

### 3.3 Derived requirements

- **D1 — Missing ≠ null.** A tuple scanned from a path that lacks attribute `A` must be
  distinguishable from a tuple in which `A` was read and is `null`. Consequence: a plain CLR
  `T` **cannot** represent a partially materialized tuple, because for a nullable property
  (`string? FirstName`) there is no third state — and conflating the two is a dependable way to manufacture a bug that only surfaces in production, where a legitimately-null `FirstName` and an unmaterialized one are indistinguishable. A materialization state is mandatory.
- **D2 — One currency between operators.** For operators to compose across heterogeneous
  paths (R4), scans of every path type must emit the same value type.
- **D3 — Attribute identity must be stable and shared.** The planner, operators, canonical
  store, and access paths must all refer to the *same* attribute identities. An identity
  model is mandatory.
- **D4 — No per-tuple reflection or per-tuple schema discovery** (C7).
- **D5 — The typed authoring API is an established commitment** (R8, `ARCHITECTURE.md` §9): `IQueryable<T>`
  and expression trees remain the authoring surface. This design must not remove them.

---

## 4. Alternatives Considered

### A. Keep the all-generic design (status quo, extended)

Represent a partially materialized tuple as a `T` instance whose unmaterialized properties
hold `default`/`null`, plus a side-band `HashSet<string>` (or similar) of "loaded"
properties. Physical operators remain generic over `T`.

**Trade-offs:**

- *Complexity:* Low initially — no new types.
- *Correctness:* Fails D1 unless the side-band mask is added; once the mask exists, the
  mask + values **is** a runtime tuple representation, and `T` is just a container glued to
  it. The mask would be keyed by property *name strings* (the only identity available on a
  bare `T`), which is slow, error-prone, and not shareable across schemas.
- *Extensibility:* Fails D2/D3 for cross-relation operators: a join of `Customer` and
  `Order` has no single `T` to name; `ISource<T>`, `ICanonicalTupleStore<T>`, and every
  operator stay coupled to a CLR type, violating R8/C5.
- *Failure behavior:* Unmaterialized reads silently return `default` — indistinguishable
  from a real `null`/`0` — violating C4.

**Verdict:** Rejected as the runtime representation. It is, in effect, the recommended
design with the schema model deleted and string keys substituted. The generic authoring
API it contains is kept (see §5.11).

### B. Generics confined to the boundary; non-generic, schema-driven runtime (recommended)

The CLR type `T` remains the authoring, registration, and value-construction currency.
Below the translation boundary, everything — schemas, tuples, masks, canonical store,
access-path descriptions, operator I/O — is non-generic and operates on attribute
ordinals, a `TupleValue`, and RIDs.

**Trade-offs:**

- *Complexity:* Moderate. New types: schema model, runtime tuple, mask, codec, registry,
  non-generic canonical store. Each maps to a real boundary (§5.1).
- *Correctness:* Satisfies D1–D5 by construction; invalid states (reading a missing
  attribute, materializing a tuple with no RID, mixing sources under one RID) are either
  unrepresentable or throw.
- *Performance:* One array allocation + value-type boxing per materialized tuple
  (§8). Comparable to or better than LINQ-to-objects; no per-tuple reflection.
- *Extensibility:* Schema/ordinal substrate is exactly what a future batch/vectorized
  executor reuses (§9); a columnar or vector path plugs in by emitting the same
  `TupleValue` currency.
- *Compatibility:* One breaking public change (`ICanonicalTupleStore<T>` → non-generic)
  plus two concrete in-memory types; all authoring APIs unchanged (§6).

**Verdict:** Recommended.

### C. Columnar/batch-first runtime

Make `TupleBatch` (per-column arrays, validity bitmaps) the operator currency from the
start.

**Trade-offs:**

- *Performance:* Best long-term ceiling for columnar workloads.
- *Complexity:* Highest now. Every operator (filter, join, aggregate, mutation,
  propagation) must be designed batch-first before a single row-at-a-time operator exists;
  the Volcano pull model of `ARCHITECTURE.md` §14 and the `IAsyncEnumerable` streaming
preference (C3) are
  both row-oriented.
- *Correctness:* Batch-level validity masks complicate the missing/null distinction and
  transactional propagation (which is set-oriented but currently per-change).
- *Timing:* `ARCHITECTURE.md` §16 explicitly leaves granularity open and says the
  architecture should "avoid unnecessarily coupling relational operators to a
  row-at-a-time representation" — it does not commit to batches.

**Verdict:** Rejected *for now*. Alternative B's schema/ordinal/codec substrate is the
shared foundation a batch model would sit on, so choosing B does not foreclose C (§9).

### D. Fully dynamic engine (no generics anywhere)

Replace `IQueryable<T>`/expression trees with a dynamic query language; CLR types become
optional annotations.

**Trade-offs:**

- *Extensibility:* Maximum language independence.
- *Compatibility:* Directly contradicts established commitments — `ARCHITECTURE.md` §3
  ("relation schemas are represented using CLR types"), `ARCHITECTURE.md` §9 ("The C# API
  is built around `IQueryable<T>` and expression trees"), `ARCHITECTURE.md` §25 (C# provides
  expression trees, generics, strong typing). Also loses compile-time checking of
  `Column(x => x.Id)`-style path definitions (`ARCHITECTURE.md` §13), which the
  architecture values.

**Verdict:** Rejected. Removing generics is not a consequence of this design; the typed
authoring layer is retained intact.

---

## 5. Recommended Design

### 5.1 The architectural boundary

```text
┌──────────────────────────────────────────────────────────────────┐
│  TYPED AUTHORING LAYER — generic over T                          │
│                                                                  │
│  IQueryable<T>, expression trees, IRelationContext,              │
│  IRelationChangeContext, ISource<T>, IDerived<T>,               │
│  IPropagateChanges<T>, Change<T>, BTreePath<T>, HeapPath<T>     │
└────────────────────────────────┬─────────────────────────────────┘
                                 │
                    TRANSLATION BOUNDARY (one-way)
      expression tree → logical plan:
        • MemberExpression  → attribute ordinal   (via RelationSchema)
        • T value           → TupleValue          (via TupleCodec)
        • TPath type        → AccessPathDescription
                                 │
                                 ▼
┌──────────────────────────────────────────────────────────────────┐
│  RUNTIME — non-generic, schema-driven                            │
│                                                                  │
│  Logical plan (operators + RelationSchema + ordinals)            │
│      │                                                           │
│      ▼  planner: propagates OutputSignature, inserts            │
│         RID materialization, enforces constraints               │
│                                                                  │
│  Physical operators:  IAsyncEnumerable<TupleValue>              │
│      │                                                           │
│      ▼                                                           │
│  Storage:  ICanonicalTupleStore  (RID → full TupleValue)        │
│            access paths      (RID + key/payload → TupleValue)    │
└──────────────────────────────────────────────────────────────────┘
```

Rules of the boundary:

1. **Above it:** `T`, expression trees, LINQ. The C# type system provides compile-time
   checking (`ARCHITECTURE.md` §13). Nothing here knows about ordinals, masks, or RIDs.
2. **The crossing is one-way and total:** a query enters as an expression tree and leaves
   as `IAsyncEnumerable<TResult>`; the reconstruction of `TResult` from the final
   `TupleValue` happens at the output edge of the same boundary.
3. **Below it:** no CLR type parameter. The unit of value is `TupleValue`; the unit of
   shape is `RelationSchema`; the unit of logical attribute identity is the attribute
   **name**; the unit of runtime indexing is an **ordinal** (a schema-local slot); the
   unit of tuple identity is `Rid`.
4. **The registry is the only place that knows `T` ↔ schema ↔ codec ↔ store ↔ paths** for a
   relation. It is a keyed map (DI registration), not a property bag (C2, `AGENTS.md`
   "Schema and Discovery").

### 5.2 Schema model — `PolyStore.Schema`

New namespace `PolyStore.Schema` (folder `PolyStore/Schema/`).

#### Identity, slot, and physical layout

Three distinct concepts are separated throughout this design:

1. **Logical attribute identity** — the identity of an attribute within the logical
   relation schema. This is the attribute's **name** (the CLR property name). It is
   stable under addition or removal of *other* attributes. It is what a persistence
   format or a future schema-evolution mechanism keys on.
2. **Runtime slot (ordinal)** — a dense, schema-local position (0..n-1) assigned by the
   compiled `RelationSchema`. Used by `TupleValue` indexing, `MaterializationMask`,
   compiled expressions, and `OutputSignature`. Valid **only relative to a particular
   compiled `RelationSchema` instance**. It is not a logical identity and not a
   persistence identifier. If the schema is recompiled (e.g., a property is added), the
   ordinals may shift; that is expected and correct because ordinals are runtime slots,
   not identities. Sorted-property-name assignment is a *runtime layout rule* (it gives a
   deterministic slot order for this schema) — it does not confer persistence identity.
3. **Physical encoding/layout** — owned by the storage format. A slotted binary
   representation may require a stable ordered physical schema, while a self-describing
   representation such as JSON may not depend on ordering at all. The runtime
   representation (`TupleValue`, ordinals, masks) does not dictate the physical layout.
   A persistence format keys on the logical identity (name), not the runtime ordinal.

**Invariants:**

- **I-IDENT (Identity is the name, not the ordinal).** The logical attribute identity is
  the attribute's name. It is stable under addition/removal of other attributes. It is
  what crosses persistence and schema-evolution boundaries.
- **I-SLOT (Ordinal is a runtime slot).** Runtime ordinals are dense, schema-local
  positions (0..n-1) valid only relative to a particular compiled `RelationSchema`
  instance. They are not stable across schema recompilation and are not persistence
  identifiers.
- **I-PHYSICAL (Physical layout is the storage format's concern).** The physical encoding
  of a tuple is owned by the storage format. The runtime representation (`TupleValue`,
  ordinals, masks) does not dictate the physical layout. A persistence format may be
  slotted (requiring a stable ordered physical schema) or self-describing
  (order-independent); either way it keys on the logical identity, not the runtime
  ordinal.

**Schema-evolution example.** A relation with properties `{Id, Name, Age}` has sorted
ordinals `{Age:0, Id:1, Name:2}`. If a property `Email` is added, the new compiled schema
has sorted ordinals `{Age:0, Email:1, Id:2, Name:3}`. The *logical identity* (name) of
the three existing attributes is unchanged; only the *runtime slots* for `Id` and `Name`
shifted. A persistence format that keyed on names would be unaffected; one that keyed on
ordinals would break — persisting ordinals would turn an innocent property addition into an exciting storage-recovery exercise. That is exactly why ordinals must not be persistence
identifiers.

```csharp
namespace PolyStore.Schema;

/// <summary>
/// A single attribute of a relation schema.
/// </summary>
/// <param name="Ordinal">Dense, schema-local runtime slot (0..n-1) used for TupleValue indexing, masks, and compiled expressions. Valid only relative to this compiled RelationSchema. Not a logical identity and not a persistence identifier.</param>
/// <param name="Name">The logical attribute identity (CLR property name). Stable under addition/removal of other attributes. This is what a persistence format or schema-evolution mechanism would key on. Distinct from the runtime ordinal.</param>
/// <param name="ClrType">CLR type of the attribute value.</param>
/// <param name="IsNullable">Whether the attribute may hold a materialized null.</param>
public readonly record struct AttributeInfo(int Ordinal, string Name, Type ClrType, bool IsNullable);

/// <summary>
/// The immutable shape of a relation's tuples: an ordered set of attributes.
/// </summary>
/// <remarks>
/// Schemas are shared, immutable, and compared by reference. A schema may be derived from
/// a CLR type (via the registry) or constructed synthetically by the planner for
/// projections, joins, and aggregates.
/// </remarks>
public sealed class RelationSchema
{
    public RelationSchema(string name, IReadOnlyList<AttributeInfo> attributes);

    /// <summary>
    /// The relation name. For a CLR-derived schema: the value of
    /// <c>[Relation(Name = "...")]</c> when present and non-empty; otherwise the CLR type
    /// name (<c>typeof(T).Name</c>). For a synthetic schema: a planner-assigned name
    /// (e.g., an alias). See the naming invariant below.
    /// </summary>
    public string Name { get; }

    /// <summary>Attributes in ordinal order.</summary>
    public IReadOnlyList<AttributeInfo> Attributes { get; }

    public int AttributeCount { get; }

    public AttributeInfo this[int ordinal] { get; }

    /// <summary>Resolves a name to an ordinal. Throws <see cref="KeyNotFoundException"/> if absent.</summary>
    public int IndexOf(string name);

    public bool TryIndexOf(string name, out int ordinal);
}
```

Design decisions:

- **Name is the logical attribute identity; the ordinal is the runtime slot.** Ordinals
  give O(1) attribute access, cheap bitsets, and a stable slot reference that is
  independent of string comparison *within a compiled schema*. Names are the stable logical
  identity that
  crosses persistence and schema-evolution boundaries. Ordinals are assigned once at
  schema creation and never change *for the lifetime of that schema instance*; they are
  not stable across schema recompilation and are not persistence identifiers (I-SLOT,
  I-IDENT).
- **Deterministic derivation.** When a schema is derived from a CLR type, attribute order
  must be deterministic. `Type.GetProperties()` order is not guaranteed by the CLR, so the
  registry assigns ordinals by **sorted property name** (a fixed, documented rule). This
  is a *runtime layout rule* that gives a deterministic slot order for a given compiled
  schema; it does not confer persistence identity. A persistence format would key on the
  logical identity (name), not the ordinal (I-PHYSICAL). (See Assumptions, A5.)
- **Inheritance is part of the shape.** `HelloWorld/Models.cs` defines
  `ArchivedCustomer : Customer`. Schema derivation must include inherited public instance
  properties. The derived type's schema is a *different* schema (different name, possibly
  more attributes), not an identity with the base.
- **Synthetic schemas.** Projections, joins, and aggregates produce output shapes with no
  CLR type. `RelationSchema` is therefore constructible directly, not only from a CLR type.
  For joins, attribute names must be made unique (e.g., prefixed with the relation alias);
  each attribute also receives a unique ordinal slot.
- **Class, not struct.** A schema is a shared, reference-compared, immutable artifact; a
  sealed class avoids copying a growing attribute list by value and makes "same schema"
  identity a reference check.
- **Naming invariant (I-NAME).** The relation name is derived as follows: if
  `[Relation(Name = "...")]` is present and non-empty, that name is used; otherwise the
  CLR type name (`typeof(T).Name`) is used. This invariant is enforced at registration
  (§5.7): the registry derives the name, validates it (duplicate names across registered
  relations are a registration failure, R5), and uses it in all diagnostics and plan
  inspection. Examples in this document use the explicit name from `[Relation]` (e.g.,
  "customer" for `Customer`, per `HelloWorld/Models.cs`).

### 5.3 Runtime tuple — `PolyStore.Execution`

```csharp
namespace PolyStore.Execution;

/// <summary>
/// The set of attributes currently materialized on a tuple.
/// </summary>
/// <remarks>
/// A bitset over schema ordinals. This is what distinguishes a *missing* attribute
/// (bit clear) from a *materialized null* (bit set, value null).
/// </remarks>
public readonly struct MaterializationMask : IEquatable<MaterializationMask>
{
    public bool IsEmpty { get; }
    public bool IsSet(int ordinal);
    public MaterializationMask With(int ordinal);
    public MaterializationMask Union(MaterializationMask other);
    public bool CoversAll(RelationSchema schema);
}

/// <summary>
/// A single provenance entry: the canonical tuple from a source relation.
/// </summary>
public readonly struct ProvenanceEntry
{
    public ProvenanceEntry(RelationSchema source, Rid rid, int instance);

    /// <summary>The source relation's schema (identifies the relation and its canonical store).</summary>
    public RelationSchema Source { get; }

    /// <summary>The canonical tuple's RID within the source relation.</summary>
    public Rid Rid { get; }

    /// <summary>
    /// The 0-based instance discriminator, assigned by the combining operator that produced
    /// this entry. **Instance IDs must be unique per source** across all entries of a
    /// single <see cref="TupleProvenance"/>: for a given source relation, no two entries
    /// may share an instance (enforced by <see cref="TupleProvenance.Create"/> and
    /// <see cref="TupleProvenance.Union"/>). The common 2-way case (two different sources,
    /// one entry each) assigns 0 to the first input's entries and 1 to the second's. For
    /// nested joins or multi-way self-joins, the combining operator (or the planner)
    /// assigns instance IDs that remain unique per source. For the common single-source
    /// case (scan), instance is always 0. Required to disambiguate self-joins where the
    /// same source relation appears in multiple entries.
    /// </summary>
    public int Instance { get; }
}

/// <summary>
/// The provenance of a tuple: the canonical tuples from which its materialized
/// attributes originate. Engine-internal metadata; not a user-visible relation attribute
/// and not part of <see cref="RelationSchema"/>.
/// </summary>
/// <remarks>
/// Zero entries: no canonical source (aggregate, values scan).
/// One entry: single source (scan).
/// Multiple entries: combined sources (join, same-relation set ops).
/// The planner decides when to preserve or discard provenance (§5.8).
///
/// Backing: the first entry is stored inline in the struct (Source0, Rid0, Instance0);
/// additional entries are stored in a heap-allocated overflow array. The common
/// single-source case (Count == 1) therefore carries **zero heap allocations**; a
/// multi-source provenance (Count ≥ 2, e.g. a join) carries exactly one allocation
/// (the overflow array). See the design decisions below.
/// </remarks>
public readonly struct TupleProvenance
{
    public int Count { get; }
    public bool IsEmpty { get; }
    public bool IsSingle { get; }

    /// <summary>
    /// Creates a provenance with a single entry (the common scan case). No array
    /// allocation — the entry is stored inline in the struct.
    /// </summary>
    public static TupleProvenance Create(ProvenanceEntry entry);

    /// <summary>
    /// Creates a provenance with two entries (the common 2-way join case). No array
    /// allocation for the first entry (inline); the second entry is stored in the
    /// overflow array (1 allocation). Throws <see cref="ArgumentException"/> if the two
    /// entries share a (Source, Instance) pair.
    /// </summary>
    public static TupleProvenance Create(ProvenanceEntry first, ProvenanceEntry second);

    /// <summary>
    /// Creates a provenance from the given entries (the general multi-way case). The
    /// entries must have unique (Source, Instance) pairs; throws
    /// <see cref="ArgumentException"/> otherwise. Note: C# <c>params</c> expansion
    /// allocates an array at the call site — use the single- or two-entry overloads on
    /// hot paths to avoid that allocation. A combining operator (e.g., a join) uses
    /// these overloads to build the output provenance with the instance IDs it has
    /// assigned to its inputs' entries (§5.8).
    /// </summary>
    public static TupleProvenance Create(params ProvenanceEntry[] entries);

    /// <summary>True if the provenance includes a canonical tuple from the given source relation (any instance).</summary>
    public bool HasSource(RelationSchema source);

    /// <summary>
    /// Gets the RID for the given source relation.
    /// Throws <see cref="KeyNotFoundException"/> if the source is not in the provenance.
    /// Throws <see cref="AmbiguousProvenanceException"/> if the source appears in
    /// multiple entries (self-join) — use the two-argument overload to disambiguate.
    /// This is the authoritative statement of the duplicate-source behavior; §5.8 and
    /// §12 reference it.
    /// </summary>
    public Rid GetRid(RelationSchema source);

    /// <summary>
    /// Gets the RID for the given source relation and instance (see
    /// <see cref="ProvenanceEntry.Instance"/>). Throws
    /// <see cref="KeyNotFoundException"/> if no entry matches both the source and the
    /// instance.
    /// </summary>
    public Rid GetRid(RelationSchema source, int instance);

    /// <summary>
    /// Combines two provenances (union of entries). Instance IDs are assigned by the
    /// combining operator that calls this (typically via <see cref="Create"/>, where a
    /// join assigns 0 to the first input's entries and 1 to the second's in the common
    /// 2-way case); this method does not renumber them. Throws
    /// <see cref="ArgumentException"/> if the union would contain a duplicate
    /// (Source, Instance) pair — instance IDs must be unique per source.
    /// </summary>
    public TupleProvenance Union(TupleProvenance other);
}

/// <summary>
/// The runtime representation of a relation tuple, possibly partially materialized.
/// </summary>
/// <remarks>
/// Every physical operator produces and consumes TupleValue. The
/// <see cref="Provenance"/> identifies, for each source relation, the canonical tuple
/// from which that source's materialized attributes originate (the provenance invariant,
/// §5.8). Provenance is engine-internal: it is not a user-visible attribute and is not
/// part of the <see cref="RelationSchema"/>.
/// </remarks>
public readonly struct TupleValue
{
    public RelationSchema Schema { get; }
    public TupleProvenance Provenance { get; }
    public MaterializationMask Mask { get; }

    /// <summary>True if the attribute is materialized (present), regardless of whether its value is null.</summary>
    public bool IsMaterialized(int ordinal);

    /// <summary>
    /// Reads a materialized attribute.
    /// Throws <see cref="TupleAttributeNotMaterializedException"/> if the attribute is not
    /// materialized — reading a missing attribute is a planning/execution error, not a null.
    /// </summary>
    public object? GetValue(int ordinal);

    public T GetValue<T>(int ordinal);

    /// <summary>
    /// Returns a copy with the given attribute materialized (used by the materialization
    /// operator for selective merge; see §5.8).
    /// </summary>
    public TupleValue WithMaterialized(int ordinal, object? value);

    /// <summary>
    /// Returns a copy with multiple attributes materialized in a single O(width) array
    /// pass. The materialize operator uses this to batch all *m* attributes of a source
    /// into one copy, avoiding the O(m × width) cost of calling
    /// <see cref="WithMaterialized"/> *m* times (§5.8 rule 3, batched-write requirement).
    /// </summary>
    public TupleValue WithMaterializedMany(int[] ordinals, object?[] values);

    /// <summary>Returns a copy carrying the given provenance (used when provenance is assigned post-scan or combined by a join).</summary>
    public TupleValue WithProvenance(TupleProvenance provenance);
}

/// <summary>Thrown when an operator reads an attribute that is not materialized.</summary>
public sealed class TupleAttributeNotMaterializedException : InvalidOperationException
{
    // ctor(schema, attribute name, ordinal)
}

/// <summary>
/// Thrown by <see cref="TupleProvenance.GetRid(RelationSchema)"/> when the source
/// relation appears in multiple provenance entries (a self-join) and the caller did not
/// specify an instance. Use <see cref="TupleProvenance.GetRid(RelationSchema, int)"/>
/// to disambiguate.
/// </summary>
public sealed class AmbiguousProvenanceException : InvalidOperationException
{
    // ctor(source schema, entry count)
}
```

Design decisions:

- **`readonly struct`.** A `TupleValue` is a handle: schema reference + provenance + mask
  + values-array reference. Struct semantics give value copying for free at operator
  boundaries with **no additional allocation** (the per-tuple heap allocations are the
  values array **and** the boxing of each materialized value-type attribute; see §8).
  This matches C6. The alternative (a class) would add one heap allocation and one
  indirection per tuple for no expressiveness gain.
- **Provenance is engine-internal, not a logical attribute.** `TupleProvenance` is
  metadata on the `TupleValue`, not part of the `RelationSchema`. It is not visible to
  the authoring layer, not part of the mask, and not reconstructed into a CLR value at
  the output edge. The planner decides when to preserve or discard it (§5.8). This keeps
  the logical/physical boundary clean: the schema describes logical attributes; the
  provenance describes physical provenance.
- **Provenance cost is small and bounded; backing is specified.** Each entry is a
  `(RelationSchema, Rid, Instance)` triple (~32 bytes: schema reference 8 + RID 16 +
  instance 8). `TupleProvenance` stores the **first entry inline** in the struct
  (`Source0`, `Rid0`, `Instance0`) plus a `Count` and a nullable `ProvenanceEntry[]?
  Overflow` (null when `Count ≤ 1`). Consequences:
  - **Single-source (Count == 1): zero heap allocations** — the entry lives entirely in
    the struct, which itself is copied by value at operator boundaries.
  - **Multi-source (Count ≥ 2, e.g. a join): exactly one heap allocation** — the overflow
    array, sized to `Count - 1`.
  A single-source tuple carries one entry; a join of two relations carries two. The cost
  is only incurred when the planner needs it (late materialization); the planner can
  discard provenance after the materialize operator has consumed it (§8).
- **Construction and the instance-uniqueness invariant.** `TupleProvenance` is a
  `readonly struct` with no public instance constructor; the construction paths are the
  empty default, the static `Create` factory (single-entry, two-entry, and `params`
  overloads), and `Union`. All `Create` overloads and `Union` validate that
  **(Source, Instance) pairs are unique** within the result and throw `ArgumentException`
  otherwise, so the invariant that `GetRid(source, instance)` is well-defined holds by
  construction: a provenance in which the same source appears under two *different*
  instances is representable (self-join), but two entries claiming the *same* (source,
  instance) are not. **Allocation avoidance on hot paths:** C# `params` expansion
  allocates an array at the call site, so the single-entry and two-entry overloads exist
  for the common scan and 2-way join cases: `Create(entry)` stores the entry inline with
  **zero heap allocations**, and `Create(first, second)` allocates only the overflow
  array (1 allocation) — neither pays the `params` array allocation. The `params`
  overload remains for the general multi-way case, where one overflow allocation is
  already accepted. A combining operator (e.g., a join) assigns instance IDs to its
  inputs' entries — 0 for the first input, 1 for the second in the common 2-way case;
  unique-per-source IDs for nested joins and multi-way self-joins — and then builds the
  result via the appropriate `Create` overload (or per input followed by `Union`) (§5.8).
- **Values are `object?[]`, indexed by ordinal, sized to the full schema.** Unmaterialized
  slots hold `null`. Full-size indexing is O(1) with no ordinal→slot map. The cost is
  unused slots on sparse tuples (a scan that materializes 2 of 5 attributes still allocates
  5 slots) — accepted for simplicity; see §8 and Open Questions. **Exception: a tuple with
  an empty mask (RID-only, e.g. a heap scan) carries a `null` values array** (or a shared
  empty array) — **zero allocation**. This is safe because `GetValue` throws for every
  ordinal when no mask bit is set, so the null array is never read; the D1 invariant
  (missing ≠ null) is unaffected. This removes the full-width null array from the most
  common full-scan path (see §8).
- **Mask backing.** A single `long` for schemas with ≤64 attributes (8 bytes, O(1) bit
  test, trivially copyable, **zero per-tuple allocation**); a `ulong[]` otherwise.
  **Invariant:** the mask is uniform across a single operator's output — a scan emits a
  fixed mask, a pass-through preserves it, a projection emits a fixed mask over its output
  schema, and a materialize emits a **fixed mask (input mask ∪ the materialized attributes
  from the source)** — so for the `ulong[]` case the backing array is allocated **once
  per operator and shared by reference** across every tuple that operator emits, not per
  tuple. (The emit-full degenerate case of the materialize — where the row schema is the
  relation schema and the operator materializes from that single relation — is the only
  case that yields a full mask; the general case is the fixed selective mask described
  above.)
  **Copy-on-write guarantee:** the shared `ulong[]` backing is **never mutated after
  publication**. Any `With`/`Union` operation that would modify the mask allocates a new
  array and returns a new `MaterializationMask`; the previously published array is left
  untouched. This is what makes the value-semantic claim (equality and `IsSet` are
  value-based) sound despite the reference-shared backing: a shared array is read-only
  after it has been published to any tuple, so two masks sharing a backing array are
  observably equal exactly when their bits agree.
- **The mask is mandatory, not optional.** It is the only thing that separates "attribute
  not loaded" from "attribute is null" (D1). Any design that drops the mask silently
  corrupts nullable attributes.
- **Fail explicitly on missing reads (C4).** `GetValue` throws rather than returning
  `default` — returning `default` is a small lie that every downstream operator would inherit. A plan that reads an unmaterialized attribute is a planner bug; surfacing it
  as an exception (with schema + attribute name in the message) is the intended diagnostic
  path.
- **Immutability.** Operators produce new `TupleValue`s; they never mutate a child's tuple.
  `WithMaterialized` copies the values array (or allocates a new array of the schema's
  width if the input's array is `null` — the RID-only case, §5.3). This preserves the
  Volcano producer/consumer contract and makes tuples safe to share (e.g., change sets
  captured mid-plan).

### 5.4 Value codec — `PolyStore.Schema.TupleCodec`

The boundary must convert between CLR values and `TupleValue` **without per-tuple
reflection** (C7). A per-schema codec compiles the accessors once:

```csharp
namespace PolyStore.Schema;

/// <summary>
/// Cached conversion between a relation's CLR type and the runtime tuple representation.
/// One instance per (relation CLR type, schema); created once at registration.
/// </summary>
public sealed class TupleCodec
{
    public RelationSchema Schema { get; }

    /// <summary>Reads attribute <paramref name="ordinal"/> from a CLR value (getter delegate, no reflection).</summary>
    public object? GetAttribute(object clrValue, int ordinal);

    /// <summary>Writes a value into attribute <paramref name="ordinal"/> of a CLR target (setter delegate).</summary>
    public void SetAttribute(object clrTarget, int ordinal, object? value);

    /// <summary>Converts a complete CLR value into a fully materialized TupleValue (no provenance).</summary>
    public TupleValue ToTuple(object clrValue);

    /// <summary>
    /// Reconstructs a CLR value of type <typeparamref name="T"/> from a TupleValue.
    /// All attributes of <paramref name="tuple"/> used by the target must be materialized;
    /// throws <see cref="TupleAttributeNotMaterializedException"/> otherwise.
    /// Throws <see cref="ArgumentException"/> if <c>tuple.Schema != Schema</c> — the tuple
    /// must be over the same schema the codec was built for (C4, fail explicitly).
    /// </summary>
    public T FromTuple<T>(TupleValue tuple);
}
```

Design decisions:

- **Justified by a real boundary (C1):** the CLR↔runtime conversion is required at exactly
  three places — the write path (`ISource<T>` mutations, access-path key/payload extraction
  at insert time), the output boundary (`IQueryable<T>` materialization), and change-set
  reconstruction for propagation authoring. All three need the same per-attribute accessor
  table, compiled once.
- **Delegates, not reflection, per tuple.** Getters/setters are compiled
  (`Expression.Lambda(...).Compile()` or equivalent) at registration and cached. Per-tuple
  cost is a delegate call, not a `PropertyInfo.GetValue`.
- **`FromTuple<T>` serves the identity case.** A `TupleCodec` is constructed for one
  specific relation type, so `FromTuple<T>` reconstructs `T` for the relation type the
  codec was built for (`T` = that relation type). It has no knowledge of a projection
  target type's properties and carries no ordinal→property mapping for a different target;
  as specified, the method only supports the identity case. The **projection case**
  (reconstructing an anonymous or other target type from a projected `TupleValue`)
  requires a separate API — a mapping parameter, or a per-plan compiled constructor
  delegate — that is **not specified here**; it is deferred to the translation design
  (see Open Questions, §12 item 9). The requirement it must meet is that it reuses this
  accessor mechanism and never reflects per tuple.
- **Assumes class-based relation types.** `ToTuple(object)` / `GetAttribute(object, …)`
  take the CLR value as `object`. For the current record (class) models this is fine; a
  record-struct relation type would box the whole struct on every call. If struct relation
  types are ever supported, add a generic `ToTuple<T>(T)` overload to avoid boxing the CLR
  value (see Assumptions, A10).

### 5.5 Canonical store — `PolyStore.Storage.ICanonicalTupleStore` (non-generic)

```csharp
namespace PolyStore.Storage;

/// <summary>
/// The canonical tuple store for a relation: the authoritative RID → complete-tuple map.
/// </summary>
/// <remarks>
/// Non-generic by design: the store operates on the runtime tuple representation and is
/// bound to a single <see cref="RelationSchema"/>. It is a relation-scoped storage
/// accessor, not a CLR-type container.
/// </remarks>
public interface ICanonicalTupleStore
{
    /// <summary>The schema this store holds tuples of.</summary>
    RelationSchema Schema { get; }

    int Count { get; }

    /// <summary>
    /// Inserts a fully materialized tuple and returns its RID.
    /// Throws <see cref="ArgumentException"/> if <c>value.Schema != Schema</c> or the
    /// value is not fully materialized — canonical tuples are always complete.
    /// </summary>
    Rid Insert(TupleValue value);

    /// <summary>
    /// Retrieves the complete canonical tuple for a RID. The returned tuple is fully
    /// materialized and carries the relation's provenance (this store's schema + the RID).
    /// </summary>
    bool TryGet(Rid rid, [MaybeNullWhen(false)] out TupleValue value);

    void Delete(Rid rid);
}
```

Design decisions:

- **Non-generic (C5, R8).** The current `ICanonicalTupleStore<T>` couples the authoritative
  storage to a CLR type. The store's contract is "RID → complete tuple of *this relation's
  schema*"; the schema travels with the value and is validated at the boundary. This is the
  single breaking public change in this design and is the one that most directly serves
  R1/R8.
- **Schema-bound, validated.** Binding the store to a schema and rejecting foreign or
  partial tuples makes "a wrong-shaped tuple in the canonical store" unrepresentable and
  fails explicitly (C4).
- **In-memory representation vs. physical encoding.** The store's *in-memory*
  representation uses `TupleValue` (runtime ordinals). The store's *serialization or
  persistence* (if any) is a separate concern owned by the storage format (I-PHYSICAL).
  The store's binding to a `RelationSchema` is for runtime validation, not a
  physical-layout commitment.
- **`TryGet` returns the full tuple.** The canonical store is a KV store (R1); partial
  reads from it are not part of its contract. Selective materialization is an operator
  concern (§5.8), not a store concern.
- **In-memory implementation.** `InMemoryCanonicalTupleStore<T>` becomes
  `InMemoryCanonicalTupleStore` over `Dictionary<Rid, TupleValue>`. Existing tests migrate
  with mechanical changes plus new tests for schema validation and full/partial
  distinction.

### 5.6 Access path description — `PolyStore.Storage.AccessPathDescription`

```csharp
namespace PolyStore.Storage;

/// <summary>
/// The non-generic description of an access path's physical contribution:
/// which attributes it provides alongside the RID.
/// </summary>
public sealed record AccessPathDescription
{
    public AccessPathDescription(
        string name,
        RelationSchema schema,
        IReadOnlyList<int> keyOrdinals,
        IReadOnlyList<int> payloadOrdinals);

    /// <summary>
    /// Path identity: the path type's name or a registered name. Distinguishes paths on the
    /// same relation (e.g. duplicate indexes) so the planner can honor <c>From&lt;T, TPath&gt;()</c>
    /// constraints and name paths in diagnostics.
    /// </summary>
    public string Name { get; }

    public RelationSchema Schema { get; }

    /// <summary>Key attributes, in significance order (first = most significant).</summary>
    public IReadOnlyList<int> KeyOrdinals { get; }

    /// <summary>Payload (included) attributes.</summary>
    public IReadOnlyList<int> PayloadOrdinals { get; }

    /// <summary>Key ∪ payload. The RID is implicit: every access path produces RIDs.</summary>
    public IReadOnlySet<int> ProvidedOrdinals { get; }
}
```

Design decisions:

- **The RID is not in the ordinal set.** It is a first-class, implicit property of every
  access path (§5, R1). The RID (as provenance) is tracked in `OutputSignature` (§5.8),
  not as a synthetic attribute, so user schemas stay clean.
- **A RID-only heap is the degenerate case:** `KeyOrdinals = []`, `PayloadOrdinals = []`,
  `ProvidedOrdinals = []` — the heap provides only the RID. A heap **with included columns**
  is a heap with non-empty `PayloadOrdinals` (the existing `HeapPath<T>.Include` authoring
  API exists precisely to include columns in a heap); the representation supports both.
  This matches §6.1 of `ARCHITECTURE.md` ("A heap may optionally carry additional tuple
  data") and falls out of the same type rather than a special case.
- **Path identity is explicit.** `Name` carries the path's identity (the path type's name
  or a registered name). Two paths on the same relation may have identical
  keys/payload (e.g. a duplicate index); the planner must distinguish them to honor
  `From<T, TPath>()` constraints and to name them in diagnostics. The registry assigns the
  name at registration (§5.7).
- **Authoring stays generic; description is derived.** `BTreePath<T>`/`HeapPath<T>` keep
  their `Column`/`Include` expression API (compile-time checking, `ARCHITECTURE.md` §13).
  The change to the
  path classes is that `Column`/`Include` **record** their expressions (today they are
  no-ops) and expose them read-only:

  ```csharp
  public class BTreePath<T> : IAccessPath<T>
  {
      public IReadOnlyList<Expression<Func<T, object?>>> Columns { get; }
      public IReadOnlyList<Expression<Func<T, object?>>> Includes { get; }
      // Column(expr) / Include(expr) append; Configure() unchanged
  }
  ```

  The registry (or a small describer it owns) resolves each recorded member expression to
  an ordinal against the relation's schema and builds the `AccessPathDescription`.
  Unresolvable members (non-property, missing property) are a **registration-time
  diagnostic** (R5, `ARCHITECTURE.md` §24) — not a silent drop.
- **`IAccessPath<T>` itself is unchanged.** No new methods are added to the authoring
  interface; the description is a derived artifact. This keeps the authoring surface stable.

### 5.7 Relation registry — `PolyStore.Hosting.RelationRegistry`

```csharp
namespace PolyStore.Hosting;

/// <summary>
/// The engine's map from relation CLR types to their runtime artifacts:
/// schema, codec, canonical store, and access-path descriptions.
/// </summary>
/// <remarks>
/// A keyed registration map (DI-friendly), not a property bag. Populated at database
/// construction; read-only during execution.
/// </remarks>
public sealed class RelationRegistry
{
    /// <summary>Derives (or returns the cached) schema for T and registers it.</summary>
    public RelationSchema Register<T>();

    public RelationSchema GetSchema<T>();

    public TupleCodec GetCodec<T>();

    /// <summary>Gets or creates the canonical store bound to T's schema.</summary>
    public ICanonicalTupleStore GetCanonicalStore<T>();

    /// <summary>
    /// Explicitly registers an access path type for a relation type. Resolves the path's
    /// key/payload members to ordinals against T's schema and records the path's name.
    /// Fails at registration if the path references unknown or non-property members.
    /// </summary>
    public void RegisterPath<T, TPath>() where TPath : IAccessPath<T>;

    /// <summary>
    /// Resolves a registered access path type to its description for a relation type.
    /// Throws if the path was not registered for the relation.
    /// </summary>
    public AccessPathDescription Describe(Type relationType, Type pathType);

    /// <summary>All access path descriptions registered for T.</summary>
    public IReadOnlyCollection<AccessPathDescription> GetAccessPaths<T>();
}
```

Design decisions:

- **Single owner of `T` knowledge (C1).** The registry is the only component that reflects
  over `T`, and it does so **once per type at startup**. Every other component receives
  schema/codec/store/description artifacts. This is what makes per-tuple reflection
  impossible by construction.
- **Registration-time validation (R5, `ARCHITECTURE.md` §24):** schema derivation failures, path/member
  mismatches, and duplicate relation names are diagnostics at database construction, not
  at first query.
- **Relation name derivation (I-NAME, §5.2).** At `Register<T>()`, the registry derives
  the relation name: `[Relation(Name = "...")]` when present and non-empty, otherwise
  `typeof(T).Name`. The derived name is stored on the `RelationSchema` and used in all
  diagnostics and plan inspection. If two registered relations derive the same name,
  registration fails (R5) — the name is a logical identifier and must be unique.
- **Interaction with `DatabaseBuilder`.** `DatabaseBuilder.Source<T>(name)` and
  `Relation<T>(name, factory)` (currently stubs) become the registration entry points:
  each calls into the registry. `RelationAttribute` remains the relation-name source.
- **Path types are registered explicitly, not discovered.** `RegisterPath<T, TPath>()` is
  the mechanism by which the registry learns *which* path types belong to a relation —
  called from `DatabaseBuilder.Source<T>()` (or a dedicated `RegisterPath` call) for each
  path the application wants available. Paths are **not** discovered by reflection over
  nested types; if nested-type discovery is later permitted it must be opt-in and
  deterministic. This satisfies `AGENTS.md`'s requirement that inference be "conservative
  and deterministic" and its preference for explicit registration APIs. The registry
  assigns each path a `Name` (§5.6) at registration.

### 5.8 Planner-facing representation: availability and RID materialization

This section defines the *representation* the planner needs (answering `ARCHITECTURE.md` §28's "How are
partially materialized tuples represented?"). It does not design the planner.

```csharp
namespace PolyStore.Execution;

/// <summary>
/// The availability signature of an operator's output: over which schema, which
/// attributes are materialized, and which source relations have a canonical RID available
/// (provenance).
/// </summary>
/// <remarks>
/// A mask is only meaningful relative to a schema — ordinals are schema-relative. The
/// schema therefore travels with the signature; two signatures over different schemas are
/// not comparable (see <see cref="Satisfies"/>). The provenance is engine-internal
/// metadata (§5.3); it is separate from the mask (logical attributes) and is what enables
/// late materialization after combining operators (§5.8).
/// </remarks>
public readonly struct OutputSignature
{
    public OutputSignature(RelationSchema schema, MaterializationMask mask, TupleProvenance provenance);

    /// <summary>The schema over which <see cref="Mask"/> is defined (ordinals are schema-relative).</summary>
    public RelationSchema Schema { get; }

    /// <summary>The logical attributes that are materialized.</summary>
    public MaterializationMask Mask { get; }

    /// <summary>
    /// The source relations that have a canonical RID available for this output.
    /// Empty: no canonical source (aggregate, values scan).
    /// One entry: single source (scan).
    /// Multiple entries: combined sources (join, same-relation set ops).
    ///
    /// **Plan-time note:** at plan time the RIDs are unknown (they are per-tuple values
    /// determined at runtime), so the signature's entries use placeholder RIDs
    /// (<c>default(Rid)</c>); only the (Source, Instance) pairs are meaningful here.
    /// The planner's only provenance query is <see cref="HasSource(RelationSchema)"/>,
    /// which ignores RIDs.
    /// </summary>
    public TupleProvenance Provenance { get; }

    /// <summary>True if the attribute (an ordinal in <see cref="Schema"/>'s space) is materialized.</summary>
    public bool Provides(int ordinal) => Mask.IsSet(ordinal);

    /// <summary>True if a canonical RID is available for the given source relation.</summary>
    public bool HasSource(RelationSchema source) => Provenance.HasSource(source);

    /// <summary>
    /// True if this signature satisfies the requirement. Requires **schema identity**:
    /// <c>requirement.Schema == this.Schema</c> (reference equality, §5.2). A requirement
    /// over a different schema is never satisfied — a projection changes the schema space,
    /// so a requirement above it is expressed in the projection's output schema, not the
    /// source schema. In addition, every required attribute must be provided, and for each
    /// source relation the requirement demands, a canonical RID must be available in the
    /// provenance.
    /// </summary>
    public bool Satisfies(OutputSignature requirement);
}
```

**Signature rules by operator class** (the contract any physical operator must honor).
Each row's mask is over the operator's **output schema** (the "schema" column); a
pass-through's output schema is its input's.

| Operator class | Output schema | Output signature |
|---|---|---|
| Access-path scan | the relation's schema | `mask = path.ProvidedOrdinals`, `provenance = {relation: rid}`. For a RID-only path (`ProvidedOrdinals` empty) the emitted tuple carries a `null` values array — no allocation (§5.3). |
| Values scan (`FromValues<T>`) | the relation's schema | `mask = full schema mask`, `provenance = ∅` (no canonical tuple exists for a value). |
| Materialize | the input (row) schema — schema-preserving | `mask = input mask ∪ the materialized source's attributes (in the row schema)`, `provenance` = input's (see materialize semantics below). |
| Pass-through (filter, sort, limit) | the input schema | identical to input (same schema, same mask, same provenance). |
| Projection (attribute subset, same source) | a synthetic output schema (ordinals 0..n) | `mask = input mask restricted to the selected attributes (mapped to output ordinals)` — a projection cannot provide an attribute its input did not materialize. `provenance` = the provenance entries for the selected attributes' sources (a subset of the input's provenance; see below). |
| Join | a synthetic output schema | `mask = union of both inputs' masks (mapped to output ordinals)` — a join output is materialized to the extent its inputs were. `provenance = union of both inputs' provenance`, with instance IDs assigned by the join: **instance IDs must be unique per source** across all entries of the output; the common 2-way case assigns 0 to the first input's entries and 1 to the second's, while nested joins and multi-way self-joins require the join to assign unique-per-source IDs (see `ProvenanceEntry.Instance` and `TupleProvenance.Create`, §5.3) — each output row retains the canonical identities of both sources; this is what makes late materialization after a join expressible. |
| Same-relation set ops (union/intersect/except) | the input schema | `mask = union of both inputs' masks`, `provenance = union` (each output row retains its own source's RID). |
| Cross-relation set ops | a synthetic output schema | `mask = union of both inputs' masks (mapped to output ordinals)`, `provenance = ∅` (rows come from different relations; no per-source identity is preserved across the operation). |
| Group-by / aggregate | a synthetic output schema | `mask = full over the output schema` (group keys are copied from materialized inputs — a group key attribute whose input mask is clear is a planning error; aggregate values are computed), `provenance = ∅` (no canonical tuple identifies an aggregate). |

**The provenance invariant (the key rule):**

> The provenance of a `TupleValue` identifies, for each source relation, the canonical
> tuple from which that source's materialized attributes originate. A tuple with no
> provenance entry for a given source relation **cannot be further materialized from that
> source**.

This replaces the former single-source RID invariant. The single-source restriction was a
simplicity choice, not a correctness requirement: the correctness requirement (knowing
which canonical tuple to look up) is satisfied by carrying provenance per source. The
provenance model enables **late materialization after combining operators** — a join can
retain the canonical identities of both inputs, and the planner can materialize additional
attributes only after the intermediate result has been substantially reduced by filters.

Consequences:

- **Materialization is defined per source relation.** The materialize operator, given a
  missing attribute X that belongs to source relation R, looks up `provenance[R]` and
  performs `store_R.TryGet(rid)`. A plan that requires materialization of an attribute
  whose source has no provenance entry is a **planning error** (R5) — the planner must
  have preserved the provenance from the scan.
- **Combining operators propagate provenance by union.** A join output's provenance is
  the union of both inputs' provenance, so each output row retains the canonical
  identities of both sources. This is what makes "materialize after a join" expressible.
- **Aggregates and cross-relation set operations emit empty provenance.** A group-by or
  aggregate output is not identified by any single canonical tuple; a cross-relation set
  operation combines rows from different relations. Neither can be further materialized.
- **Self-joins.** A self-join of relation R with itself produces two provenance entries
  with the same source schema, different RIDs, and different instance IDs (in the common
  2-way case, 0 for the first input's entry and 1 for the second's — assigned by the join
  operator; the general invariant is that instance IDs are unique per source, §5.3). The
  single-argument `TupleProvenance.GetRid(source)` throws `AmbiguousProvenanceException`
  in this case (the authoritative statement of this behavior is in §5.3); the planner
  disambiguates by calling the two-argument `GetRid(source, instance)` overload, using the
  output schema's attribute-to-source-instance mapping (each output attribute is mapped to
  a specific source instance) to select the correct instance.

**Materialize operator semantics** (representation-level contract, generalized):

1. **Precondition (plan time):** the input signature's provenance includes the source
   relation for each attribute the operator will materialize. The operator carries, for
   each source relation it materializes from, an **explicit reference to the relation's
   `ICanonicalTupleStore` and the source relation schema, resolved at plan time from the
   scan node** — not derived from the tuple's schema at runtime (a row's schema may be a
   projection, which does not identify the store). It also carries the mapping from the
   row's schema attributes to source-schema attributes (the identity mapping when the row's
   schema is the relation's schema).
2. **Per row, per source relation:** for each source relation R in the operator's
   materialization set, if the row's mask already covers all of R's attributes in the
   row's schema, skip. Otherwise resolve the RID via
   `provenance.GetRid(R, instance)` (the operator's instance for R is fixed at plan
   time; for the common non-self-join case instance is 0) and call
   `store_R.TryGet(rid, out full)`:
   - success → emit a tuple with **the row's schema**, the row's mask updated to include
     R's attributes, and the row's provenance unchanged, copying from `full` only the
     values for R's attributes in the row's schema (mapped to source attributes);
   - failure (stale/unknown RID) → **execution error**, not a silent skip. This explicitly
     reverses today's `InMemoryHeapStoreProvider.EnumerateTuples` behavior, which drops
     unresolvable RIDs (C4 violation).
3. **Schema-preserving merge.** The operator preserves the row's schema; it never changes
   the schema mid-plan (that would break operator I/O contracts). When the row's schema
   is the relation's schema and the operator materializes from that single relation, the
   merge **degenerates to "emit full"**: since `TryGet` returns the complete canonical
   tuple anyway (complete by §5.5), emitting it directly is simplest and avoids a second
   lookup if the downstream needs yet another attribute. When the row's schema is a
   projection (synthetic schema), or when the operator materializes from one of multiple
   sources, the merge is **selective**: copy only the row's attributes from the
   canonical tuple. `TupleValue.WithMaterialized` is the primitive for the selective
   merge (an O(width) array copy, §8); emit-full avoids it.
   **Batched-write requirement:** when the operator materializes *m* attributes from one
   source, it must fill all *m* slots in a **single O(width) array pass** (one copy of
   the values array, writing *m* slots), not by calling `WithMaterialized` *m* times
   (which would cost O(m × width)). The operator implementation should therefore use a
   multi-attribute variant of the primitive (e.g., `WithMaterializedMany(int[]
   ordinals, object?[] values)`) or perform the copy directly. This is a cost-model
   clarification, not a representation change (§8).
4. **Placement rule.** Materialize may be placed **anywhere below a scan that provides
   the required provenance**, including **after combining operators** (join, set ops) —
   this is the key difference from the former single-source model. The planner places it
   **as high as possible** — after as many schema-preserving filters as possible — to
   minimize canonical lookups (each filtered-out row avoids a lookup). Whether to
   materialize eagerly, lazily, or via a different path entirely is costing territory,
   out of scope here (§8 of `ARCHITECTURE.md`); the representation merely makes each
   option expressible. The selective-merge path exists so the operator's contract is
   well-defined for any placement, including after a join.
5. **Provenance discard.** After the materialize operator has consumed the provenance for
   a source relation (the row's mask now covers all of that relation's attributes in the
   row's schema), the planner **may** drop that provenance entry to reduce per-tuple
   memory. The provenance is engine-internal; discarding it does not affect the logical
   result — though discarding it *before* the operator has consumed it would throw away the only map back to the canonical tuple, which is why the rule is scoped to *after* consumption. The planner is not required to discard it, but it is permitted to do so (§8).

### 5.9 Translation boundary (expression → attribute references → plan)

The crossing of the boundary is a compiler concern (`PolyStore.Compiler`), but the
representation it produces is fixed by this design:

1. **`From<T>()`** → registry lookup of `T`'s `RelationSchema` → logical `RelationScan`
   node carrying that schema.
2. **`From<T, TPath>()`** → additionally resolves `TPath` to an
   `AccessPathDescription` → a *constrained* scan node. If the path type is not
   registered for `T`, planning fails with a diagnostic naming the path (R5) — never a
   silent substitution to another path.
3. **`Where(c => c.FirstName == "x")`** → each `MemberExpression` on the query parameter is
   resolved to an ordinal via `schema.IndexOf("FirstName")` → a `Filter` node whose
   predicate references ordinals (compiled to a delegate over `TupleValue` **once per
   plan**, not per tuple — C7).
4. **`Select(c => new { c.Id, c.FirstName })`** → member list resolved to ordinals → a
   `Project` node with a synthetic output schema (ordinals 0..n mapped to source ordinals).
   An identity projection (`Select(c => c)`) or a query with no `Select` preserves the
   relation's schema; `FromTuple<T>` requires the tuple to be over the codec's schema
   (§5.4). At the output edge the projected `TupleValue` is reconstructed into the target
   type: the identity case uses `TupleCodec.FromTuple<T>` (§5.4), while a projection target
   type uses a separate reconstruction API deferred to the translation design (§12 item 9).
5. **`Join`/`GroupBy`/aggregates** → logical nodes with synthetic schemas; per §5.8 their
   physical outputs are fully materialized. A join's provenance is the union of both
   inputs' provenance (enabling late materialization after the join); a group-by or
   aggregate emits empty provenance.
6. **Output edge** → the executor's final operator reconstructs `TResult` per row via the
   codec; the public surface remains `IAsyncEnumerable<TResult>` (C3).

The planner then walks the logical plan, propagates `OutputSignature` bottom-up, and
inserts materialize nodes wherever an operator's required attributes exceed its child's
provided attributes and the child's provenance includes the source relation for the
required attributes. Any requirement that cannot be satisfied (no provenance for the
required source, no path, conflicting constraints) is a diagnostic (R5).

Because `OutputSignature` carries the operator's **output schema** (§5.8), propagation is
schema-aware: a parent's requirement over its input schema is compared against the child's
signature with `Satisfies`, which requires schema identity. A projection is the point
where the schema space changes — requirements *above* it are expressed in the
projection's synthetic output schema, requirements *below* it in the source schema — so a
requirement is never compared across two different schemas.

### 5.10 Value semantics: read, compare, project, hash, null

Consistency across heterogeneous paths is guaranteed by construction: **every path emits
`TupleValue`s over the same schema and ordinals, and all value operations are keyed by the
attribute's `ClrType`, never by the producing path.**

- **Read:** `GetValue(ordinal)` — bounds check, materialized check (throws if missing),
  array index, typed cast. No path knowledge.
- **Compare:** a per-`ClrType` comparer factory (static, cached) resolves
  `IComparer<object?>` for ordering (B-tree key order, `Order by`) and
  `IEquatable`-based equality for join/lookup keys. The B-tree's key ordering is defined by
  the key attributes' CLR types in `KeyOrdinals` order — identical whether the value came
  from the B-tree leaf, a heap + materialize, or an insert. The per-type cache is a
  `ConcurrentDictionary<Type, …>` (or equivalent) so concurrent plan construction does not
  race.
- **Hash:** a per-`ClrType` hasher factory (static, cached, same thread-safe cache as
  comparers) for group-by/join hashing. Hash and equality come from the same per-type
  source so they agree.
- **Null:** a materialized null is a real value and participates in comparison/hashing
  under the chosen null policy; a missing attribute cannot be read at all (§5.3). The
  policy for null *ordering* and *predicate evaluation* (SQL three-valued logic vs. CLR
  semantics) is an open question (§12) — but the representation already supports either,
  because missing and null are distinct states.
- **Projection:** `Project` reads source ordinals and writes output ordinals; no value
  conversion occurs (values are the same CLR objects, merely re-indexed).

### 5.11 Where generics live

| Layer | Generic over `T`? | Currency |
|---|---|---|
| Authoring: `IQueryable<T>`, expressions, `ISource<T>`, `IDerived<T>`, `IPropagateChanges<T>`, `IRelationChangeContext`, `Change<T>`, `BTreePath<T>`, `HeapPath<T>`, `IAccessPath<T>` | **Yes** — retained unchanged | CLR type `T`, expression trees |
| Registration: `RelationRegistry`, schema derivation, codec compilation | **Yes, once at startup** | `T` → `RelationSchema`/`TupleCodec` |
| Translation: expression → logical plan | Bridge | member → ordinal, `T` → `TupleValue` |
| Logical plan, planner, physical operators | **No** | `TupleValue`, `RelationSchema`, ordinals, `OutputSignature` |
| Canonical store, access paths (physical) | **No** | `Rid`, `TupleValue`, `AccessPathDescription` |
| Output edge: `TupleValue` → `TResult` | **Yes, per query** | codec reconstruction |

Generics are **useful** at: authoring (compile-time checking, `ARCHITECTURE.md` §13),
schema derivation
(reflection, once), and value construction (codec). Generics are **inappropriate** at:
physical operators (must compose across relations — a join has no single `T`), the
canonical store (must be provider- and type-independent, R8), and cross-path composition
(D2).

### 5.12 Data flow example

Query (using the existing `Customer` model — relation name `customer` per
`[Relation(Name = "customer")]`, I-NAME — where `ById` provides `Id` + `FirstName` only):

```csharp
tx.ExecuteAsync(ctx => ctx
    .From<Customer, Customer.ById>()
    .Where(c => c.LastName == "Doe"));
```

```text
Authoring:   IQueryable<Customer> over expression tree
                      │  translation (members → ordinals)
                      ▼
Logical:     ConstrainedScan(customer, ById) ── Filter(LastName == "Doe")
                      │  planning (signatures)
                      ▼
Plan:        BTreeScan(ById)                 sig: prov={customer} + {Id, FirstName}
                      │   Filter needs LastName ∉ provided; provenance has customer ⇒ insert materialize
                      ▼
               Materialize(store=customer)   sig: prov={customer} + {Id, FirstName, LastName, Expired, CreatedAt}
                      │
                      ▼
               Filter(LastName == "Doe")      sig: unchanged (pass-through)
                      │
                      ▼
               Output edge: TupleValue → Customer (CLR type) via TupleCodec,
                            streamed as IAsyncEnumerable<Customer>
```

Contrast: an unconstrained `From<Customer>()` with the same filter — the planner may
choose the heap (RID only) + materialize, the B-tree + materialize, or (if a future path
carries `LastName`) that path without materialize. All three plans are expressible with
the same representation; the choice is costing, out of scope here.

**Projection variant** (the case that motivated the §5.8 placement rule — a `Project`
node sits between the scan and the filter, and `LastName` is not in `ById`'s payload):

```csharp
tx.ExecuteAsync(ctx => ctx
    .From<Customer, Customer.ById>()
    .Select(c => new { c.Id, c.LastName })
    .Where(x => x.LastName == "Doe"));
```

```text
Logical:     ConstrainedScan(customer, ById) ── Project({Id, LastName}) ── Filter(LastName == "Doe")
                       │  planning: Project needs LastName ∉ provided (ById provides {Id, FirstName});
                       │  Project changes the schema ⇒ materialize must be below it (placement rule, §5.8)
                       ▼
Plan:        BTreeScan(ById)                 sig: prov={customer} + {Id, FirstName}   (schema: customer)
                       │   row schema here is the relation schema ⇒ merge degenerates to emit-full
                       ▼
                Materialize(store=customer, source schema=customer)
                       │   sig: prov={customer} + full customer schema
                       ▼
                Project({Id, LastName})        sig: prov={customer} + full over synthetic {Id, LastName}
                       │
                       ▼
                Filter(LastName == "Doe")      sig: unchanged (pass-through)
                       │
                       ▼
                Output edge: TupleValue → anonymous type via the (deferred) projection
                             reconstruction API (§12 item 9), streamed
```

The materialize operator sits **below** the projection, so the row schema it sees is the
relation schema and the merge degenerates to emit-full. The operator's selective-merge
path (§5.8 rule 3) would apply if the row's schema were a projection — the placement rule
prevents that in the current operator set, but the operator's contract is defined for
either case so a different placement cannot break it.

**Late materialization after a join** (the case that motivated the provenance model):

The join's projection references `c.LastName`, which is in the `customer` schema but not
in `ById`'s payload. The join output therefore carries `LastName` in its schema but not
in its mask. The provenance retains the customer's canonical identity, so the planner can
materialize `LastName` *after* the join and the filter have reduced the result set — not
before the join, where it would be materialized for every customer that participates in
the join.

```csharp
tx.ExecuteAsync(ctx => ctx
    .From<Customer, Customer.ById>()
    .Join(ctx.From<Order>(), c => c.Id, o => o.CustomerId,
          (c, o) => new { c.Id, c.FirstName, c.LastName, OrderId = o.Id })
    .Where(x => x.OrderId == 42));
```

```text
Logical:     ConstrainedScan(customer, ById) ──┐
              Scan(order) ─────────────────────┤── Join ── Filter(OrderId == 42)
                                              │
Planning:    customer scan   sig: prov={customer} + {Id, FirstName}
              order scan      sig: prov={order} + {Id, CustomerId}
              join            sig: prov={customer, order} + {Id, FirstName, LastName?, OrderId}
                             (LastName is in the join's output schema but not materialized —
                              it was not in ById's payload)
                       │   Filter needs nothing extra; provenance preserved (pass-through)
                       │   Output needs LastName ∉ provided; provenance has customer ⇒ insert materialize
                       ▼
Plan:        BTreeScan(ById) ── Join(Hash) ── Filter(OrderId == 42)
                       │   Materialize(store=customer, attr=LastName) — selective merge
                       ▼
                Output edge: TupleValue → anonymous type via the (deferred) projection
                             reconstruction API (§12 item 9), streamed
```

The key difference from the former single-source model: under that model the join emitted
`HasRid = false`, making `LastName` unmaterializable after the join — the planner would
have been forced to materialize it *before* the join (for every customer that participates
in the join). Under the provenance model the join retains `prov={customer, order}`, and
the materialize operator resolves `provenance[customer]` to the canonical tuple and copies
only `LastName` (selective merge, §5.8 rule 3). The filter sits between the join and the
materialize, so only the surviving rows trigger a canonical lookup.

---

## 6. Impact on Existing Public API

| Type | Change | Justification |
|---|---|---|
| `IRelation<T>` | **Unchanged.** | Marker; still names the logical relation. |
| `ISource<T>` | **Unchanged** in signature. Semantics: implementations convert `T` → `TupleValue` via the codec, insert into the non-generic canonical store, assign the RID, maintain access paths, and record the change set. | Authoring stays generic; physical work is non-generic. |
| `ICanonicalTupleStore<T>` | **Replaced by non-generic `ICanonicalTupleStore`** (§5.5). Breaking. | The authoritative store must not be coupled to a CLR type (R8, C5). This is the core of the design. |
| `InMemoryCanonicalTupleStore<T>` | **Replaced by non-generic `InMemoryCanonicalTupleStore`** (`Dictionary<Rid, TupleValue>`). Concrete proof-of-concept type; churn acceptable. | Follows the interface. |
| `InMemoryHeapStoreProvider<T>` | **Replaced by non-generic `InMemoryHeapStoreProvider`**; `EnumerateTuples(ICanonicalTupleStore<T>)` becomes a RID-only enumeration emitting `TupleValue { Provenance = {relation: rid}, Mask = empty }` — with a `null` values array (zero allocation, §5.3). Resolution of RIDs to full tuples moves out of the heap and into the materialization operator. Unresolvable RIDs become an explicit error at materialization, not a silent skip. | The heap's job is "find RIDs" (`ARCHITECTURE.md` §6.1); materialization is a separate operator (`ARCHITECTURE.md` §8). Removes a C4 violation. |
| `IAccessPath<T>` | **Unchanged** (`Configure()` only). | Authoring surface stays stable. |
| `BTreePath<T>` / `HeapPath<T>` | **Additive:** `Column`/`Include` record their expressions; new read-only `Columns`/`Includes` (and `Includes` for heap) expose them. No signature removals. | Enables `AccessPathDescription` derivation without changing the authoring API. |
| `Rid` | **Unchanged.** | Already a non-generic logical identifier (R1). |
| `Change<T>` / `RelationChange<T>` / `Insert<T>` / `Delete<T>` / `Update<T>` | **Unchanged** in signature. Internally, change sets are accumulated as full `TupleValue` images (§7). | Authoring of propagation stays generic. |
| `IDerived<T>` / `IPropagateChanges<T>` | **Unchanged.** | `Define`/`Propagate` remain `IQueryable<T>` authoring. |
| `IRelationContext` / `IRelationChangeContext` | **Unchanged.** | Authoring surface. |
| `RelationAttribute` | **Unchanged.** | Name source for schema derivation. |
| `IQueryableExtensions` (DML stubs) | **Unchanged** by this design; their implementation will consume the representation. | Out of scope. |
| `ITransaction` | **Unchanged.** | Entry point stays expression-based. |
| `DatabaseBuilder` / `DatabaseContext` / `DatabaseModule` | **Unchanged** in signature; `Source<T>`/`Relation<T>` implementations will drive `RelationRegistry`. | Stubs become real against the registry. |
| `IRelationRewriter` / `OptimizationPipeline` | **Unchanged** by this design; future rewriters operate on the logical plan over schemas/ordinals. | Placeholder status retained. |

**New public types:** `AttributeInfo`, `RelationSchema`, `TupleCodec` (`PolyStore.Schema`);
`MaterializationMask`, `ProvenanceEntry`, `TupleProvenance`, `TupleValue`,
`TupleAttributeNotMaterializedException`, `AmbiguousProvenanceException`,
`OutputSignature` (`PolyStore.Execution`); `AccessPathDescription`
(`PolyStore.Storage`); `RelationRegistry` (`PolyStore.Hosting`).

**Test migration:** the two existing test classes migrate mechanically (`T` values become
`TupleValue`s built via a codec or directly). The canonical-store test class should also be
renamed `InMemoryCanonicalTupleStoreTests` to satisfy `AGENTS.md` naming (pre-existing
inconsistency, fixed while the file is being touched).

---

## 7. Interaction with the Propagation Model

`ARCHITECTURE.md` §19–§23 require propagation over **sets of changes** within one
transaction. The representation consequences:

1. **Change sets carry full canonical images.** A transaction's accumulated changes for a
   relation are:
   - `Inserts`: `IList<TupleValue>` — fully materialized, `Provenance = {relation: rid}`
     (RID assigned by the canonical store at insert);
   - `Updates`: pairs `(TupleValue old, TupleValue new)` — both fully materialized, both
     carrying the relation's provenance;
   - `Deletes`: `IList<TupleValue>` — the pre-delete image, `Provenance = {relation: rid}`.

   Full images are required because a derived relation's `Propagate` may reference *any*
   attribute of the source (e.g., `ArchivedCustomer` reads `FirstName`, `LastName`,
   `CreatedAt` off `Customer` changes). A partial change image would force propagation to
   re-read canonical storage — and after a delete, the tuple is gone. This also matches
   the open question in `ARCHITECTURE.md` §19 ("How are old and new tuple versions exposed?") with the
   conservative answer: **complete old and new images**, with delta-based propagation left
   as a future optimization the representation does not preclude.

    **Memory bound.** A transaction's change-set memory is
    `N_changes × (width × 8 bytes + boxings)` — one full values array plus one box per
    materialized value-type attribute per change. **Updates capture both the old and the
    new image, so they cost 2×** that per change. For a wide relation with a large
    transaction this can be significant; the delta-based path (Open Questions, §12 item 7)
    is the future remedy, and the representation does not preclude it.

2. **Authoring stays generic.** `IRelationChangeContext.Inserts<T>()` etc. remain
   `IQueryable<T>`: the change-context implementation enumerates the `TupleValue` change
   sets and reconstructs `T` via the codec at the authoring edge. `Change<T>(OldValue,
   NewValue)` maps to `(TupleValue, TupleValue)` internally. No authoring API changes.

3. **Propagation plans are ordinary plans.** `Propagate(...)` expressions translate under
   the same rules as queries (§5.9). Change sets are input relations (scans over in-memory
   `TupleValue` lists — trivially fully materialized, provenance as stored). The
   provenance invariant (§5.8) applies: a `Propagate` that joins a change set with
   `context.From<Other>()` emits the union of both inputs' provenance, so late
   materialization after the join remains expressible (the change-set side is already
   fully materialized, so only the `Other` side may need a canonical lookup).

4. **Transaction atomicity is unaffected.** The representation does not change the commit
   boundary: source mutations + access-path maintenance + derived propagation still commit
   as one unit (`ARCHITECTURE.md` §21). It only changes *what value* flows inside that boundary — a
   schema-indexed tuple instead of a CLR object — which is strictly more information
   (provenance + mask) than today's full `T`, a value that carries no record of which of its attributes are materialized or where they came from.

5. **`Fork`/`Insert`/`Update`/`Delete` DML stubs** will consume the same representation
   when implemented: the affected-set is a stream of `TupleValue`s, and the mutation
   operators write full images to the target's canonical store. Out of scope here.

---

## 8. Hot-Path Cost Analysis

Per-tuple costs in the inner execution loop (row-at-a-time, the near-term model):

| Operation | Cost | Notes |
|---|---|---|
| Scan emits a materialized tuple | 1 `object?[]` allocation (schema-sized) + boxing of each materialized **value-type** attribute | `TupleValue` itself is a struct — no extra allocation. A 5-attribute `Customer` scan materializing `Id` (long) + `FirstName` (string) boxes 1 value. Value-type attributes (`long`, `decimal`, `DateTime`, and other value types) each **box once per codec read/write** — 1 allocation each, ~24–40 bytes including the object header. Reference types (e.g. `string`) do not box. |
| Scan emits a RID-only tuple (empty mask) | **0 allocations** (null values array) | Heap scan / RID-only path. `GetValue` throws for every ordinal (no mask bit set), so the null array is never read. Removes the full-width null array from the most common full-scan path (§5.3). |
| Attribute read | bounds check + bit test + array index + unbox (value types) | O(1), no lookup, no reflection. |
| Provenance carry | ~32 bytes per entry (schema ref 8 + RID 16 + instance 8); 1 entry for single-source, 2 for a two-relation join | **Single-source (Count == 1): 0 heap allocations** — the entry is stored inline in the `TupleProvenance` struct, and the single-entry `Create(ProvenanceEntry)` overload avoids the `params` array allocation at the call site (§5.3). **Multi-source (Count ≥ 2):** two-entry `Create(first, second)`: **1 heap allocation** (the overflow array); `params` overload (3+ entries): **2 allocations** (call-site array + overflow array). No boxing (all fields are struct/reference). Only carried when the planner needs it; the planner may discard after materialize (§5.8 rule 5). |
| Materialize | 1 `Dictionary<Rid, TupleValue>` hash + struct return (emit-full) or selective copy (selective merge) | One canonical lookup per row that needs it; a second attribute need on the same row costs nothing (tuple is now full for that source). |
| `WithMaterialized` (selective merge) | O(width) values-array copy **per call**; the materialize operator must batch all *m* attributes of a source into a **single O(width) pass** (not *m* calls at O(m × width)) | The selective-merge path (§5.8 rule 3, batched-write requirement); emit-full avoids it. |
| Mask operations (`IsSet`/`Union`/`CoversAll`) | O(1) bit test for ≤64 attributes; O(width/64) for wider | **No allocation** — the mask backing is shared per operator (invariant, §5.3). |
| Async iterator per-tuple | `TupleValue` struct copy into the state machine's `Current` field + `MoveNextAsync`/`GetResult` indirection | The async state machine is **per-operator, not per-tuple**; operators stream `IAsyncEnumerable<TupleValue>`. |
| Predicate evaluation | compiled delegate over `TupleValue` (compiled once per plan) | No per-tuple expression work (C7). |
| Compare/hash (keys) | cached per-`ClrType` comparer/hash delegate + unbox | Cache is a static `ConcurrentDictionary<Type, …>` per type (§5.10); no per-tuple dictionary of comparers. |
| Output reconstruction | 1 `T` allocation + per-attribute setter delegate calls | Output edge only, not in scan/filter inner loops. |
| Change-set capture | tuple struct copy into a list (values array shared) | Immutability means no defensive copy of the array. |
| Wide-sparse scan (e.g. 100 attributes, 2 materialized) | ~width × 8 bytes per row (≈800 bytes) + 2 boxes, **regardless of materialized count** | The full-size array wastes slots on sparse wide tuples (≈50× space waste). The RID-only case is mitigated by the null array (§5.3); the *partially*-materialized wide case is not — see Open Questions §12.4. |

**Dominant costs:** for materialized tuples, the per-tuple values-array allocation and
value-type boxing. Both are inherent to a row-at-a-time, schema-heterogeneous CLR
representation and are comparable to what LINQ-to-objects pays (which boxes more, since
every `IQueryable` operator over `Expression<Func<T, object?>>`-style paths boxes key
values). **RID-only scans (the most common full-scan path) are now zero-allocation** — the
null values array removes the full-width null array that would otherwise be paid to
represent the (empty) set of materialized attribute values (§5.3). The remaining per-tuple
overhead on that path is the async-iterator
struct copy and the provenance carry (~32 bytes per entry, stored inline in the struct for
the single-source case — zero heap allocations, §5.3).
The provenance cost is only incurred when the planner needs it (late materialization); for
plans that materialize eagerly or do not materialize at all, the planner may discard the
provenance after the materialize operator (§5.8 rule 5) or avoid carrying it entirely.

**Explicitly avoided (C7):** per-tuple reflection; per-tuple schema/name lookup (ordinals
are integers); per-tuple comparer resolution; per-tuple expression compilation; hidden
buffering (operators stream `IAsyncEnumerable<TupleValue>`); per-tuple string-keyed
attribute lookup.

**Not claimed:** this representation is fast in absolute terms. `ARCHITECTURE.md` §26
defers performance claims to measurement. If benchmarks show boxing dominates, the
prescribed remedies are (a) selective-merge materialization to reduce materialized
attribute counts, (b) typed slot arrays per attribute type, or (c) batch execution — all
compatible with this design (§9).

---

## 9. Vectorization Headroom

`ARCHITECTURE.md` §16 requires that the row representation not foreclose vectorized
execution. This design's shared substrate is exactly what a batch model reuses:

- **`RelationSchema` is the unit of shape and is shared by reference.** A future
  `TupleBatch` is naturally `(RelationSchema, int count, object?[][] columns,
  per-row provenance (array of `ProvenanceEntry`s), per-column or per-row validity)` — the
  *same* schema, the *same* ordinals, the *same* per-type comparers/hashers, the *same*
  codec. Nothing in this design is row-specific except the `object?[]` being per-row
  rather than per-column.
- **The mask generalizes.** Per-row `MaterializationMask` becomes a per-column "all rows
  materialized" flag in a uniform batch, or a per-row mask array in a heterogeneous one.
  The missing/null distinction (D1) survives the generalization unchanged.
- **The provenance invariant generalizes** to a per-row array of `ProvenanceEntry`s;
  combining operators emit the union of their inputs' provenance per row, and aggregates
  emit an empty per-row provenance array.
- **Operator interface.** Operators consume/produce `IAsyncEnumerable<TupleValue>` today;
  a batch executor would introduce `IAsyncEnumerable<TupleBatch>` as a *second* currency
  with the same signatures' semantics. Because signatures are expressed in terms of
  schema + ordinals + values (not in terms of `T`), neither currency is privileged.

What this design deliberately does **not** do: define `TupleBatch`, batch validity
semantics, batch-level materialization, or vectorized operator interfaces. That is the
`ARCHITECTURE.md` §16/§28 executor question, to be settled by benchmarking after the row model exists.

---

## 10. Testing Strategy

Semantics-level tests (per `AGENTS.md`: establish semantics, not call sequences), in
`PolyStore.Tests` mirroring the folder layout:

1. **Schema derivation (`Schema/RelationSchemaTests` or `Hosting/RelationRegistryTests`):**
   deterministic ordinal assignment; inherited properties included
   (`ArchivedCustomer`-shaped fixture); nullable detection; name→ordinal round-trip;
   duplicate registration returns the same schema instance.
2. **Tuple/mask semantics (`Execution/TupleValueTests`):** missing vs. materialized-null
   distinction (the D1 test: `string?` attribute — bit clear vs. bit set + null value);
   `GetValue` throws `TupleAttributeNotMaterializedException` on missing; `WithMaterialized`
   does not mutate the original; `WithProvenance` preserves values/mask.
   **Provenance semantics (`Execution/TupleProvenanceTests`):** empty/single/multi-entry
   construction via `Create` — including the **single-entry `Create(ProvenanceEntry)`**
   and **two-entry `Create(ProvenanceEntry, ProvenanceEntry)`** overloads (correct
   `Count`/`IsSingle`/`GetRid` results); **`Create` throws `ArgumentException` for
   duplicate (Source, Instance) pairs** (two-entry and `params` overloads);
   `HasSource`/`GetRid` correctness; `Union` combines
   entries and **throws `ArgumentException` when the union would contain a duplicate
   (Source, Instance) pair**; `GetRid` throws for a source not in the provenance;
   **`GetRid(source)` throws `AmbiguousProvenanceException` when the source appears in
   multiple entries** (self-join case); **`GetRid(source, instance)` returns the correct
   RID for the specified instance** and throws `KeyNotFoundException` for an unknown
   instance.
3. **Codec (`Schema/TupleCodecTests`):** `ToTuple`/`FromTuple` round-trip for value types,
   reference types, nulls, and nullable value types; no per-call reflection (behavioral:
   round-trip correctness + a test that accessors are cached instances).
4. **Canonical store (`Storage/Impl/InMemoryCanonicalTupleStoreTests`):** full/partial
   insert validation (partial insert throws); foreign-schema insert throws; `TryGet`
   returns full tuple with RID; delete semantics. (Migrate existing tests; fix class
   naming.)
5. **Access path description (`Storage/AccessPathDescriptionTests` via registry):**
   `BTreePath` key/payload resolution to ordinals; unresolvable member fails at
   registration; heap description has empty key/payload; `Name` is assigned at
   registration and distinguishes paths on the same relation; paths are registered
   explicitly via `RegisterPath<T, TPath>()` (an unregistered path type fails `Describe`).
6. **Signature rules (`Execution/OutputSignatureTests`):** `Provides`/`Satisfies` logic,
   including the **schema-identity requirement** (a requirement over a different schema is
   never satisfied); scan/values-scan/materialize/filter/project/combine signature
   propagation per the §5.8 table (table-driven).
7. **Materialize semantics (against an in-memory fixture operator):** pass-through when
   covered; **schema-preserving merge** — emit-full when the row schema is the relation
   schema and the operator materializes from that single relation, selective merge (copy
   only the row's attributes) when it is a projection or when materializing from one of
   multiple sources; the output schema always equals the input schema; **stale RID throws**
   (regression against the old silent skip); materialize after a combining operator is
   **expressible** under the provenance model (the join retains both inputs' provenance;
   the materialize resolves `provenance[source]` for each missing attribute) — plan-level
   checks once a minimal planner exists.
8. **Cross-path consistency:** the same logical value read via (a) B-tree payload,
   (b) heap + materialize, (c) direct canonical read compares and hashes identically.
9. **Propagation:** change sets captured as full images; a `Propagate`-shaped fixture
   consuming `Inserts<T>`/`Updates<T>`/`Deletes<T>` sees complete old/new values including
   after deletes.

---

## 11. Assumptions

- **A1.** A relation's attributes are its public instance **properties** (getters),
  including inherited ones. Fields, indexers, and methods are not attributes. (Matches
  every model in `HelloWorld/Models.cs` and the `Column(x => x.Id)` authoring style.)
- **A2.** Attribute identity within a schema is the property **name**; two attributes of
  the same name in one schema are invalid (registration failure). The ordinal is the
  runtime slot, not the identity (I-IDENT, I-SLOT).
- **A3.** Ordinal assignment by sorted property name is acceptable as the deterministic
  rule. (Alternative: source-order via metadata reading; rejected as more complex with no
  current consumer that needs source order.)
- **A4.** The canonical store serves **complete** tuples (KV semantics). Partial canonical
  reads are not a store capability.
- **A5.** Schemas are derived once per process lifetime and are stable; no schema
  evolution/migration is in scope. Runtime ordinals are schema-local slots, not
  persistence identifiers. A persistence format would own its own encoding and key on the
  logical identity (name), not the runtime ordinal.
- **A6.** Row-at-a-time execution is the near-term executor model; this design is
  compatible with, but does not require, it.
- **A7.** Single-process engine. The representation is not designed for cross-process
  transfer, though the schema is in principle serializable (name, type, nullability).
- **A8.** `Rid`'s current Guid-v7 implementation remains the RID type; its eventual
  encoding is an open storage question (`ARCHITECTURE.md` §28) and does not affect this design, which treats
  RID as an opaque value-equality token.
- **A9.** Value semantics use CLR default equality/comparison per attribute type, with the
  null policy (SQL vs. CLR) deferred to §12.
- **A10.** Relation types are **class-based** (records/classes). The codec's
  `ToTuple(object)` / `GetAttribute(object, …)` take the CLR value as `object`, which is
  fine for class types; a record-struct relation type would box the whole struct on every
  call. If struct relation types are ever supported, add a generic `ToTuple<T>(T)` overload
  to avoid boxing the CLR value (§5.4).
- **A11.** Provenance is **engine-internal**: it is metadata on the `TupleValue`, not a
  user-visible relation attribute, not part of the `RelationSchema`, and not reconstructed
  into a CLR value at the output edge. The planner decides when to preserve or discard it.
  This keeps the logical/physical boundary clean: the schema describes logical attributes;
  the provenance describes physical provenance (§5.3, §5.8).

---

## 12. Open Questions

None of these block the representation design; each is flagged for the follow-on planner
or storage designs.

1. **Null semantics in predicates and ordering** (SQL three-valued logic vs. CLR
   semantics) — affects operator implementation, not the representation (missing vs. null
   is already distinct).
2. **Update/delete targeting.** `ISource<T>.UpdateAsync(T)`/`DeleteAsync(T)` take a value,
   not a RID: how is the target tuple identified (natural key? first match?) is a mutation
   design question. The representation supports both (key seek → RID → canonical).
3. **RID scope.** Is a RID unique per relation or globally? The current `Rid` (random
   Guid v7) and per-relation stores imply per-relation; a global canonical store would need
   global uniqueness. Either is compatible with this design.
4. **Wide relations.** The full-size values array wastes slots on sparse tuples of very
   wide relations. A sparse (ordinal→slot) layout is a possible optimization if measured
   necessary.
5. **Selective-merge materialization.** Whether any canonical store implementation will
   serve individual attributes cheaply enough to justify `WithMaterialized`-based merge
   over emit-full. The representation supports both.
6. **Interesting orderings.** A B-tree scan provides an ordering over its key; how the
   planner represents and propagates that is a planner question (the description already
   exposes `KeyOrdinals` for it).
7. **Delta-based propagation.** Whether `Propagate` should ever receive attribute deltas
   instead of full old/new images. Conservative answer today: full images (§7).
8. **`LeftJoin` and other custom operators** used in `HelloWorld/Models.cs` are not yet
   defined in `PolyStore.Core`; their logical/physical mapping (and the synthetic schemas
   they produce) belong to the translation design.
9. **Projection reconstruction API.** `TupleCodec.FromTuple<T>` serves the identity case
   only (§5.4). Reconstructing a projection target type (an anonymous or other type) from a
   projected `TupleValue` requires a separate API — a mapping parameter, or a per-plan
   compiled constructor delegate. This is a translation-design item; it is deferred here
   rather than specified, but it must reuse the codec's accessor mechanism and never
   reflect per tuple.
10. **Dedicated stable attribute ID.** The logical attribute identity is currently the
    name (I-IDENT). A future extension could introduce a persisted stable ID (e.g., a GUID
    or small integer assigned at first registration) for rename-stability under schema
    evolution. Out of scope now (C1).
11. **Physical encoding of the canonical store.** Owned by the storage format (I-PHYSICAL).
    May be slotted (requiring a stable ordered physical schema) or self-describing (e.g.,
    JSON, order-independent). This is a storage-format design, not a representation
    concern. Out of scope for this design.
12. **Self-join provenance disambiguation.** A self-join of relation R with itself produces
   two provenance entries with the same source schema, different RIDs, and different
   instance IDs (in the common 2-way case, 0 for the first input and 1 for the second —
   assigned by the join operator; the general invariant is unique-per-source instance
   IDs, §5.3).
   `TupleProvenance.GetRid(source)` throws `AmbiguousProvenanceException` when the source
   appears multiple times (authoritative statement in §5.3); the two-argument
   `GetRid(source, instance)` overload disambiguates. The planner's remaining work is to
   track the output schema's **attribute-to-source-instance mapping** at plan time so that
   each materialized attribute is resolved against the correct instance. That mapping
   (how the planner records and propagates it through the plan) is a planner-design
   detail; the representation provides the instance discriminator and the
   disambiguating lookup.

---

## 13. Implementation Outline

A plan for a later implementation agent; **not** an implementation. Phases are ordered by
dependency; each is independently testable.

**Phase 1 — Schema model.** `PolyStore/Schema/`: `AttributeInfo`, `RelationSchema`,
deterministic derivation from a CLR type (sorted property names, inherited properties,
nullability). The schema model must encode the logical-identity (name) vs.
runtime-ordinal (slot) distinction: the ordinal is a schema-local slot, not a
persistence identifier (§5.2, I-IDENT/I-SLOT). *Depends on: nothing.* Tests: §10.1.

**Phase 2 — Runtime tuple + codec.** `PolyStore/Execution/`: `MaterializationMask`,
`ProvenanceEntry`, `TupleProvenance`, `TupleValue`, `TupleAttributeNotMaterializedException`,
`AmbiguousProvenanceException`; `PolyStore/Schema/TupleCodec` (compiled accessors).
*Depends on: Phase 1.* Tests: §10.2, §10.3.

**Phase 3 — Non-generic canonical store.** `PolyStore/Storage/`: new
`ICanonicalTupleStore`; `InMemoryCanonicalTupleStore` (non-generic); remove the generic
versions; migrate and extend tests (including the rename to `...Tests`). Also fix the
stale `Rid` doc comment (it says "non-negative integer"; the code is a Guid v7 — the code
is authoritative). *Depends on: Phases 1–2.* Tests: §10.4.

**Phase 4 — Access path description + registry.** `PolyStore/Storage/AccessPathDescription`;
`BTreePath<T>`/`HeapPath<T>` expression recording + read-only exposure;
`PolyStore/Hosting/RelationRegistry` (schema/codec/store/path registration and
validation). `InMemoryHeapStoreProvider` de-genericized to RID-only enumeration.
*Depends on: Phases 1–3.* Tests: §10.5, §10.8 (partially testable in Phase 4; full
execution requires Phase 5's materialize operator and comparers).

**Phase 5 — (separate design) Translation + planning.** The translator
(expression → logical plan over schemas/ordinals), `OutputSignature` propagation, the
materialize operator (generalized for the provenance model), and constraint diagnostics.
This design fixes the representation and invariants it operates on (§5.8–§5.9); the
planner/executor design document should be written before Phase 5 implementation.
*Depends on: Phases 1–4.*

**Phase 6 — (separate design) Propagation wiring.** Change-set accumulation as
`TupleValue` images, change-context authoring edge, transaction commit integration.
*Depends on: Phases 1–5.*

Out of scope for all phases above: costing, batching/vectorization, persistence formats,
concurrency control — all `ARCHITECTURE.md` §28 open areas to be designed on top of this representation.
