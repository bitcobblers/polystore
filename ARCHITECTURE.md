# PolyStore Architecture

PolyStore is an experimental relational storage engine built around a simple idea:

> A relation describes a logical set of tuples. Storage structures describe ways to access those tuples. Neither should define the other.

Relations are exposed through a typed relational API. Tuples have a canonical representation independent of their physical access paths. Queries are expressed as relational operations and compiled into executable plans. Developers can leave physical decisions to the planner or explicitly constrain parts of a plan when predictable execution matters.

PolyStore also explores transactional change propagation between relations, allowing derived relations to be maintained from upstream changes without requiring a separate batch-processing or orchestration system.

This document describes the architectural direction of the project. Some lower-level implementation details remain intentionally unspecified until experimentation establishes the appropriate design.

---

## 1. Design Goals

PolyStore is intended to explore several related ideas.

### 1.1 Separate logical relations from physical access

A relation should not inherently be a "heap table," "column store," "B-tree table," or other physical representation.

Instead:

- the **relation** defines the logical tuple set;
- the **canonical store** owns the authoritative tuple representation;
- **access paths** provide physical strategies for finding or partially materializing those tuples.

A relation may have multiple access paths optimized for different workloads.

### 1.2 Give the planner latitude by default

High-level relational operations describe *what* should be computed.

For example:

```csharp
context.From<Customer>()
    .Where(c => c.Region == "apac")
    .Join(
        context.From<Order>(),
        c => c.Id,
        o => o.CustomerId,
        (c, o) => new { c.Name, o.Total });
```

The planner is free to choose appropriate access paths and physical operators.

### 1.3 Allow developers to constrain physical execution

PolyStore also exposes lower-level operations when physical execution is part of the application's requirements.

Conceptually:

```csharp
context.From<Customer, Customer.ById>()
    .NestedLoop(
        context.From<Order, Order.ByCustomerId>(),
        ...);
```

An explicit access path or physical operator is a **constraint**, not a hint.

If the requested plan cannot be constructed, planning should fail with a diagnostic rather than silently substituting a different strategy.

### 1.4 Keep mutations relational

Insert, update, and delete operations operate on sets.

Mutation results should remain composable with the rest of the relational API rather than introducing a separate statement-oriented language.

The exact mutation surface is still evolving, particularly around the limitations imposed by C# expression trees.

### 1.5 Treat derived data as part of the transactional model

Changes to source relations may propagate through a DAG of derived relations.

The intended invariant is:

> A transaction either commits the source changes and all required derived changes, or commits none of them.

Incremental propagation is intended to operate over sets of changes rather than requiring row-at-a-time processing.

---

# 2. Conceptual Architecture

At a high level:

```text
┌─────────────────────────────────────────────────────────────┐
│                     Application / API                       │
│                                                             │
│   IQueryable<T> relational expressions                     │
│   Explicit physical constraints where requested            │
│   Relational mutation operations                           │
└────────────────────────────┬────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────┐
│                    Relational Expression IR                 │
│                                                             │
│   Logical relational operators                             │
│   Physical constraints                                     │
│   Mutation expressions                                     │
└────────────────────────────┬────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────┐
│                     Planner / Optimizer                     │
│                                                             │
│   Resolve required physical constraints                    │
│   Select access paths                                      │
│   Select physical operators                                │
│   Determine tuple/materialization requirements             │
│   Produce executable operator plan                         │
└────────────────────────────┬────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────┐
│                         Executor                            │
│                                                             │
│   Scan                                                      │
│   Filter                                                    │
│   Projection                                                │
│   Join                                                      │
│   Aggregate                                                 │
│   Mutation                                                  │
│   RID materialization                                      │
│   ...                                                       │
└────────────────────────────┬────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────┐
│                       Storage Layer                         │
│                                                             │
│   Canonical tuple store                                    │
│           │                                                 │
│          RID                                                │
│           │                                                 │
│   ┌───────┼──────────┬──────────┬──────────┐               │
│   │       │          │          │          │               │
│  Heap   B-Tree    Columnar    Vector      ...              │
│                                                             │
│                 Access Paths                                │
└─────────────────────────────────────────────────────────────┘
```

