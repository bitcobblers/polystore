# AGENTS.md

Instructions for agents working in PolyStore. Read `ARCHITECTURE.md` before
non-trivial work. Preserve explicit contracts and keep implementations small.

## 1. Authority and Required Context

Before designing, implementing, or reviewing:

1. Read the user request and exact feature design supplied for the task.
2. Read this file and `ARCHITECTURE.md`.
3. Inspect relevant source, usages, project configuration, and tests.
4. Read overlapping designs where they affect a contract.
5. Separate implemented behavior, declared APIs, intended changes, and open decisions.

| Evidence | Authority |
| --- | --- |
| Explicit user direction | Scope and authorized changes, including superseding earlier decisions. |
| Current source and observable behavior | What exists now. Signatures, comments, and samples do not prove missing runtime behavior. |
| `ARCHITECTURE.md` | Repository-wide constraints and the labeled distinction between current state, intent, and open decisions. |
| Exact human-approved design revision | Feature requirements and deliberate baseline changes, subject to repository-wide constraints unless explicitly superseded. |
| Drafts, older designs, and examples | Context; not authority to invent or restore behavior. |

Do not let stale documentation override source facts. Do not treat source as
proof that its behavior is correct when the task is to change it. If an
approved design conflicts with architectural constraints, report the conflict
instead of silently choosing an interpretation.

Do not infer approval from a filename, reviewer verdict, or proposal status
header. Use the invoking workflow's identified human-approved revision.

## 2. Clarifying Architectural Ambiguity

Designers and implementers must request human clarification when a missing or
contradictory decision materially changes semantics. Examples include RID
scope, source-instance aliasing, null/absence behavior, mutation targeting,
transaction visibility, and supported schema/CLR conversion.

First inspect relevant source and supplied designs enough to rule out a
routine lookup or implementation choice. Then report:

- Conflicting statements or the missing contract, with file locations.
- The smallest concrete example showing why the choice matters.
- Plausible interpretations and a recommendation where supported.
- Which work depends on the answer.

Ask the human directly, or route through the invoking orchestrator if the
agent cannot ask directly. Continue independent work where possible; leave
dependent decisions unresolved. Do not mark a proposal implementation-ready
or encode an assumption in tests while required semantics remain ambiguous.

Routine local choices do not require escalation. Choose the smallest approach
consistent with established contracts. Adding `IParsable<Rid>`, for example,
is a public API decision, not an automatic consequence of a formatting task.

## 3. Current Baseline and Storage Reset

At the 2026-10-11 reconciliation baseline (`e910c86`):

- `Rid` is a GUID-v7-backed readonly struct with equality, hashing, and `"N"`
  string formatting. It has no parsing or span-formatting API.
- Heap/B-tree classes are generic authoring stubs; their configuration methods
  do not record or realize anything.
- No canonical-store interface, store implementation, key-schema primitives,
  runtime tuple/schema model, query provider, planner, executor, or transaction
  implementation exists.
- Both xUnit projects exist but contain no C# tests. Only RID benchmarks remain.

Verify the current checkout rather than assuming this inventory stays fixed.
The stores were removed deliberately. Do not restore generic stores/accessors,
removed key types, or their tests merely to satisfy an obsolete proposal.
Replacement storage must follow an approved tuple/schema contract.

The tuple proposal requires revision before implementation. Integration and
benchmark designs/READMEs contain removed-code examples. Use applicable
conventions, but reconcile source claims before using them as specifications.
`ARCHITECTURE.md` identifies this known drift.

## 4. Feature Workspaces and Ownership

Use an isolated branch/worktree unless the invoking workflow already supplies
a workspace. Reuse an assigned tree; do not create nested worktrees per
subagent. Design-only tasks may use their supplied document path without
creating a worktree.

Inspect `git status` first. Preserve user changes. Do not switch branches,
pull into a dirty checkout, or overwrite unrelated work. From a clean primary
checkout, normal setup is:

```bash
git switch main
git pull --ff-only
git worktree add -b <descriptive-branch> ../<workspace-name> main
```

