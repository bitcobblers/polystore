# Runtime Tuple Representation

**Status:** Proposed — implementation-ready for the scope below; pending human approval. **Revised:** 2026-10-11. **Source baseline:**
`e910c86534ac28cd292fa6d53c8eb3575cdb63f9`. **Requirements basis:** the storage reset and the accepted 2026-10-11
[architecture](../../ARCHITECTURE.md) and [agent guidance](../../AGENTS.md)
reconciliation.

## Executive Summary

Introduce a non-generic runtime tuple whose immutable physical layout describes
exactly the values present in each row. Carry canonical RIDs as hidden physical
columns keyed by bound relation instances, so self-joins retain distinct identities
and later operators can request missing attributes. A compact slot array replaces
the previous full-relation array, materialization mask, and separate provenance
collection. This revision implements only schemas, identities, layouts, and tuple
values; storage, CLR conversion, registration, and executable operators follow in
separate designs. The initial representation favors explicit contracts over avoiding
boxing, and its slot immutability does not imply deep copying of arbitrary values.

## 1. Problem and Scope

A path may supply only a RID, or a RID plus key/included values. A later join can
combine values from several occurrences of the same relation. The engine needs
one runtime representation that preserves those distinctions without requiring
a CLR relation type for each row or operator.

This revision supersedes the previous recommended structures and implementation
phases in this file. It removes the generic-store migration, per-row masks,
separate provenance collections, placeholder RIDs in plan metadata, and implied
planner/executor implementations.

### Deliver in this feature

- Explicit relation schemas constructible without reflecting over a CLR entity type.
- Separate identities for a registered relation, a bound query occurrence, and a value column.
- Immutable physical layouts containing only available columns, including hidden RIDs.
- A validated tuple value with a private slot array and an explicit ownership contract.
- Unit/component tests and a small set of representation benchmarks.

### Follow-on work

This feature does not implement a canonical-record format or store interface,
heap/B-tree storage, access-path expression recording, relation registration,
CLR schema discovery or codecs, query translation, operators, SQL semantics,
transaction/version management, or propagation. Existing authoring APIs remain
as they are. No removed code or tests are restored.

The contracts in §7 constrain those later integrations. They do not instruct the
coding agent to implement a query engine to validate this feature. In particular,
there is no new `ICanonicalTupleStore`, `TupleCodec`, `RelationRegistry`,
`OutputSignature`, or `MaterializationMask` in this implementation.

## 2. Verified Source Baseline

| Source                                                                            | Current fact                                                                                                                                                                                                 |
|-----------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `PolyStore/Storage/Rid.cs`                                                        | Readonly GUID-v7-backed struct. `new Rid()` generates an identity; `default(Rid)` is zero-initialized. Equality/hashing exist; `ToString()` uses `"N"`. No parsing, public GUID conversion, or ordering API. |
| `PolyStore/Storage/IAccessPath.cs`, `BTreePath.cs`, `HeapPath.cs`                 | Generic authoring declarations. `Configure`, `Column`, and `Include` are no-ops. Heap configuration defaults exist; storage does not.                                                                        |
| `PolyStore/Core/IRelationContext.cs`                                              | Declares `From<T>()`, path-constrained `From<T,TPath>()`, and `FromValues<T>()`. No query provider implementation.                                                                                           |
| `PolyStore/Core/IQueryableExtensions.cs`                                          | DML/Fork extension declarations throw `NotImplementedException`.                                                                                                                                             |
| `PolyStore/Core/IDerived.cs`, `IPropagateChanges.cs`, `IRelationChangeContext.cs` | Definition/propagation interfaces exist. `Change<T>` uses `OldValue`/`NewValue`; `RelationChange<T>` is a separate hierarchy. No engine wiring.                                                              |
| `PolyStore/Hosting/`                                                              | Builder/context stubs and abstract module configuration. No registration/naming-precedence implementation.                                                                                                   |
| `PolyStore/Compiler/`, `PolyStore/Execution/ITransaction.cs`                      | Rewriter marker/list and transaction interface. No IR, physical operators, or transaction implementation.                                                                                                    |
| `PolyStore.Tests`, `PolyStore.IntegrationTests`                                   | Test projects exist but have no C# tests at this baseline.                                                                                                                                                   |
| `PolyStore.Benchmarks`                                                            | BenchmarkDotNet runner and RID creation/equality/hashing workloads remain.                                                                                                                                   |

