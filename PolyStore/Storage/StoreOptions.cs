namespace PolyStore.Storage;

/// <summary>
/// Defines generic options for a realization.
/// </summary>
public abstract record StoreOptions;

/// <summary>
/// Defines options for a row-store realization.
/// </summary>
public sealed record RowStoreOptions : StoreOptions
{
    /// <summary>
    /// Gets or sets the page size for rows.
    /// </summary>
    public int? PageSize { get; init; }
}

/// <summary>
/// Defines options for a column-store realization.
/// </summary>
public sealed record ColumnStoreOptions : StoreOptions
{
    /// <summary>
    /// Gets or sets the group size for columns.
    /// </summary>
    public int? RowGroupSize { get; init; }
}
