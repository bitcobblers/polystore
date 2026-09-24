using System;

namespace PolyStore.Core;

/// <summary>
/// Marks a type as being a relation.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class RelationAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the name of the relation.
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