Canonical stores, heap/B-tree stores, and key-schema primitives are absent.
`HelloWorld` supplies authoring examples, not proof of query execution or
propagation correctness. The old proposal's migration targets and test inventory
therefore no longer apply.

## 3. Core Decisions

| Concern                  | Decision                                                                                                                          |
|--------------------------|-----------------------------------------------------------------------------------------------------------------------------------|
| Runtime currency         | Non-generic `TupleValue`; CLR `Type` describes individual slot values, not an entity type parameter.                              |
| Relation shape           | `RelationSchema`, independent of CLR entity discovery and physical output layout.                                                 |
| Stored relation identity | `RelationIdentity`, an in-process reference token carrying a name and schema.                                                     |
| Query occurrence         | `RelationInstance`, allocated once per bound occurrence and preserved through planning.                                           |
| Value identity           | Shared `TupleColumn` reference, distinct from names and physical ordinals.                                                        |
| Availability             | A column is available exactly when it has a slot in `TupleLayout`. Every row with that layout has every slot.                     |
| Null                     | A slot value may be `null` only when that physical slot is nullable. No missing-value sentinel.                                   |
| Provenance               | A `SourceRid` column associated with one `RelationInstance`; hidden from logical results.                                         |
| Row ownership            | Tuple owns an immutable slot array. Attribute object graphs are borrowed under the stability contract in §6.                      |
| Equality/order           | Metadata identity is reference-based. This feature supplies no relational value equality, hashing, collation, or ordering policy. |

### 3.1 Identity is not a name or a slot

The following objects are sealed reference types without structural equality:
`RelationSchema`, `RelationIdentity`, `RelationInstance`, `TupleColumn`, and
`TupleLayout`. Use reference identity for their maps and comparisons. No global
counter, static registry, string alias, or generated RID is used for these identities.

- A schema describes a shape. Two registered relations may share it.
- A relation identity denotes one registration/storage scope. The future catalog
  must reuse that object for the same registered relation while plans reference it.
- A relation instance denotes one occurrence in a bound query. Two occurrences of
  the same relation receive different objects even when their aliases are equal.
- A column denotes one bound value. Reordering its slot does not change its identity.

Schemas, relation identities, and layouts are immutable after construction.
Their reference identities are process-local and are not persistence identifiers.
Schema evolution, serialization, and plan serialization require separate designs.

### 3.2 Source attributes versus computed values

A source attribute is identified within this runtime by its relation instance
and schema attribute ordinal. `RelationInstance.GetAttributeColumn(i)` always
returns the same `TupleColumn` object for that instance and ordinal. Its RID
column is also created once and reused. Construct these columns eagerly with
the instance so concurrent reads require no lazy cache mutation.

A computed column is a fresh identity created for a bound computed expression
or literal input column. Reuse it when referring to that same bound result;
creating another column with the same name and type creates another identity.
A computed expression does not become recoverable from canonical storage merely
because one of its inputs was a source attribute.

A direct attribute pass-through preserves the original column identity. Logical
output aliases are result-mapping metadata, not new storage provenance. If a
projection outputs the same value twice under different names, both logical
outputs may reference one physical slot. Layouts reject duplicate column identities.

## 4. Metadata Contracts

The following signatures define the required public surface. They are declaration
sketches, not copyable implementations. Use the indicated namespaces/folders and
ordinary private helpers; introduce no service interfaces or external dependencies.