The major boundary is between **logical tuple identity** and **physical tuple access**.

---

# 3. Relations

A relation represents a logical set of typed tuples.

In the C# API, relation schemas are represented using CLR types. This gives the authoring API access to the normal C# type system while allowing expression trees to serve as the initial representation of relational operations.

For example:

```csharp
public class Customer
{
    public long Id { get; init; }
    public string Name { get; init; }
    public string Region { get; init; }
}
```

The CLR type describes the logical tuple shape. It does not imply that `Customer` is physically stored as a conventional row-oriented table.

This distinction is fundamental.

---

# 4. Canonical Tuple Storage

PolyStore currently assumes that every logical tuple has a canonical representation in a key-value-oriented storage layer.

Conceptually:

```text
RID -> Tuple
```

The canonical store answers two fundamental questions:

1. **Which representation of a tuple is authoritative?**
2. **Given a tuple identifier, how can the complete tuple be obtained?**

Access paths do not replace this representation. They provide alternative ways of locating RIDs and, optionally, obtaining some tuple data without accessing the canonical representation.

The exact implementation of the canonical KV store is not yet specified. Its internal page organization, compression strategy, buffer management, persistence format, and concurrency mechanisms remain implementation concerns.

---

# 5. RID

Every stored tuple has a **RID**.

A RID is a logical tuple identifier rather than a physical memory or disk address.

Conceptually:

```text
RID -> canonical tuple
```

This indirection allows physical storage to change without requiring every access path referencing the tuple to be rewritten simply because the tuple moved.

An access path therefore identifies tuples primarily through RIDs:

```text
BTree key ──► RID
Heap entry ─► RID
Vector entry ► RID
Column data ─► RID
```

The precise RID representation is not yet defined.

It may eventually contain information useful for efficient lookup, but consumers should not depend on a RID representing a stable physical location.

---

# 6. Access Paths

An **access path** is a physical structure that provides a way to access tuples belonging to a relation.

Examples under consideration include:

| Access Path | Primary Purpose |
|---|---|
| Heap | Sequential traversal |
| B-tree | Ordered lookup and range access |
| Columnar | Efficient access to selected columns across many tuples |
| Vector | Similarity / nearest-neighbor access |
| Other structures | May be added as required |

An access path is not the canonical tuple representation.

Instead, it describes a way to find or partially realize tuples.

## 6.1 Heap

A heap access path can conceptually be as small as:

```text
RID
RID
RID
RID
...
```

The tuple itself remains in canonical storage.

A heap may optionally carry additional tuple data when doing so provides useful read-performance tradeoffs.

A heap is an access path like any other. PolyStore does not assume that every relation implicitly receives a heap.

## 6.2 B-tree

A B-tree path conceptually stores:

```text
Key -> RID
```

For example:

```text
Customer.Id -> RID
```

This permits efficient lookup of tuples without requiring the B-tree to own the tuple itself.

## 6.3 Columnar and other paths

Column-oriented access paths may provide efficient scans over selected attributes while retaining RID identity.

The exact relationship between columnar encoding, canonical storage, and RID mapping is not yet defined.

Likewise, vector and future access-path implementations are architectural extension points rather than finalized storage designs.

---

# 7. Payload / Included Columns

An access path may carry additional tuple values alongside its primary structure.

For a B-tree:

```text
Key -> RID + payload
```

Conceptually:

```text
Id -> RID, Name, Region
```

These payload values can allow operators to consume required columns without immediately materializing the canonical tuple.

This creates an explicit tradeoff.

| More payload | Less payload |
|---|---|
| More queries can avoid RID lookup | More queries require RID lookup |
| Greater storage consumption | Smaller access structures |
| Greater write amplification | Lower write amplification |
| Potentially faster reads | Potentially cheaper writes |

Payload columns are therefore a physical design decision.

They do **not** determine which relational operations are legal.