Use descriptive names such as `feat/runtime-tuples`, `fix/rid-formatting`, or
`docs/reconcile-architecture`. Work only in the assigned tree. Incorporate
dependencies through explicit Git operations rather than copying partially
modified files. Do not rewrite shared history or force-delete worktrees/branches.

When concurrent work is explicitly assigned, use separate trees for independent
writers unless shared-tree collaboration is intentional. Follow the invoking
workflow's delegation/permissions; this file does not instruct agents to launch
additional agents. Reviewers must not edit reviewed implementations unless
explicitly assigned that role.

## 5. Architecture and Generic Boundaries

Keep logical relations, execution values/layouts, canonical stored records,
and access-path entries distinct.

Generics belong at typed authoring/registration/conversion boundaries. Current
relation, source, and path declarations remain valid authoring surfaces.
Dynamic-schema physical storage/execution must not require a CLR entity `T`
per record or operator. Do not introduce generic `ICanonicalTupleStore<T>`,
generic physical accessors, or a generic storage provider as a default pattern.

Do not invent schema, codec, registry, capability-interface, or plugin frameworks
without an approved requirement. Stubs do not justify parallel abstractions.
Avoid unnecessary constraints, inheritance, interfaces, service locators,
and global mutable state.

Keep logical APIs independent of specific backends or physical formats. Use
established terms: relation, source, access path, RID, canonical record,
execution tuple, and transaction. Do not recast PolyStore as a CRUD framework.

## 6. Identity, Provenance, and Query Semantics

A relation can occur more than once in a query. Provenance must distinguish
bound relation instances, not merely CLR types or names. Do not assign
identities opportunistically at executing joins or infer RID scope from its value.

Hidden RID columns are a proposed direction, not an implemented layout. Do
not turn that proposal into an API without the required design. Distinguish
absent outer-join sources, SQL nulls, and unavailable attributes. Do not assume
a default RID is an absence sentinel or every operator preserves provenance.
Hidden identity must not change user-visible equality, grouping, or set semantics.

When physical constraints are implemented, honor them or diagnose failure.
Do not silently replace required paths/operators, skip unresolved RIDs, or
ignore unsupported expression nodes. A fallback needs a contract and
observable behavior.

No query provider/executor currently implements the interfaces. LINQ/sample
syntax does not establish PolyStore translation support. Design DML around
expression-tree capabilities and explicit target identity; do not assume
arbitrary C# statements can be captured.

## 7. Async, Transactions, and Propagation

Use `IAsyncEnumerable<T>` for naturally streaming async results. Choose
`ValueTask`/`Task` according to the operation and preserve cancellation.
Do not replace streaming with buffered collections for convenience.

Source mutation and required dependent maintenance share a coherent committed
transaction boundary. Do not add fire-and-forget propagation after commit or
APIs bypassing the transaction contract. Dependency order, cycles, failures,
rollback, visibility, backpressure, and bounded memory need defined behavior
when those features are implemented.

`Define` and `Propagate` have signatures but no engine wiring. `Change<T>` and
`RelationChange<T>` are distinct; do not conflate them. Empty propagation
results may be valid and must not trigger implicit recomputation. Do not invent
MVCC/as-of-read semantics from the current transaction interface.

## 8. Coding Style and Scope

Follow nearby code and `.editorconfig`: four spaces in C#, LF endings, final
newlines, and no trailing whitespace. Nullable annotations are enabled.
Implicit usings are disabled in the library/benchmarks and enabled in the
test/sample projects. Release builds treat warnings as errors.

Prefer immutable/value-like representations where appropriate, small methods,
and expression bodies when clear. Avoid hot-path reflection unless cached or
justified, unnecessary dependencies, unrelated refactors, and speculative
infrastructure. Check platform APIs before writing custom parsers/codecs.

Implement the supplied feature, necessary integration, and meaningful
validation. Do not settle deferred questions, widen the public surface for
hypothetical use, or rewrite an approved design to fit code. Prototypes may
simplify implementation but must identify compromises and preserve boundaries.

## 9. Tests and Benchmarks

Use xUnit. Unit/component tests belong in `PolyStore.Tests`, mirroring the
production subsystem layout. Integration tests belong in
`PolyStore.IntegrationTests` when observable behavior depends on interaction
across an implemented architectural boundary.

