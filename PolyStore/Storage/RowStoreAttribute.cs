using System;

namespace PolyStore.Storage;

/// <summary>
/// Marks a relation as using row-based storage.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class RowStoreAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the default page size to use.
    /// </summary>
    public int PageSize { get; set; } = 8192;

    /// <summary>
    /// Gets or sets the default fill factor for a page.
    /// </summary>
    public double FillFactor { get; set; } = 0.90;
}