A query may use an access path even when that path does not contain every column required by the query. Missing values can be obtained from canonical storage through RID materialization.

---

# 8. Tuple Materialization

Access-path scans produce some combination of:

```text
RID
available tuple values
```

The planner tracks which attributes are currently available.

If an operator requires an attribute that is not available from the current access path, the planner may introduce a canonical tuple lookup.

For example:

```text
BTreeScan Customer.ById
    available: RID, Id, Name
          │
          ▼
Filter on Id
          │
          ▼
RID Materialize
    adds: LifetimeRevenue
          │
          ▼
Filter LifetimeRevenue > 10000
          │
          ▼
Projection Name, LifetimeRevenue
```

An explicit access path therefore constrains **how tuples are initially accessed**, not necessarily which columns can ever be used by the remainder of the query.

This distinction is important.

A query such as:

```csharp
context.From<Customer, Customer.ById>()
    .Where(c => c.LifetimeRevenue > 10_000)
```

is not inherently invalid merely because `LifetimeRevenue` is absent from `ById`.

The planner may retrieve the missing value through the RID.

Whether materialization should occur, where it should occur, and whether another access path would have been cheaper are physical planning questions.

---

# 9. Query API

The C# API is built around `IQueryable<T>` and expression trees.

Expression trees provide a typed representation that can be translated into PolyStore's internal relational representation.

## 9.1 Logical operations

Normal relational operations give the planner latitude:

```csharp
context.From<Customer>()
    .Where(c => c.Region == "apac")
    .Select(c => new
    {
        c.Id,
        c.Name
    });
```

Operations such as:

- `Where`
- `Select`
- `Join`
- `GroupBy`
- ordering
- set operations

describe logical intent.

The planner determines physical execution.

## 9.2 Physical operations

PolyStore may also expose operations that deliberately constrain execution.

Examples include:

```csharp
NestedLoop(...)
```

or selecting a particular access path.

These constructs mean something materially different from ordinary LINQ operations.

Conceptually:

```text
Join()
```

means:

> Produce this relational join.

Whereas:

```text
NestedLoop()
```

means:

> Produce this join using a nested-loop physical operator.

The planner may optimize around such a constraint but must not silently replace the requested physical operation with another implementation.

---

# 10. Planning

The planner translates relational expressions into an executable physical plan.

Its responsibilities include:

- determining applicable access paths;
- selecting physical operators;
- tracking available attributes;
- inserting RID materialization where necessary;
- honoring explicit physical constraints;
- rejecting impossible constraints;
- applying relational rewrites where legal;
- estimating alternative plans where planner latitude exists.

The exact optimizer architecture is not yet defined.

PolyStore may initially use relatively simple rule-based planning and evolve toward more sophisticated costing as the execution and storage models stabilize.

---

# 11. Physical Constraints

Explicit physical operations are treated as planner constraints.

For example:

```csharp
context.From<Customer, Customer.ById>()
```

requires the planner to begin access to `Customer` through `Customer.ById`.

Similarly:

```csharp
NestedLoop(...)
```

requires the corresponding join to use nested-loop execution.

These constraints reduce the planner's search space.

Conceptually:

```text
             unconstrained
                  │
          ┌───────┴───────┐
          │               │
       FIXED           optimizer
      subtree            choice
          │               │
          └───────┬───────┘
                  │
             remaining
             optimizer
              choices
```

The planner remains free to optimize portions of the plan not constrained by the developer.

This provides a continuum rather than two separate query systems:

```text
fully declarative ───────────────► fully constrained
planner chooses                   developer chooses
```

Most queries should be able to remain toward the declarative end of that spectrum.

---

# 12. Constraint Failure

Physical constraints are requirements, not suggestions.

If a requested physical operation is impossible, PolyStore should report why rather than silently choosing another plan.

Examples might include:

- referenced access path does not exist;
- requested operator cannot consume the provided inputs;
- required ordering cannot be established under the specified constraints;
- mutually incompatible physical constraints are present.