Use `<Subject>Tests` for unit classes, and `<ComponentA><ComponentB>BridgeTests`
or `<Scenario>Tests` for integration classes. Namespaces mirror folders.
Group Arrange/Act/Assert clearly; keep fixtures local and deterministic where
practical. Test public semantics rather than private state or incidental calls.

Cover requirements, meaningful boundary/failure cases, cancellation, and
regressions as applicable. Do not test stubs as though they implement engine
semantics, weaken valid adversarial tests, or assert unspecified behavior.
Report specification disputes to the invoking workflow.

Use BenchmarkDotNet in `PolyStore.Benchmarks` for performance claims. The
runner discovers workloads from the assembly and writes to `artifacts/`.
Use `<Subject>Benchmarks`, `[MemoryDiagnoser]`, and a relevant category. Build
fixtures outside measured operations and keep mutable workloads repeatable.
Add shared support only for current workloads; do not restore deleted fixtures
because a README lists them.

Existing RID workload:

```bash
dotnet run --project PolyStore.Benchmarks/PolyStore.Benchmarks.csproj -c Release -- --filter "*RidBenchmarks*"
```

Avoid blind micro-optimization. Separate analytical expectations from
measurements; harnesses alone prove neither correctness nor performance.

## 10. Build and Validation

Use .NET 10 and `PolyStore.slnx`. CI installs `10.0.x`; `global.json` currently
contains the literal SDK version `10.0.1xx` with `latestFeature` roll-forward.
If SDK resolution fails, report the configuration error rather than silently
changing the pin.

For code changes, establish the baseline and run appropriate checks:

```bash
dotnet restore PolyStore.slnx
dotnet format PolyStore.slnx --no-restore --verify-no-changes
dotnet build PolyStore.slnx --no-restore --configuration Debug
dotnet test PolyStore.slnx --no-restore --configuration Debug
```

Run Release checks when warning/performance behavior or publishing is affected.
CI's test job checks formatting and builds/tests Debug; publishing builds/tests
Release. Benchmarks are built with the solution, but `dotnet test` and current
CI do not execute them.

Scoped checks can supplement solution validation. Record commands/results and
baseline failures. No tests discovered is not tests passed. If the SDK,
dependencies, or environment prevents validation, state the limitation.

For documentation-only edits, inspect references, links, consistency, scope,
and `git diff --check`; new engine tests are unnecessary. Do not perform
unrelated repairs to make the baseline appear healthy.

## 11. Documentation and Decision Maintenance

`ARCHITECTURE.md` is the concise repository-wide decision summary, not a copy
of every proposal or an implementation transcript.

When a human-approved design establishes a decision:

1. Summarize its durable contract/rationale in the relevant architecture section.
2. Link the exact design and label the decision accepted but unimplemented,
   implemented, superseded, or still open.
3. Replace conflicting text and remove resolved questions together.
4. Keep detailed algorithms, feature APIs, and validation plans in the design.

When implementation lands, update current-state descriptions and affected
examples/READMEs. Do not leave implemented signatures listed as undecided or
unimplemented proposals described as current behavior. Design-only agents
restricted to a proposal path must hand off architecture updates to its owner.

Human approval controls acceptance; reviewer approval alone does not change
repository-wide decisions. Identify stale approved revisions instead of
silently implementing their obsolete baseline.

## 12. Completion and Reporting

For editing tasks, make actual file changes. Review `git diff --check`, the
final diff, and `git status`. Use the working diff and `git diff main...HEAD`
as appropriate; the latter omits uncommitted edits. Stay within scope and
exclude generated artifacts.

Use conventional commit messages when committing, such as
`feat: add runtime tuple layout` or `docs: reconcile architecture with source`.
Follow user/workflow instructions for commits, PRs, and publication. Do not
claim changes were pushed/integrated unless that happened.

Report concisely:

- Changes and the contracts they establish or preserve.
- Files/workspace/branch and commit, where applicable.
- Checks/results, including unavailable checks or baseline failures.
- Unresolved decisions, deviations, and required human answers.

Completion means the requested result is written/reviewed, validation is
reported accurately, and contradictions are resolved or explicitly surfaced.
Do not substitute explanation for requested files or implementation.