```csharp
// PolyStore/Schema/
namespace PolyStore.Schema;

public readonly record struct AttributeDefinition(
    string Name, Type ValueType, bool IsNullable);

public sealed class RelationSchema
{
    public RelationSchema(IReadOnlyList<AttributeDefinition> attributes);
    public IReadOnlyList<AttributeDefinition> Attributes { get; }
    public int AttributeCount { get; }
    public AttributeDefinition this[int ordinal] { get; }
    public int GetOrdinal(string name);
    public bool TryGetOrdinal(string name, out int ordinal);
}

public sealed class RelationIdentity
{
    public RelationIdentity(string name, RelationSchema schema);
    public string Name { get; }
    public RelationSchema Schema { get; }
}

// PolyStore/Execution/
namespace PolyStore.Execution;

public enum TupleColumnKind
{
    SourceAttribute,
    Computed,
    SourceRid
}

public sealed class RelationInstance
{
    public RelationInstance(RelationIdentity relation, string? alias = null);
    public RelationIdentity Relation { get; }
    public string? Alias { get; }
    public TupleColumn RidColumn { get; }
    public TupleColumn GetAttributeColumn(int attributeOrdinal);
}

public sealed class TupleColumn
{
    // Source columns are created only by RelationInstance.
    public static TupleColumn Computed(
        string name, Type valueType, bool isNullable);
    public TupleColumnKind Kind { get; }
    public string Name { get; }
    public Type ValueType { get; }
    public bool IsNullable { get; }
    public RelationInstance? Source { get; }
    public int? AttributeOrdinal { get; }
}

public readonly record struct TupleSlot(TupleColumn Column, bool IsNullable);

public sealed class TupleLayout
{
    public TupleLayout(IReadOnlyList<TupleSlot> slots);
    public IReadOnlyList<TupleSlot> Slots { get; }
    public int SlotCount { get; }
    public TupleSlot this[int ordinal] { get; }
    public int GetOrdinal(TupleColumn column);
    public bool TryGetOrdinal(TupleColumn column, out int ordinal);
}
```

### 4.1 Schema construction and names

`RelationSchema` copies the supplied attributes in their supplied order; their
positions are schema ordinals `0..AttributeCount-1`. It neither sorts them nor
reflects over a CLR relation type. A future CLR adapter must supply its own
explicit discovery/ordering rule. Empty schemas are valid.

Names must be non-null and not all whitespace. Preserve spelling exactly; use
`StringComparer.Ordinal` for lookup and duplicate detection. Duplicate attribute
names in one schema fail construction. Do not trim, case-fold, or derive names
from `[Relation]` here. `RelationIdentity.Name` is explicitly supplied; distinct
identity objects may have the same diagnostic name. Global registration-name
uniqueness and the builder/attribute precedence rule belong to the catalog design.

An attribute ordinal is meaningful only with its schema. A physical slot ordinal
is meaningful only with its layout. They can differ: relation attribute 17 may
occupy physical slot 1. Neither ordinal is a durable storage address.

### 4.2 Value type descriptors

`ValueType` is a non-null, closed CLR type that can be carried as an `object`.
Reject `void`, by-ref, pointer/function-pointer, by-ref-like, open-generic, and
`Nullable<T>` descriptors. Nullability is expressed separately: describe an
`int?` value as `typeof(int)` with `IsNullable = true`. Accept closed ordinary
reference types, interfaces, arrays, enums, and boxable value types; the ownership
limits in §6 apply to their contents. No serializer or comparer is inferred.

Non-null values must satisfy `ValueType.IsInstanceOfType(value)`. Consequently,
boxed numeric types must match the descriptor's type rules; there is no numeric
conversion, parsing, culture conversion, or coercion. A boxed nullable value
contains either its underlying value or null and follows the same checks.

Metadata validation and type checks may use already supplied `Type` objects.
There is no attribute/member discovery, reflection-based property access, or
delegate compilation per row.

### 4.3 Column invariants

`TupleColumn` has no public instance constructor. Its public computed-column
factory and internal source-column creation enforce the following combinations;
callers cannot supply a kind/source/ordinal combination directly.

| Kind              | Source            | Attribute ordinal                    | Type and intrinsic nullability                     |
|-------------------|-------------------|--------------------------------------|----------------------------------------------------|
| `SourceAttribute` | Required instance | Valid ordinal in its relation schema | Exactly the attribute descriptor.                  |
| `Computed`        | `null`            | `null`                               | Supplied and validated by `Computed(...)`.         |
| `SourceRid`       | Required instance | `null`                               | Exactly `typeof(Rid)`, intrinsically non-nullable. |