Diagnostics should identify the constraint that could not be satisfied and, where practical, explain the conflicting requirement.

This differs from traditional optimizer "hints" that may be advisory or ignored.

---

# 13. Access-Path Evolution

Typed access-path definitions can provide useful development-time properties.

If application code explicitly references:

```csharp
Customer.ById
```

then deleting or renaming that access path can produce a normal compile-time failure in languages capable of expressing the reference statically.

This makes explicit physical dependencies visible in source code.

However, plan stability only exists where the developer explicitly requested it.

An unconstrained query:

```csharp
context.From<Customer>()
```

remains intentionally eligible to use newly added access paths.

Adding an access path may therefore change plans for unconstrained queries while leaving explicitly constrained queries unchanged.

---

# 14. Execution Model

PolyStore is currently oriented around a Volcano-style operator model.

Conceptually, operators expose a common pull interface:

```text
Open
Next
Current
Close
```

A plan might resemble:

```text
Projection
    │
NestedLoopJoin
    ├── Filter
    │     └── BTreeScan
    │
    └── HeapScan
```

Each operator consumes tuples from its children and produces tuples for its parent.

This provides a common execution abstraction across heterogeneous access paths.

---

# 15. Storage Encapsulation

Higher-level relational operators should not need to understand the internal representation of every access path.

For example:

```text
BTreeScan ──────┐
HeapScan ───────┤
ColumnScan ─────┼──► common operator representation
VectorScan ─────┘
```

A join operator should operate on its input streams rather than containing separate implementations for every possible storage pairing.

This is particularly important for cross-storage queries.

For example:

```text
BTree
  │
  ▼
NestedLoopJoin
  ▲
  │
Columnar
```

The join implementation should not fundamentally care that one child originated from a B-tree and the other from column-oriented storage.

Storage-specific behavior should remain as low in the operator tree as practical.

---

# 16. Row-at-a-Time vs. Vectorized Execution

A conventional Volcano iterator is a useful conceptual starting point, but the final execution granularity is not yet fixed.

Possible execution strategies include:

```text
Next() -> Tuple
```

and:

```text
NextBatch() -> TupleBatch
```

Column-oriented execution may benefit substantially from vectorized batches.

PolyStore should avoid unnecessarily coupling relational operators to a row-at-a-time representation if doing so would make later vectorization difficult.

This remains an implementation and benchmarking question.

---

# 17. Mutations as Relational Operations

PolyStore treats mutations as operations over sets.

Conceptually:

```text
source relation
      │
      ▼
    filter
      │
      ▼
   update
      │
      ▼
resulting changed set
```

The changed set can participate in further relational operations.

This avoids a hard boundary between:

```text
query expressions
```

and:

```text
mutation statements
```

The API should support expressing operations equivalent to:

> Update this set of tuples, then use the affected tuples as the input to another relational operation.

The exact C# syntax remains under development.

In particular, ordinary assignment expressions cannot simply be embedded into C# expression trees, so mutation syntax must be designed around the actual capabilities of the expression-tree representation rather than assuming arbitrary C# statements can be captured.

---

# 18. Derived Relations

PolyStore supports the concept of relations whose contents are derived from other relations.

A derived relation has a full relational definition conceptually equivalent to:

```csharp
IQueryable<T> Define(IRelationContext context);
```

`Define()` describes what the relation means.

For example:

```text
ArchivedCustomer =
    Customer
        .Where(customer => customer.Expired)
        .Select(...)
```

This definition is useful both as a semantic description and as a way to construct the relation from its upstream data.

The exact interface shape remains subject to change.

---

# 19. Incremental Change Propagation

Recomputing an entire derived relation after every upstream change may be unnecessarily expensive.

PolyStore therefore explores incremental propagation.

Conceptually:

```text
upstream change set
        │
        ▼
   propagation logic
        │
        ▼
downstream mutations
```

Propagation operates on **sets of changes**, allowing a transaction to accumulate work before downstream relations are updated.

Relevant upstream sets may include:

