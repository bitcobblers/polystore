namespace PolyStore.Storage;

/// <summary>
/// Flags describing the capabilities supported by a relation accessor.
/// </summary>
[Flags]
public enum RelationAccessorCapabilities
{
    /// <summary>
    /// No capabilities are supported.
    /// </summary>
    None = 0,

    /// <summary>
    /// The accessor supports full sequential scans over the relation.
    /// </summary>
    Scan = 0x01,

    /// <summary>
    /// The accessor supports point lookups by key (seek operations).
    /// </summary>
    Seek = 0x02,

    /// <summary>
    /// The accessor returns rows in a defined, stable sort order.
    /// </summary>
    Ordered = 0x04,

    /// <summary>
    /// Data accessed through this accessor is durable and persisted to stable storage.
    /// </summary>
    Durable = 0x08,

    /// <summary>
    /// The accessor supports write operations (insert, update, delete).
    /// </summary>
    Writable = 0x10,

    /// <summary>
    /// The accessor exposes data in a column-oriented layout rather than row-oriented.
    /// </summary>
    Columnar = 0x20
}