Source-attribute names match their schema names. RID column name is `$rid` for
diagnostics; it is the kind and source identity that establish its meaning.
A user attribute named `$rid`, or whose type is `Rid`, remains an ordinary
source attribute. All `SourceRid` columns are hidden from logical result shape,
star expansion, grouping, and distinctness unless a future explicit API says
otherwise. Other physical columns are not automatically logical output columns;
result reconstruction will require an explicit output mapping.

### 4.4 Physical layout validation

A layout snapshots the supplied slot list, preserving order. It permits zero
slots, repeated diagnostic names, and multiple instances of the same relation.
It rejects null columns, duplicate column identities, and nullability narrowing:
a slot cannot be non-nullable when its column is intrinsically nullable.
A non-nullable column may occupy a nullable slot, as required by an outer join.

If a layout contains a nullable RID slot, every `SourceAttribute` slot for that
same instance must also be nullable. This makes a null-extended row representable.
This rule does not apply to computed columns, which have their own expression
semantics. Outer-join execution must null-extend every column from an absent
input, including computed columns; the layout cannot infer a computed expression's
input dependencies.

A layout may contain source attributes without their RID. Those values are
available, but the layout cannot recover further attributes for that source.
It may also contain a RID without any source attributes.

Collection properties must not expose mutable backing arrays/lists, including
through downcasts. Constructor inputs are defensively copied; expose read-only
wrappers over private storage. No process-wide metadata cache is introduced.

## 5. Tuple Value Contract

```csharp
namespace PolyStore.Execution;

public readonly struct TupleValue
{
    public static TupleValue Create(
        TupleLayout layout, ReadOnlySpan<object?> values);
    public bool IsValid { get; }
    public TupleLayout Layout { get; }
    public int Count { get; }
    public object? GetValue(int ordinal);
}
```

A valid value contains two references: its shared `TupleLayout` and its private
`object?[]`. The array length equals `layout.SlotCount`; only physical slots consume
space. `Create` validates the whole row and copies the caller's span into owned
storage. Empty tuples use shared empty storage. The backing array is never
returned, mutated after publication, pooled, or reused for another row.

`default(TupleValue)` is invalid. `IsValid` returns false; `Layout`, `Count`, and
`GetValue` throw `InvalidOperationException`. An explicitly created zero-slot tuple
is valid. Copying a valid tuple shares its layout and immutable slot array safely.

For a valid row, `GetValue(i)` returns the stored object reference or null. The
consumer resolves columns to ordinals from the expected layout before entering
its row loop and ensures incoming rows use that exact layout object. An integer
ordinal carries no identity itself: `GetValue` cannot detect an in-range ordinal
accidentally taken from another layout or a relation schema. Invalid ordinals
throw; there is no string lookup, type conversion, materialization, canonical
lookup, or user code executed by a read.

### 5.1 Row validation

Construction checks:

1. Layout is non-null and the value count matches its slot count exactly.
2. Each null is permitted by its physical slot's nullability.
3. Every non-null value satisfies the slot's column `ValueType` check in §4.2.
4. For each null `SourceRid` slot, all present `SourceAttribute` slots of that
   same instance are null. A non-null source attribute beside an absent RID
   is rejected as inconsistent provenance.

The layout precomputes the slot relationships needed by rule 4. Row validation
is linear in physical width; do not search all slots once per RID.

A non-null RID, including `default(Rid)`, is a present identity value for this
representation. This feature does not declare that value valid in a store and
does not check existence, generate replacement RIDs, or infer relation scope
from GUID bits. The future store boundary owns those checks.

Construction validates representation consistency, not truth about stored data.
It cannot prove that an available attribute actually came from the record named
by a RID; scans and materializers must uphold that producer contract.

### 5.2 Missing, null, and unavailable provenance

