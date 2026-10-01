namespace PolyStore.Benchmarks.Support;

/// <summary>
/// The dataset-size matrix for storage benchmarks. Kept deliberately short so the
/// benchmark matrix stays manageable (D4). Add a size only when a benchmark needs it.
/// </summary>
public static class DatasetSizes
{
    /// <summary>Tiny: one thousand rows. The heap's default floor (O(n²) setup is cheap here).</summary>
    public const int Tiny = 1_000;

    /// <summary>Small: ten thousand rows. The heap's default top and the canonical store's floor.</summary>
    public const int Small = 10_000;

    /// <summary>
    /// Medium: one hundred thousand rows. The canonical store's default top. The O(n)-setup
    /// canonical store builds this in O(n); the O(n²)-setup heap does not use this size.
    /// </summary>
    public const int Medium = 100_000;

    /// <summary>
    /// Large: one million rows. Opt-in deep runs only, for the O(n)-setup canonical store.
    /// The O(n²)-setup heap never offers this (a 1M heap build is ~5×10¹¹ comparisons).
    /// </summary>
    public const int Large = 1_000_000;
}
