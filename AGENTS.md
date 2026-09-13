# AGENTS.md

This file provides instructions for coding agents working in the PolyStore repository.

PolyStore is an early-stage storage-engine and relational-dataflow project. Correct architectural reasoning is more important than maximizing the amount of code produced.

Read `ARCHITECTURE.md` before making non-trivial changes.

## Primary Rule

Do not silently invent architecture.

If a requested change touches an unresolved architectural area, implement only what can be justified from the existing design and clearly identify assumptions.

Prefer a small coherent implementation over a broad speculative framework.

## Project Context

PolyStore is a C#-first relational/dataflow storage system.

Important concepts include:

* `IRelation<T>` as the core logical relation abstraction;
* `ISource<T>` for mutable ingress relations;
* transactional propagation through relation dependencies;
* strong internal consistency despite asynchronous execution;
* transaction-version-aware reads;
* logical relations separated from physical realization;
* relation-scoped storage accessors;
* multiple storage providers;
* LINQ-oriented query construction;
* `IAsyncEnumerable<T>` for asynchronous query sequences.

Do not reinterpret the project as a conventional repository/service CRUD framework.

## Feature Workspaces

Feature work should normally be performed in an isolated Git worktree rather than directly in the primary checkout.

This is particularly important when multiple developers or coding agents may work on the repository concurrently. Each feature should have its own branch, working directory, build artifacts, and uncommitted state.

The primary checkout should remain clean and should not be used as scratch space for feature implementation.

### Creating a Workspace

Before starting feature work, synchronize the primary checkout:

```bash
git switch main
git pull --ff-only
```

Create a feature branch and corresponding worktree from the current `main`:

```bash
git worktree add -b <branch-name> ../<workspace-name> main
```

For example:

```bash
git worktree add -b feat/transaction-context ../polystore-transaction-context main
```

Then perform all work for that feature from the new workspace:

```bash
cd ../polystore-transaction-context
```

Do not switch the worktree to an unrelated branch or reuse it for unrelated feature work.

### Branch Naming

Use short, descriptive branch names consistent with the purpose of the change.

Examples:

```text
feat/transaction-context
feat/relation-accessors
fix/async-scan-cancellation
refactor/query-expression-visitor
test/transaction-visibility
docs/storage-architecture
```

Avoid generic branch names such as:

```text
changes
work
agent
test
feature
wip
```

The branch name should make it possible to identify the purpose of a workspace without inspecting its contents.

### Workspace Initialization

After creating the worktree, restore and verify the repository before making changes:

```bash
dotnet restore
dotnet build
dotnet test
```

This establishes that the starting revision is healthy.

If the baseline build or tests fail, determine whether the failure already exists before modifying code. Do not silently attribute a pre-existing failure to feature work or attempt unrelated repairs unless they are required to proceed.

### Workspace Isolation

Treat each worktree as an independent unit of work.

Do not:

* share uncommitted changes between worktrees;
* copy partially modified source files between worktrees;
* make unrelated changes in another feature's workspace;
* use `git stash` as the normal mechanism for moving between features;
* switch branches merely to begin unrelated work when a separate worktree is appropriate.

If another feature contains work that is required by the current feature, prefer an explicit Git operation such as merging, rebasing, or cherry-picking the relevant commit rather than copying files manually.

This keeps dependencies between concurrent changes visible in repository history.

### Working With Multiple Agents

When multiple agents are working concurrently, each agent should operate in its own worktree unless the agents are deliberately collaborating on the same working tree.

Do not assume that another agent's uncommitted changes are available.

Do not modify another agent's workspace.

Do not create commits containing changes that originated in another workspace unless those changes were intentionally incorporated through Git.

A useful mapping is:

```text
task
  -> branch
      -> worktree
          -> agent
```

This allows independent features, experiments, and refactors to proceed without branch switching, stashing, or accidental interference.

### Keeping a Workspace Current

Feature branches may need to incorporate changes from `main`.