| Situation                            | Representation                                       | Consequence                                                                  |
|--------------------------------------|------------------------------------------------------|------------------------------------------------------------------------------|
| Attribute available and non-null     | Attribute column has a slot with a value             | Read directly.                                                               |
| Attribute available and null         | Attribute column has a nullable slot containing null | Read a real null; do not fetch to replace it.                                |
| Attribute not available              | No slot for its column                               | `TryGetOrdinal` returns false; materialization may be required.              |
| Source present and recoverable       | RID column has a slot containing a `Rid`             | Look up using that instance's relation identity and the execution context.   |
| Source absent through null-extension | Nullable RID slot contains null                      | No lookup; later direct attributes from that absent instance become null.    |
| Provenance not carried               | No RID slot for the instance                         | No lookup is possible from this layout; this does not assert source absence. |

There is no `GetValue` call for a missing column: column resolution must establish
an ordinal first. Missing-column lookup fails explicitly instead of reading an
uninitialized slot. Every row on an operator edge must reference the exact declared
layout; changing which columns are available requires an explicit layout transition.

## 6. Ownership, Lifetime, and Concurrency

Slot-array ownership is strict; attribute-object ownership is deliberately separate.
`Create` copies slot references, including references to boxed value types, not
arbitrary object graphs. `GetValue` returns a borrowed value. The tuple is an
immutable container, not a deep snapshot.

The producer must ensure each referenced value remains observably stable while
any tuple containing it is retained. A consumer must not mutate a borrowed value.
For mutable arrays, custom objects, or mutable boxed structs, the producer must
supply an isolated snapshot or transfer exclusive ownership and cease mutation.
Changing the caller's slot array after `Create` is safe; changing a referenced
object is a contract violation and can affect every row sharing that object.

This contract permits dynamic schemas without inventing a universal clone/codec
protocol. Do not claim the runtime can enforce deep immutability of arbitrary CLR
objects. Copying a `TupleValue` is therefore insufficient to capture a transaction's
old image from mutable application data. A later CLR/storage boundary must define
supported values and snapshotting before using rows for transactional change images.

Shared immutable metadata and published tuple slots support concurrent readers
under this value-stability contract. Construction over caller collections/spans
requires the caller not to mutate them during construction. There are no mutable
registries, lazy global caches, concurrent writers, disposables, or buffer leases
in this feature. Its synchronous in-memory operations need no async APIs.

## 7. Contracts for Future Producers and Consumers

These are integration requirements, not new operator implementations in this feature.

### 7.1 Binding and scans

The binder creates a `RelationInstance` once for each relation occurrence and
shares it across paths considered for that occurrence. Choosing a different path
must not change the occurrence's column identities. A self-join or separate
expansion of a reused subquery needs separate instances for its distinct bindings.
A rewrite that creates a new occurrence must explicitly remap affected references;
executing joins never allocate or renumber identities.

A scan produces a fixed layout containing its instance's RID and the attributes
it actually supplies. Included columns and key columns use their source attribute
identities, regardless of storage origin. A values-only input needs no RID; it can
use computed columns with a declared shape. No `FromValues<T>` implementation is
added here.

### 7.2 Pass-through, projection, and joins

| Future operation                         | Required representation behavior                                                                                                                       |
|------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------|
| Filter/sort/limit without column changes | Preserve the layout and values. Required inputs must already be available.                                                                             |
| Physical projection                      | Construct an explicit output layout and slot mapping; preserve column identities for direct copies. Retain RIDs required downstream.                   |
| Computation                              | Add a computed-column identity and a value; all expression inputs must be available first. No inferred canonical provenance for the computed result.   |
| Inner/cross join                         | Combine selected input columns and preserve their existing identities. No runtime renumbering.                                                         |
| Outer join                               | Widen slots on each possibly absent input, including RIDs, to nullable. Emit nulls for that absent input; preserve present-side values and identities. |
| Semi/anti join                           | Preserve only the emitted side's columns/provenance unless a later design explicitly defines more.                                                     |

A physical layout must not contain the same identity twice. If two join inputs
use the same relation-instance object for different logical occurrences, binding
is wrong and must be corrected before execution. Logical duplicate output aliases
can share one physical slot as described in §3.2.

Dropping an RID is legal only after all downstream uses have been accounted for,
including future materialization or mutation targeting. Having all currently
projected attributes available is not sufficient proof that the RID is dead.

### 7.3 Materialization

