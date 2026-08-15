namespace PolyStore.Core;

/// <summary>
/// Represents a basic relation with a name.
/// </summary>
/// <remarks>
/// A relation is anything that can participate in a relational expression.
///
/// Relations are built from sources or by chaining other relations together.
/// </remarks>
public interface IRelation
{
    /// <summary>
    /// Gets the name of the relation.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the row type.
    /// </summary>
    Type RowType { get; }

    /// <summary>
    /// Gets the expression for the relation.
    /// </summary>
    RelationExpression Expression { get; }
}

/// <summary>
/// Represents a generic relation of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the relation.</typeparam>
public interface IRelation<out T> : IRelation;
