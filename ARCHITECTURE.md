# PolyStore Architecture

PolyStore is an experimental C#-first relational storage and dataflow system.

The project is exploring a model in which tables, indexes, views, projections, and other derived structures are represented uniformly as **relations** participating in a transactional dataflow graph.

This document describes the current architectural direction. It is not a promise that every detail is final. Where implementation and this document disagree, treat the disagreement as something to investigate rather than silently assuming the implementation is authoritative.

## Goals

PolyStore should provide:

* A strongly typed C# API for defining and querying relations.
* A consistent relational abstraction across mutable and derived data.
* Transactional propagation of changes through relation dependencies.
* Strong internal consistency across asynchronously maintained structures.
* Support for multiple physical storage and materialization strategies.
* Explicit, inspectable query execution rather than opaque abstraction.
* Extensibility without requiring each storage backend to reproduce a complete relational optimizer.
* Fully asynchronous query and storage APIs where I/O may occur.

PolyStore is not intended to hide the capabilities of the underlying storage engine behind a lowest-common-denominator API.

## Core Concepts

### Relation

A relation is the primary logical abstraction in PolyStore.

```csharp
public interface IRelation<T>
{
}
```

`T` is the logical element type of the relation.

Do not constrain `T` to `class`. Value types, records, structs, and other suitable CLR types may represent relation values.

A relation may represent:

* a base table
* a mutable source
* a projection
* an index
* a materialized view
* a transformed relation
* an externally backed dataset

The logical relation should remain distinct from the way its data is physically stored.

### Source

A source is a relation into which changes may enter the PolyStore dataflow.

Conceptually:

```csharp
public interface ISource<T> : IRelation<T>
{
}
```

A source is mutable. A general `IRelation<T>` is not necessarily mutable.

Mutation should therefore be expressed through capabilities rather than assuming that every relation supports writes.

### Relation Change

Changes moving through the dataflow are relation-scoped.

A representative shape is:

```csharp
public readonly record struct RelationChange<T>(
    ChangeKind Kind,
    T? Before,
    T? After);
```

The exact representation may evolve, but change propagation should remain explicit.

Derived relations consume changes from upstream relations and produce their own changes.

### Transaction

Every operation that reads or mutates PolyStore-managed data executes within a transactional context.

A transaction is not merely a storage-provider transaction. It is the consistency boundary across the relation graph.

A transaction may:

1. accept changes into one or more source relations;
2. propagate those changes through dependent relations;
3. persist required physical state;
4. establish a durable transaction identity;
5. make the committed state visible atomically according to PolyStore's consistency model.

Changes must not become independently visible in different internal relations merely because asynchronous work occurs between stages.

## Consistency Model

Strong consistency inside PolyStore is a core design goal.

In particular, PolyStore should avoid the class of behavior where:

1. a write succeeds;
2. direct lookup immediately sees the new item;
3. a secondary index or search relation does not yet see it.

Asynchronous execution does not imply eventual consistency.

Internal relation maintenance may use asynchronous handoffs, but committed reads should observe a coherent transaction boundary.

### Transaction Identity

Committed transactions should have stable identities.

The architecture should support reads conceptually equivalent to:

```text
read these relations as of transaction 123
```

This enables consistent reads across structures that may be physically maintained through different asynchronous paths.

The exact MVCC/versioning implementation is intentionally unspecified here.

### External Systems

External connectors may not be able to participate in the same consistency guarantees.

PolyStore should distinguish between:

* strongly consistent internal relations;
* externally backed or asynchronously synchronized relations.

Do not silently weaken internal consistency merely because some external integrations are eventually consistent.

## Dataflow Model

Relations form a directed dependency graph.

For example:

```text
Orders
  |
  +--> OrdersByCustomer
  |
  +--> OpenOrders
          |
          +--> OpenOrdersByPriority
```

A mutation entering `Orders` may cause changes to propagate through all affected descendants.

The graph is part of the transactional model.

### Derived Relations

Derived relations should be expressed declaratively where practical.

Examples include:

* projections
* filters
* indexes
* aggregates
* joins
* materialized query results

An index is not conceptually a special side structure disconnected from the relational model. It is a maintained relation with a particular access strategy.

### Reactive Implementation

Reactive mechanisms such as `IObservable<T>` may be useful internally for representing change propagation.

They should not automatically become the public query API.

Rx introduces significant semantic complexity, especially around scheduling, asynchronous handoffs, producer speed, buffering, and backpressure. Any Rx-based implementation must make those behaviors explicit and bounded.

Do not assume that converting a pipeline to asynchronous Rx makes it safe under an unbounded producer.

## Query API

The logical query API is expected to use familiar LINQ concepts.

Representative usage:

```csharp
await foreach (var order in database
    .From<Orders>()
    .Where(x => x.CustomerId == customerId)
    .ExecuteAsync(cancellationToken))
{
    // ...
}
```

Query results that may involve asynchronous work should use:

```csharp
IAsyncEnumerable<T>
```

