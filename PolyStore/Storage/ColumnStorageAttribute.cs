using System;

namespace PolyStore.Storage;

/// <summary>
/// Marks a relation as using column storage.
/// </summary>
public class ColumnStorageAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the row group size to use.
    /// </summary>
    public int RowGroupSize { get; set; } = 64;

    /// <summary>
    /// Gets or sets the compression algorithm to use.
    /// </summary>
    public CompressionType Compression { get; set; } = CompressionType.None;

    /// <summary>
    /// Gets or sets the default compression level.
    /// </summary>
    public int CompressionLevel { get; set; } = 5;
}