```text
Inserts<T>
Updates<T>
Deletes<T>
```

The precise representation of updates — including old/new values and how those values participate in relational expressions — remains an API design question.

---

# 20. Propagation DAG

Derived relations form a dependency graph.

For example:

```text
Staging
   │
   ▼
Customer
   ├──────────────► CustomerAnalytics
   │
   ▼
ArchivedCustomer
   │
   ▼
ArchiveAnalytics
```

When `Customer` changes, its downstream dependents may need corresponding changes.

The engine is responsible for understanding dependency order and executing required propagation in a valid sequence.

The graph must be acyclic.

Propagation should therefore resemble:

```text
source mutations
       │
       ▼
capture change set
       │
       ▼
topologically ordered dependent propagation
       │
       ▼
commit
```

rather than application code manually invoking each downstream relation.

---

# 21. Transactional Propagation

The intended transactional invariant is:

```text
source changes
      +
derived changes
      =
one transaction
```

If:

```text
Customer
    ↓
ArchivedCustomer
    ↓
ArchiveAnalytics
```

participate in a propagation chain, observers should not see a committed state in which `Customer` reflects the transaction while required downstream relations do not.

Either the complete required change commits or the transaction fails.

This property distinguishes transactional propagation from conventional asynchronous data-pipeline architectures.

---

# 22. Propagation Correctness

A full relation definition and its incremental propagation logic describe related but distinct things:

```text
Define()     -> what the relation means
Propagate()  -> how a change can be applied incrementally
```

Where both mechanisms exist, an important correctness property is:

> Applying an upstream change and incrementally maintaining the derived relation should produce the same logical relation as evaluating its full definition against the resulting source state.

This provides a potentially powerful testing strategy.

For example:

1. Construct an initial source state.
2. Materialize the derived relation using its full definition.
3. Generate a change set.
4. Apply incremental propagation.
5. Independently evaluate the full definition against the resulting source state.
6. Compare the resulting relations.

Property-based testing may eventually automate this process.

### No implicit correctness fallback

PolyStore should not assume that an empty incremental result means propagation logic was incomplete.

An empty result may legitimately mean:

> This upstream change does not affect this derived relation.

Therefore, the engine cannot generally infer that incremental propagation "missed" a transition and silently fall back to full recomputation.

Whether full recomputation is explicitly available as an execution strategy is a separate design question.

---

# 23. Micro-Batching

Change propagation is intended to operate over sets rather than individual callbacks.

An application may accumulate multiple source mutations within a transaction:

```text
insert
insert
update
delete
insert
      │
      ▼
transaction change set
      │
      ▼
derived propagation
```

This provides many of the computational advantages associated with batching while preserving a transactional boundary.

The application may ultimately control batch size according to workload requirements.

The engine does not require an external event broker or scheduler merely to propagate changes between relations inside the database.

External streaming systems may still be useful when the surrounding application architecture requires them.

---

# 24. Validation

PolyStore should validate structural invariants as early as practical.

Potential validation includes:

- relation definitions are internally consistent;
- access paths reference valid relations and attributes;
- derived-relation dependencies are acyclic;
- physical constraints reference valid physical structures;
- propagation dependencies form a valid graph;
- schemas and operator inputs are compatible.

Different errors may naturally be detected at different stages:

```text
compile time
    │
engine initialization
    │
plan construction
    │
execution
```

The C# type system can catch some errors earlier than the engine.

The engine must not rely exclusively on those language-level guarantees because architectural correctness belongs to the engine rather than to a particular client language.

The exact boundary between initialization-time and planning-time validation remains to be determined.

---

# 25. C# API and Engine Boundary

C# is currently the primary implementation and authoring language.

It provides:

- expression trees;
- LINQ;
- generics;
- strong static typing;
- mature asynchronous primitives;
- high-performance memory APIs;
- straightforward application integration.

The C# API should not, however, define the fundamental semantics of PolyStore.

Conceptually:

```text
C# expression API
        │
        ▼
PolyStore relational representation
        │
        ▼
planner / executor / storage
```

