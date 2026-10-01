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