rather than:

```csharp
Task<IEnumerable<T>>
```

or:

```csharp
ValueTask<IEnumerable<T>>
```

Streaming is part of the execution model.

### IQueryable

`IQueryable<T>` may be used as part of query construction where its semantics are useful.

Do not assume that `IQueryable<T>` by itself is sufficient to model:

* asynchronous execution;
* mutation;
* change streams;
* physical capabilities;
* transaction semantics.

Expression-tree translation is a tool, not the architecture.

## Query Planning

PolyStore should avoid attempting to reproduce decades of optimizer engineering present in mature storage engines.

For storage providers such as PostgreSQL, the intended direction is approximately:

```text
PolyStore logical query
        |
        v
Intermediate relational representation
        |
        v
Candidate backend query
        |
        v
Backend EXPLAIN / plan information
        |
        v
PolyStore validation / adaptation
        |
        v
Executable backend query
```

The exact pipeline may change.

The important architectural principle is:

> PolyStore should exploit backend optimizers rather than pretending they do not exist.

### Generated SQL Must Be Inspectable

When a SQL backend is used, users must be able to inspect the SQL that PolyStore intends to execute.

Generated SQL should not be treated as an implementation secret.

Query-planner behavior is already difficult to diagnose. PolyStore must not introduce another opaque layer that prevents users from understanding what reaches the backend.

## Parametric Queries

PolyStore projections may expose optional filtering or other parameters.

Unused parameters should disappear structurally from the generated query rather than becoming expressions such as:

```sql
WHERE (@customer_id IS NULL OR customer_id = @customer_id)
```

when a more selective query can be generated.

For sufficiently complex queries, this requires structural query templating rather than merely appending predicates to the end of an existing query.

For example, filters may need to affect:

* CTE definitions;
* join placement;
* subqueries;
* aggregation inputs;
* backend-specific constructs.

The query representation must therefore permit structural variation.

### Fast Feedback

Inspecting the generated query should be cheap and immediate.

A user should not need to:

1. modify a query;
2. invoke a separate build or generation command;
3. inspect an artifact;
4. repeat the process.

Query construction and backend-query inspection should participate in a tight development loop.

## Storage Architecture

Logical relations are separate from physical realization.

### Realize

`Realize` is the conceptual definition-time operation that assigns physical storage or access behavior to a relation.

For example:

```csharp
relation.Realize(...);
```

The public API is still evolving.

Do not make an `IRealization` abstraction central to normal query execution unless a concrete design requires it.

Realization is primarily a configuration concern.

### Storage Provider

A storage provider represents a backing storage technology capable of supporting multiple relations.

Conceptually:

```csharp
public interface IStorageProvider
{
    IRelationAccessor<T> CreateAccessor<T>(...);
}
```

The provider itself should generally remain non-generic.

Generic behavior belongs on relation-scoped methods and accessors.

Potential providers include:

* PolyStore-native page storage;
* append-oriented storage;
* PostgreSQL;
* SQLite;
* in-memory storage;
* test implementations.

PostgreSQL and SQLite should be treated as possible storage providers, not as the architectural definition of storage itself.

### Relation Accessor

`IRelationAccessor<T>` separates logical relation/query machinery from physical storage.

Representative shape:

```csharp
public interface IRelationAccessor<T>
{
}
```

Specialized capability interfaces may include:

```csharp
public interface IScanAccessor<T> : IRelationAccessor<T>
{
}

public interface ISeekAccessor<T> : IRelationAccessor<T>
{
}

public interface IWriteAccessor<T> : IRelationAccessor<T>
{
}
```

Capabilities should be explicit.

Do not assume that every storage implementation supports every access pattern.

Query planning may use these capabilities when selecting an execution strategy.

## API Design Principles

### Prefer Enforceable Contracts

Prefer:

1. type-system enforcement;
2. explicit metadata;
3. attributes;
4. runtime validation;

over undocumented naming conventions.

Convention-based behavior should be used cautiously because it creates contracts that tooling cannot reliably enforce.

### Use Attributes Where They Describe Metadata

Some relation characteristics may be appropriately expressed using attributes.

Examples might include:

```csharp
[Mutable]
public sealed class Orders : IRelation<Order>
{
}
```

or:

```csharp
[Derived]
public sealed class OpenOrders : IRelation<Order>
{
}
```

Attributes should describe metadata, not hide substantial runtime behavior.

### Avoid Duplicate Schema Definitions

A relation should not require users to define:

1. the CLR relation type; and
2. a separate central `DatabaseSchema` property bag containing the same information.

Discovery should preferably occur through DI, registration, attributes, generated metadata, or another mechanism that keeps each relation's definition close to the relation itself.

### Inference Must Be Conservative

PolyStore may infer information when it can do so reliably.

Do not infer semantics that could silently change correctness.

It is better to require explicit configuration than to create a convenient but ambiguous contract.

## Dependency Injection and Hosting

Relation discovery and application hosting are separate concerns.

