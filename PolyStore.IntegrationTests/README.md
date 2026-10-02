# PolyStore.IntegrationTests

Integration tests for PolyStore. They verify **observable behavior that emerges from
interaction across an architectural boundary** — behavior that would not exist if either
side of the boundary were removed. The initial set covers the one boundary implemented
today: the access-path ↔ canonical-store RID bridge
(`Storage/HeapCanonicalStoreBridgeTests`).

This is a foundation, not a test platform. It has no base class, no fixture framework,
no test-data generators, and no shared test-support assembly. xunit + plain C# is the
entire harness.

The design and the authoritative unit/integration boundary definition live in
[`docs/design/integration-testing.md`](../docs/design/integration-testing.md).

## Boundary rule

A test belongs in this project when **both** conditions hold:

1. Its assertion depends on behavior that emerges only when two components on either
   side of an architectural boundary interact.
2. The behavior under test is implemented (not a `NotImplementedException` stub).

Tests that verify a single component in isolation belong in `PolyStore.Tests`.
The full decision procedure and conventions are defined in
[`docs/design/integration-testing.md`](../docs/design/integration-testing.md) §8.1–§8.3.

## Conventions

- **Class naming.** Boundary tests: `<ComponentA><ComponentB>BridgeTests`.
  Scenario tests: `<Scenario>Tests`. One class per boundary or scenario.
- **Method naming.** `<interaction>_<expected outcome>`, consistent with the existing
  `Method_Condition_ExpectedBehavior` style.
- **AAA.** Arrange / Act / Assert grouped by blank lines.
- **Namespace mirrors folder.** `PolyStore.IntegrationTests.Storage` for
  `Storage/HeapCanonicalStoreBridgeTests.cs`.
- **Fixtures.** Local `private record` in the test class. Components constructed in
  the test method via public constructors. No shared fixtures.
- **Determinism.** Values derived from literals or indices; no wall-clock, no
  unseeded randomness.
- **Public surface only.** Tests observe behavior through public types and members;
  no reflection into internals.

## Running

```bash
# Run all tests (both test projects).
dotnet test

# Run only the integration tests.
dotnet test PolyStore.IntegrationTests

# Run one class.
dotnet test --filter "FullyQualifiedName~HeapCanonicalStoreBridgeTests"
```

## Adding a test

1. **Confirm the boundary.** Apply the two-step decision procedure in
   `docs/design/integration-testing.md` §8.1. If the test crosses no architectural
   boundary, it belongs in `PolyStore.Tests`.
2. **Confirm it is implemented.** The behavior must not be a `NotImplementedException`
   stub or an unimplemented interface.
3. **Pick the folder.** The subsystem that is the *entry point* of the scenario
   (the topmost component the test starts from). Storage-bridge tests → `Storage/`.
   A future test starting from `ITransaction` → `Execution/`.
4. **Add the class** named per the conventions above, in the matching namespace.
5. **Write the test** with AAA structure, local fixtures, and public-surface-only
   assertions.
6. **Run it:** `dotnet test --filter "FullyQualifiedName~<ClassName>"`.

## Layout

```
PolyStore.IntegrationTests/
├── PolyStore.IntegrationTests.csproj
├── README.md
└── Storage/
    └── HeapCanonicalStoreBridgeTests.cs
```

Folders are created when the first test for that boundary lands. No reserved empty
folders; no tests of unimplemented behavior.