A future materialization plan is bound to an input layout, a specific instance's
RID slot, an execution context, and a set of missing source-attribute columns.
It resolves the canonical source through `instance.Relation`, not the physical
row layout, a CLR type, an alias string, or RID bits.

The output has a new fixed layout containing the retained input columns plus
requested missing columns. Direct attributes inherit schema nullability, widened
when the RID slot is nullable. An already-present column is read as-is, including
a null value; it is not added again or refreshed implicitly.

For each row:

- A present RID permits lookup in the correct transaction/version context.
- A null RID means the instance is absent: fill the new nullable attribute slots
  with null and perform no lookup.
- A missing RID slot is an unsatisfied plan requirement, not a runtime null.
- A lookup that cannot satisfy a present RID under its promised visibility
  contract must report failure; it must not silently drop the row or fabricate nulls.

Copy retained slots and all fetched attributes in one output construction, rather
than repeatedly copying the row once per attribute. Materialization may follow
joins or selective filters when dependencies allow; it must precede any operation
that needs a missing input. A cost model decides placement later.

This does not require complete-record fetches, individual-attribute reads, a
specific missing-record exception, or `Dictionary<Rid, TupleValue>`. Those are
storage/executor choices. A canonical record does not have to use this row layout.

### 7.4 Aggregation and set operations

Hidden RIDs must not silently participate in user-visible equality, grouping,
or duplicate elimination. Never union branch layouts/provenance and assume each
emitted row now supplies both branches' values.

The conservative future baseline is:

- Aggregation produces new computed columns for keys/results and no source RIDs.
  Even a copied grouping key gets a new output identity if it no longer denotes
  one source row's recoverable attribute.
- `UNION ALL` normalizes both branches to the same output columns/types/nullability.
  Any visible values needed after it are available at that boundary. Its baseline
  output has new column identities and no canonical provenance.
- Distinct union, intersection, difference, and projection distinctness must
  establish required comparison values before duplicate/group processing and
  emit normalized output without choosing an arbitrary representative RID for
  future lookup.

No equality/comparer or set operator is implemented here. A later design can
preserve additional provenance when it proves a sound identity rule, including
branch selection and outer absence. That optimization cannot be assumed by an
implementation of these tuple primitives.

## 8. Worked Representation Example

For illustration, construct a schema explicitly with `Id: long` and nullable
`Name: string`. Create one relation identity `Employees` and two instances,
`employee` and `manager`. This is a test fixture, not a change to `HelloWorld`.

An outer self-join may produce this physical layout:

| Slot | Column                           | Physical type | Nullable | Role                                |
|------|----------------------------------|---------------|----------|-------------------------------------|
| 0    | `employee.RidColumn`             | `Rid`         | No       | Hidden employee identity            |
| 1    | `employee.GetAttributeColumn(0)` | `long`        | No       | Employee Id                         |
| 2    | `manager.RidColumn`              | `Rid`         | Yes      | Hidden optional manager identity    |
| 3    | `manager.GetAttributeColumn(0)`  | `long`        | Yes      | Manager Id, null-extended if absent |

`manager.Name` has no slot. It is unavailable, not a null already read from storage.

For a matched row, slot 2 contains the manager RID. Later materialization can add
`manager.GetAttributeColumn(1)` in slot 4 and use that manager's canonical record.
For an unmatched row, slots 2 and 3 are null; materialization adds slot 4 as null
without lookup. Both outputs share the same five-slot layout.

The employee and manager RID columns are distinct even if both values happen to
contain the same `Rid`, and both instances point to the same relation identity.
Changing slot order preserves these associations because column identity is
independent of position. No schema-wide mask or per-row provenance collection
is needed.

## 9. Failure Contract

Use standard exceptions. Diagnostics should identify the argument, column/slot,
and expected type where relevant, without embedding row payload values.

