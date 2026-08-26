namespace PolyStore.Storage;

/// <summary>
/// Defines the storage strategy for a type.
/// </summary>
public enum StorageType
{
    /// <summary>
    /// Use row storage for the type.
    /// </summary>
    Row,

    /// <summary>
    /// Use column storage for the type.
    /// </summary>
    Column
}