This leaves open the possibility of other frontends in the future.

No commitment has been made to extracting engine components into Rust, C++, or another native implementation. Such a change should be driven by measured implementation requirements rather than assumed in advance.

---

# 26. Performance Philosophy

PolyStore should avoid prematurely encoding performance assumptions into architectural contracts.

Areas requiring measurement include:

- canonical tuple representation;
- page organization;
- compression;
- caching and buffer management;
- RID lookup cost;
- row versus batch execution;
- columnar encoding;
- join implementations;
- concurrency control;
- transaction logging;
- memory allocation;
- access-path maintenance;
- write amplification.

The architecture should make efficient implementations possible without claiming in advance which implementation will prove optimal.

---

# 27. Established Architectural Direction

The following concepts currently represent the strongest architectural commitments:

- Relations are logical typed tuple sets.
- Canonical tuple identity is separate from physical access paths.
- Canonical tuples are addressed through logical RIDs.
- Access paths operate over RID space.
- Access paths may carry payload columns to avoid canonical tuple lookup.
- A relation may expose multiple heterogeneous access paths.
- No particular access-path type is implicitly the canonical representation.
- High-level relational operations give the planner physical latitude.
- Low-level physical operations constrain the planner.
- Explicit physical constraints must not silently degrade into hints.
- Missing attributes can be materialized through RID lookup.
- Physical operators should compose across heterogeneous storage.
- Mutations operate on sets and should remain relationally composable.
- Derived relations may be maintained from transactional change sets.
- Required propagation through a derived-relation DAG occurs transactionally.
- The engine, rather than application code, owns propagation ordering.

---

# 28. Open Design Questions

Several major areas remain intentionally unresolved.

## Storage

- What is the physical representation of the canonical KV store?
- How are RIDs encoded?
- What buffer-management strategy should be used?
- What compression belongs in canonical storage?
- How are large values handled?
- How are access paths persisted and recovered?

## Transactions

- What concurrency-control model should PolyStore use?
- How is MVCC represented?
- How are transaction-local versions addressed?
- What does an "as-of transaction" read mean physically?
- What logging and recovery model is appropriate?

## Planner

- How sophisticated should costing become?
- How are cardinality estimates represented?
- How are interesting physical properties propagated?
- How are partially materialized tuples represented?
- How aggressively should materialization be delayed?
- How are developer constraints represented internally?

## Executor

- Row-at-a-time, vectorized, or hybrid execution?
- How is asynchronous I/O integrated?
- Where should parallelism exist?
- How should memory budgets propagate through operators?
- What execution representation should tuples and batches use?

## Access Paths

- What constitutes the minimum viable columnar path?
- How should vector access integrate with ordinary relational predicates?
- Can an access path reference another access path?
- How are payload updates maintained efficiently?
- How should access-path creation and rebuilding work?

## Change Propagation

- What is the final propagation interface?
- How are old and new tuple versions exposed?
- How are inserts, updates, and deletes composed ergonomically?
- How are aggregate deltas represented?
- When, if ever, should full recomputation be explicitly requested?
- How should propagation interact with very large transactions?

These questions are part of the architecture work rather than details to be silently filled in by an implementation.

---

# 29. Architectural Principle

The central architectural distinction in PolyStore is:

```text
                LOGICAL
                   │
            Relation / Query
                   │
                   ▼
                Planner
                   │
                   ▼
                PHYSICAL
                   │
          ┌────────┼────────┐
          │        │        │
        Heap     BTree   Columnar
          │        │        │
          └────────┼────────┘
                   │
                  RID
                   │
                   ▼
            Canonical Tuple
```

A relation is not its storage structure.

A query is not its execution plan.

An access path is not the authoritative tuple.

A mutation is not necessarily a statement boundary.

A derived relation is not necessarily a separate batch job.

PolyStore attempts to keep those concepts independent while allowing developers to deliberately cross the abstraction boundary when they need physical control.

That separation is the foundation on which the rest of the system is intended to evolve.