| Failure                                                                                                                                                               | Result                        |
|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------|-------------------------------|
| Null required reference argument, including a lookup name/column                                                                                                      | `ArgumentNullException`       |
| Whitespace/invalid name, invalid type descriptor, duplicate attribute name or layout identity, nullability narrowing, invalid default metadata in a constructor input | `ArgumentException`           |
| Invalid schema/slot ordinal                                                                                                                                           | `ArgumentOutOfRangeException` |
| Unknown name/column in `GetOrdinal`                                                                                                                                   | `KeyNotFoundException`        |
| Unknown name/column in `TryGetOrdinal`                                                                                                                                | `false`, output ordinal `-1`  |
| Row width mismatch, wrong value type, forbidden null, or non-null source attribute beside null RID                                                                    | `ArgumentException`           |
| Accessing invalid/default tuple except `IsValid`                                                                                                                      | `InvalidOperationException`   |

`alias: null` is allowed; a supplied whitespace alias is rejected. Metadata
collections themselves cannot be null. Empty schema/layout/value collections are
valid. A default `AttributeDefinition` or `TupleSlot` supplied inside a collection
is invalid and rejected with `ArgumentException` by its containing constructor.
Other invalid attribute descriptors inside a schema collection also produce
`ArgumentException`. A null required argument to a public factory, constructor,
or lookup produces `ArgumentNullException`. Name lookups reject whitespace names
with `ArgumentException`; well-formed but absent names return false or throw
`KeyNotFoundException` according to the lookup method.
Failure must not publish a partially constructed object or change caller inputs.

Do not add relational equality/comparison operators to `TupleValue`. Its inherited
CLR object/value-type equality is not relational equality and must not be used
by future join, distinct, grouping, or index code. Those consumers need an
explicit comparison contract consistent across access paths.

## 10. Alternatives and Performance Boundaries

| Approach                                               | Assessment                                                                                                                                                                        |
|--------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Compact physical layout with hidden RID slots          | Selected. One availability contract per edge; no unused relation-width slots; provenance follows ordinary slot movement. RID/value boxing remains.                                |
| Full-relation array, per-row mask, separate provenance | Superseded. Duplicates uniform availability in rows and requires separate provenance composition. Could serve a different heterogeneous-row contract, which is not required here. |
| Typed column batches from the outset                   | Deferred. Requires batch ownership/validity and executor choices beyond this feature. The column identities and layout metadata can be reused by a later batch design.            |

The tuple handle contains two references; that is not a claim that copying it
is free. Creating a nonempty row allocates its owned array, and producing boxed
value types can allocate additional objects. A RID-only row still has one slot
and can incur RID boxing. The old zero-allocation RID-scan claim does not apply.

`Create` copies an input buffer. Any caller-created temporary array is an
additional cost. Do not add an unchecked ownership-taking constructor, pooling,
inline RID specialization, or custom scalar union in this feature. Measure the
simple representation before changing that contract. Copying an already-boxed
reference does not box it again.

Metadata and identity maps are constructed once and shared. Layout/schema
construction is outside normal per-row work. `GetValue` is constant-time;
construction/validation is linear in physical width. The representation imposes
no 64-column limit and does not allocate slots for missing relation attributes.

Benchmarks should cover prebuilt layouts with RID-only, covered-subset, and
multi-source rows; construction and repeated ordinal reads; and allocation cost
with values preboxed versus boxed by the producer. Preboxed cases isolate the
container and must not be reported as total scan cost. Include a wide logical
schema with a narrow physical output to verify the intended cost model.
There is no performance threshold or claim of superiority over LINQ in this design.

## 11. Validation and Implementation Handoff

### 11.1 Semantic acceptance criteria

| Area               | Required cases                                                                                                                                                                         |
|--------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Schema             | Explicit ordering retained; exact ordinal name lookup; duplicate names rejected; empty schema accepted; input mutation cannot change published metadata.                               |
| Type descriptors   | Underlying-type-plus-nullability convention; reject unboxable/open/nullable descriptors; valid reference/interface/value types; no numeric coercion.                                   |
| Identities         | Same instance/attribute returns the same column; two instances of one relation stay distinct; equal aliases/names do not merge identities; two relation identities may share a schema. |
| Layout             | Reordered columns keep identity; missing column returns false/-1; duplicate identity rejected; duplicate diagnostic names accepted; physical nullability can widen but not narrow.     |
| Tuple              | Width/type/null checks; ordinary reads; caller slot-array mutation cannot affect stored slots; copies retain values; default invalid versus valid empty tuple.                         |
| Value ownership    | Arbitrary payload references are preserved rather than cloned; document/test container ownership without claiming object-graph snapshotting.                                           |
| Provenance         | RID-only layout; two RIDs for a self-join; a user `Rid` attribute is not provenance; missing RID slot distinct from null RID; default-valued RID remains a present token.              |
| Outer absence      | Nullable RID requires nullable source-attribute slots; null RID plus non-null source attribute rejected; nullable computed columns follow their own value rule.                        |
| Width and sparsity | More than 64 logical attributes; narrow output; identity lookup distinguishes missing columns even when their schema ordinal happens to be a valid physical slot for another column.   |
| Composition        | Manually construct joined/projected/expanded rows with reused column identities; verify the self-join example, RID preservation/drop, and fixed layout across matched/unmatched rows.  |

