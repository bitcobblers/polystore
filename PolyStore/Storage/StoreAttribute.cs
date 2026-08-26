using System;

namespace PolyStore.Storage;

/// <summary>
/// Defines a storage configurable.
/// </summary>
/// <param name="Name">The optional name for the storage type.</param>
[AttributeUsage(AttributeTargets.Class)]
public class StoreAttribute(
#pragma warning disable CS9113 // Parameter is unread.
    string? Name = null,
    StorageType Storage = StorageType.Row) : Attribute;
#pragma warning restore CS9113 // Parameter is unread.