PolyStore should integrate naturally with .NET dependency injection without requiring all consumers to use a particular hosting model.

Possible responsibilities include:

```text
DI registration
    discovers relation definitions
    discovers storage providers
    discovers transformations

Runtime bootstrap
    validates relation graph
    validates capabilities
    realizes relations
    constructs execution services
```

Avoid turning relation classes into service locators or giving them broad access to application infrastructure.

## Asynchrony

PolyStore is asynchronous by design where asynchronous work may occur.

Preferred APIs include:

```csharp
ValueTask
ValueTask<T>
IAsyncEnumerable<T>
```

depending on semantics.

Use `IAsyncEnumerable<T>` for sequences.

Use `ValueTask<T>` only where its tradeoffs are justified; do not use it mechanically merely because an operation is asynchronous.

### ConfigureAwait

Library code should not depend on a caller's synchronization context.

Whether `ConfigureAwait(false)` is used explicitly should be consistent with the project's target framework and coding policy rather than scattered defensively throughout the implementation.

Do not assume code will only ever execute on the default thread pool.

## Mutations

Mutation syntax is still evolving.

The intended direction is that mutation participates naturally in the query/dataflow model rather than being implemented as an unrelated CRUD API.

Potential operations include:

```csharp
Insert(...)
Update(...)
Delete(...)
```

Updates may use expression-based transformations, including record `with` expressions where practical.

Mutation APIs must preserve:

* transaction boundaries;
* relation capability checks;
* change propagation;
* `RETURNING`-style result projection where supported.

Do not finalize mutation syntax without considering transaction semantics first.

## Error Handling

Prefer explicit failures over silent fallback when correctness could change.

Examples:

* unsupported query construct;
* missing physical capability;
* ambiguous relation metadata;
* invalid relation graph;
* backend translation failure;
* inconsistent transaction state.

A fallback is acceptable only when its semantics are equivalent or the caller explicitly opted into the behavior.

## Performance Philosophy

Correctness comes first, but PolyStore is intended to be performance-oriented infrastructure.

Avoid architecture that inherently requires:

* materializing entire result sets;
* unbounded buffering;
* unnecessary object allocation;
* repeated expression compilation;
* redundant schema reflection;
* excessive abstraction around tight storage loops.

Optimization should follow measurement, but obviously pathological designs should not be introduced merely for API elegance.

## Source Layout

The exact repository layout may evolve, but dependency direction should remain intentional.

A likely structure is:

```text
src/
  PolyStore/
    Relations/
    Query/
    Transactions/
    Storage/
    Dataflow/
    Hosting/

tests/
  PolyStore.Tests/
```

As the project grows, separate assemblies may be introduced around stable architectural boundaries.

Do not split assemblies merely to create apparent modularity.

## Architectural Invariants

Unless an explicit design decision changes them, preserve these assumptions:

1. `IRelation<T>` is the core logical abstraction.
2. `T` is the logical relation element type and has no `class` constraint.
3. Mutability is a capability, not an intrinsic property of every relation.
4. Transactions span relation/dataflow behavior rather than only physical storage calls.
5. Internal async propagation must not imply eventual consistency.
6. Cross-relation committed reads must be capable of observing a coherent transaction version.
7. Logical relations are distinct from their physical realization.
8. `IStorageProvider` represents a storage technology and is not relation-generic.
9. Physical access is expressed through relation-scoped accessors and explicit capabilities.
10. Query result sequences use `IAsyncEnumerable<T>` when asynchronous execution is possible.
11. Backend optimizers should be leveraged rather than reimplemented unnecessarily.
12. Backend queries, especially generated SQL, must remain inspectable.
13. Parametric queries may structurally change generated queries.
14. Relation definitions should avoid duplicate centralized schema declarations.
15. Prefer enforceable contracts over implicit conventions.

## Open Design Areas

The following areas are intentionally unresolved:

* exact transaction/version representation;
* MVCC implementation;
* relation graph construction;
* mutation syntax;
* change-set representation;
* physical page/storage format;
* native index organization;
* optimizer architecture;
* SQL intermediate representation;
* relation realization API;
* query-plan caching;
* schema evolution;
* distributed/external relation semantics;
* Rx versus custom change-propagation machinery.

Do not treat an unresolved area as permission to choose an irreversible design casually.

When implementing one of these areas, document the decision and the alternatives considered.

## Decision Records

Significant architectural decisions should eventually be recorded separately, for example:

```text
docs/
  adr/
    0001-relations-as-core-abstraction.md
    0002-transaction-versioned-reads.md
```

An ADR is appropriate when a change:

* establishes a major abstraction;
* changes an architectural invariant;
* constrains future storage implementations;
* introduces a difficult-to-reverse dependency;
* materially changes consistency semantics.

## Status

PolyStore is early-stage software.

Expect experimentation.

The goal is not to preserve every prototype. The goal is to preserve the architectural reasoning behind the system while allowing implementation details to evolve.