For composition tests, construct rows directly through the public API. Do not
create fake store/executor interfaces or implement a materializer merely for these
tests. Use new fixtures; there are no surviving storage tests to migrate.

Put primitive and composition tests in `PolyStore.Tests/Schema` and
`PolyStore.Tests/Execution`. They test this component's contracts. Add integration
coverage when an actual access-path/store/compiler/executor boundary is implemented,
not by labeling a hand-assembled tuple test an engine integration test.

### 11.2 Implementation sequence

1. **Schemas and relation identity:** `AttributeDefinition`, `RelationSchema`,
   `RelationIdentity`, and validation tests.
2. **Bound columns:** `RelationInstance`, `TupleColumnKind`, `TupleColumn`;
   canonical per-instance columns and self-join identity tests.
3. **Physical layouts:** `TupleSlot`, `TupleLayout`, immutable lookup maps and
   precomputed nullable-RID/source-attribute relationships.
4. **Tuple values:** private slot storage, construction/read/default behavior,
   ownership checks, and manually composed layout-transition tests.
5. **Performance baseline and documentation:** add the scoped tuple benchmarks
   under `PolyStore.Benchmarks/Benchmarks/Execution`; update its workload inventory
   for the new workloads and record current-state architecture accurately.

Follow repository naming, nullable, formatting, and xUnit conventions. No new
package is required. Run solution formatting/build/tests with .NET 10 and execute
the scoped benchmarks in Release. Report discovered test counts and any baseline
or SDK-resolution failure; do not claim success from empty or unexecuted tests.

### 11.3 Definition of implementation readiness

The primitives' constructors, identity rules, value validation, null/absence
behavior, ownership, public surface, errors, and acceptance criteria are specified
here. No store, codec, comparison policy, or operator must be invented to implement
them. Human approval accepts these proposed decisions; the coding handoff then
implements this scope without reopening or silently expanding it.

If implementation uncovers a contradiction, escalate the concrete conflict under
`AGENTS.md`. Do not fill it by restoring an API from an earlier revision.

## 12. Follow-On Design Boundaries

| Next design                    | Decisions it must supply before its implementation                                                                                                                                                          |
|--------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Canonical records/storage      | Record representation, relation/RID ownership, default RID validity, lookup granularity, snapshot visibility, missing-record handling, mutation and persistence contracts.                                  |
| CLR schema/codec/registration  | Property discovery/order, inherited members, builder/attribute naming precedence, supported mutable values and snapshotting, constructor-based reconstruction for immutable types, and projection mappings. |
| Access-path descriptions       | Recording/validating `Column` and `Include`, key comparison and collation, path identity/registration, and available-column descriptions.                                                                   |
| Translation/planning/execution | Query binding, column remapping, actual operator interfaces, materialization planning, comparison/null semantics, set/aggregate behavior, async I/O, and row/batch choices.                                 |
| Transactions/propagation       | Old/new image capture, lifetime of mutable values, mutation targets, version visibility, dependency ordering, atomicity, and recovery.                                                                      |

These features may consume the foundation but are not partly implemented by it.
After this proposal is approved, the architecture owner should record the chosen
compact-layout/hidden-RID contract as accepted but unimplemented, replace the old
mask/provenance assumptions where referenced, and mark implementation status only
when code lands. No separate store or CLR-codec decision is implied by approval.
