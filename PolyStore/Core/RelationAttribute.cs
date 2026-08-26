using System;

namespace PolyStore.Core;

/// <summary>
/// Marks a type as being a relation.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class RelationAttribute : Attribute;