First update `main` from the primary checkout:

```bash
git switch main
git pull --ff-only
```

Then, from the feature workspace, incorporate the updated `main` using the repository's current integration policy.

For an unpublished local feature branch, rebasing is generally appropriate:

```bash
git rebase main
```

Resolve conflicts deliberately. Do not automatically choose one side of an architectural conflict merely to complete the rebase.

After rebasing or merging, run:

```bash
dotnet build
dotnet test
```

### Completing Feature Work

Before considering the workspace complete:

```bash
git status
dotnet build
dotnet test
```

Review the final diff:

```bash
git diff main...HEAD
```

The workspace should contain only changes relevant to its feature.

Check specifically for:

* accidental formatting changes;
* generated files;
* debugging code;
* temporary instrumentation;
* unrelated refactors;
* editor-specific files;
* commented-out experiments.

Commit the completed work using the repository's conventional commit style.

### Removing a Workspace

Once the branch has been integrated and the workspace is no longer needed, remove the worktree from the primary checkout:

```bash
git worktree remove ../<workspace-name>
```

Then prune stale worktree metadata if necessary:

```bash
git worktree prune
```

Delete the local branch only after confirming that its work has been safely integrated:

```bash
git branch -d <branch-name>
```

Never force-delete a branch merely to make workspace cleanup succeed.

### Agent Responsibility

An agent creating a feature workspace owns that workspace for the duration of the task.

Before reporting completion, the agent should be able to identify:

* the workspace it used;
* the branch containing the work;
* the commits it created;
* the build/test status;
* any dependency on changes from another branch.

Workspace isolation is a correctness mechanism, not merely a convenience. It should make parallel development safer and make the provenance of every change obvious.

## Before Editing

Before making a meaningful change:

1. Read the relevant implementation.
2. Read nearby tests.
3. Read `ARCHITECTURE.md`.
4. Search for existing interfaces or terminology before introducing new ones.
5. Determine whether the requested work changes an architectural invariant.

Do not generate parallel abstractions merely because the existing implementation is incomplete.

## Build and Test

The repository targets modern .NET.

At minimum, validate changes using:

```bash
dotnet build
dotnet test
```

If the repository provides a solution file, prefer building and testing the solution.

Do not claim a change is complete if the relevant tests were not run.

If tests cannot run because of environment limitations, state that explicitly.

## Coding Style

Follow the existing repository style.

General preferences:

* nullable reference types enabled;
* explicit cancellation support for potentially long-running async operations;
* immutable types where practical;
* records or readonly structs for value-like data;
* small interfaces with meaningful semantics;
* avoid unnecessary inheritance;
* avoid service-locator patterns;
* avoid global mutable state;
* avoid reflection in hot paths unless cached or otherwise justified.

Do not introduce a dependency merely to save a small amount of implementation code.

## Naming

Use terminology already established by the architecture.

Preferred terms include:

```text
Relation
Source
Change
Transaction
Accessor
StorageProvider
Realize
Projection
Derived relation
```

Be cautious about introducing alternate terms such as:

```text
Repository
EntitySet
Collection
DAO
Manager
Handler
Store
```

unless they describe a genuinely different concept.

In particular, do not rename relations to repositories or entities merely because those terms are more common in application frameworks.

## Generic Design

Generics should normally be scoped to the relation value type.

Preferred:

```csharp
IRelation<T>
RelationChange<T>
IRelationAccessor<T>
IScanAccessor<T>
ISeekAccessor<T>
IWriteAccessor<T>
```

Database-wide abstractions should generally remain non-generic.

Preferred:

```csharp
public interface IStorageProvider
{
    IRelationAccessor<T> CreateAccessor<T>(...);
}
```

Avoid unnecessary constraints such as:

```csharp
where T : class
```

unless required by a concrete semantic reason.

## Async APIs

PolyStore is asynchronous where I/O or streaming may occur.

For asynchronous sequences, prefer:

```csharp
IAsyncEnumerable<T>
```

Do not return:

```csharp
Task<IEnumerable<T>>
```

when results can naturally be streamed.

Always consider cancellation.

For example:

```csharp
IAsyncEnumerable<T> ExecuteAsync(
    CancellationToken cancellationToken = default);
```

Do not convert streaming APIs into buffered collections for implementation convenience.

## Transactions

Treat transaction semantics as an architectural concern.

Do not implement a mutation API that bypasses the transaction model.

Do not assume a transaction is simply:

```csharp
DbTransaction
```

or another storage-provider-specific transaction object.

A PolyStore transaction may coordinate:

* source mutations;
* relation change propagation;
* derived relation maintenance;
* physical persistence;
* committed transaction identity;
* version-consistent reads.

When adding transactional behavior, inspect downstream effects across the relation graph.

## Consistency

Do not introduce eventual consistency between internally maintained PolyStore relations unless explicitly requested by the architecture.

An async handoff is not permission for downstream state to become visible later than upstream state.

This is an important invariant.

Code resembling:

```text
commit source
queue index update
return success
```

is suspect for internally managed relations.

If asynchronous propagation requires staging or versioning, preserve a coherent committed transaction boundary.

## Dataflow

Relations may depend on other relations.

Changes should propagate through this graph.

Do not model an index, projection, or materialized view as an unrelated side cache if it is logically part of the PolyStore relation graph.

When adding graph execution:

* consider dependency ordering;
* consider cycles;
* consider failure propagation;
* consider transaction visibility;
* consider backpressure;
* consider bounded memory usage.

## Reactive Extensions

Rx may be appropriate for internal change propagation.

Do not adopt Rx casually.

Any Rx-based implementation must explicitly consider:

* scheduler behavior;
* async boundaries;
* producer/consumer imbalance;
* buffering;
* disposal;
* error propagation;
* backpressure or the lack thereof.

Avoid unbounded queues between a fast producer and asynchronous consumer.

Do not expose `IObservable<T>` as a public API merely because Rx is used internally.

## Query Translation

Do not implement a large custom optimizer without a concrete requirement.

When targeting an established database backend, prefer using the backend's planner where feasible.

Generated backend queries must remain inspectable.

Do not create abstractions that make it difficult to determine what SQL or equivalent backend operation will actually execute.

## Parametric SQL

Optional query parameters may require structural query changes.

Do not automatically implement optional filters as:

```sql
WHERE (@value IS NULL OR column = @value)
```

if the architecture expects the filter to disappear from the generated query.

A parameter may affect:

* predicates;
* joins;
* CTEs;
* subqueries;
* aggregations;
* selected relations.

Treat parametric query generation as a structural problem.

## Storage Abstractions

Do not couple logical relation behavior directly to PostgreSQL, SQLite, or another prototype backend.

A backend may implement storage capabilities, but the logical architecture should remain provider-independent.

Keep the distinction between:

```text
logical relation
physical accessor
storage provider
```

### Accessor Capabilities

Prefer explicit capability interfaces.

For example:

```csharp
IRelationAccessor<T>
IScanAccessor<T>
ISeekAccessor<T>
IWriteAccessor<T>
```

Do not add methods to a universal accessor merely because one backend supports them.

The absence of a capability should be representable.

## Schema and Discovery

Avoid requiring duplicate relation declarations.

Do not introduce a central class containing one property per relation unless there is a compelling architectural reason.

Prefer mechanisms such as:

* DI registration;
* explicit registration APIs;
* attributes;
* generated metadata;
* validated reflection during bootstrap.

Avoid undocumented naming conventions.

Inference should be conservative and deterministic.

## Scope Control

Agents tend to overproduce infrastructure. Do not do that here.

When asked to implement one component:

* do not add unrelated factories;
* do not create multiple layers of abstraction "for the future";
* do not introduce a generic plugin system unless required;
* do not rewrite neighboring code solely for stylistic consistency;
* do not create public APIs for speculative use cases.

