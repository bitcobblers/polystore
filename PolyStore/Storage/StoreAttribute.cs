namespace PolyStore.Storage;

/// <summary>
/// Defines a storage configurable.
/// </summary>
/// <param name="Name">The optional name for the storage type.</param>
[AttributeUsage(AttributeTargets.Class)]
public class StoreAttribute(
    string? Name = null,
    StorageType Storage = StorageType.Row) : Attribute;
