namespace PolyStore.Core;

/// <summary>
/// Defines a change to the relation.
/// </summary>
/// <typeparam name="T">The type of relation that was changed.</typeparam>
public abstract record RelationChange<T>;

/// <summary>
/// Defines an insert change.
/// </summary>
/// <param name="Value">The inserted value.</param>
/// <typeparam name="T">The relation type.</typeparam>
public sealed record Insert<T>(T Value) : RelationChange<T>;

/// <summary>
/// Defines a delete change.
/// </summary>
/// <param name="Value">The value that was deleted.</param>
/// <typeparam name="T">The relation type.</typeparam>
public sealed record Delete<T>(T Value) : RelationChange<T>;

/// <summary>
/// Defines an update change.
/// </summary>
/// <param name="Before">The original value.</param>
/// <param name="After">The new value.</param>
/// <typeparam name="T">The relation type.</typeparam>
public sealed record Update<T>(T Before, T After) : RelationChange<T>;