A useful heuristic:

> Introduce an abstraction when at least one current architectural boundary requires it, not merely because a future implementation might.

## Tests

Prefer tests that establish semantics rather than implementation details.

Good tests include:

* relation capability behavior;
* transaction visibility;
* propagation ordering;
* query translation;
* cancellation;
* streaming behavior;
* failure rollback;
* version-consistent reads.

Avoid tests that merely reproduce the exact internal call sequence unless that sequence is itself part of the contract.

For bugs, add a regression test when practical.

## Performance

This is infrastructure code.

Be alert to:

* unnecessary allocations;
* repeated reflection;
* expression-tree recompilation;
* hidden buffering;
* LINQ in extremely hot inner loops;
* accidental quadratic graph traversal;
* synchronization bottlenecks;
* unbounded channels or queues.

Do not micro-optimize blindly.

When performance is the reason for a complex implementation, add a benchmark or explain how the improvement was established.

## Error Handling

Fail explicitly when semantics cannot be preserved.

Do not silently:

* fall back from seek to full scan;
* ignore unsupported expression nodes;
* drop transaction semantics;
* skip propagation;
* swallow backend planner failures;
* ignore relation graph inconsistencies.

A fallback may be valid, but it should be deliberate and observable.

## Public API Changes

Treat public API changes conservatively.

Before changing an existing public type:

1. determine why it exists;
2. search for current usages;
3. check architectural documentation;
4. avoid widening the API unnecessarily.

Do not retain a poor abstraction solely for compatibility while the project is still experimental, but do not churn public APIs without a reason.

## Documentation

Update documentation when introducing a new architectural concept or changing an invariant.

Small implementation changes do not require documentation churn.

If the change resolves an item listed as open in `ARCHITECTURE.md`, update that section.

For major architectural decisions, consider adding an ADR.

## Commit Discipline

Keep changes focused.

A commit should ideally represent one coherent reason for change.

Use conventional commit messages where appropriate, for example:

```text
feat(query): add async relation execution
fix(storage): preserve transaction version during scan
refactor(relations): separate write capability from base accessor
test(transactions): cover derived relation visibility
docs(architecture): document realization boundary
```

Do not mix broad formatting changes with behavioral changes.

## When Asked to Prototype

Prototype code should still preserve architectural boundaries.

It is acceptable for a prototype to:

* use an in-memory provider;
* use PostgreSQL or SQLite temporarily;
* use simplified transaction persistence;
* support only a subset of expression nodes.

It is not acceptable for a prototype to accidentally redefine the architecture around those limitations.

Label temporary compromises clearly.

## When Architecture Is Ambiguous

If multiple implementations are plausible, prefer the one that:

1. preserves existing invariants;
2. introduces the fewest new concepts;
3. is easiest to replace;
4. exposes correctness issues rather than hiding them;
5. keeps physical-storage details out of logical APIs.

Do not guess at large new architectural policy based solely on common industry patterns.

PolyStore intentionally differs from conventional ORM and CRUD designs in several places.

## Architectural Red Flags

Reconsider the implementation if it introduces any of the following without an explicit reason:

```text
IRelation<T> where T : class
Task<List<T>> as the primary query result
a DatabaseSchema property bag containing every relation
a generic IStorageProvider<T>
a single accessor interface containing every possible storage operation
fire-and-forget derived relation updates after commit
unbounded Rx buffering
opaque generated SQL
backend-specific concepts in the logical relation API
repository-per-relation CRUD abstractions
automatic convention-based behavior that cannot be validated
```

## Definition of Done

For a normal implementation task, completion generally means:

* the requested behavior is implemented;
* architecture remains coherent;
* relevant tests exist or were updated;
* `dotnet build` succeeds;
* `dotnet test` succeeds;
* public APIs and documentation are updated when necessary;
* no unrelated speculative framework was added.

If one of these could not be completed, state which one and why.
