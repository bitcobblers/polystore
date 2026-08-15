namespace PolyStore.Core;

/// <summary>
/// Represents a single instance of a relation containing a value of type <typeparamref name="T"/>.
/// </summary>
/// <param name="name">The name of the relation.</param>
/// <param name="expression">The expression defining the relation.</param>
/// <typeparam name="T">The type of the value held by this relation. Must be a reference type.</typeparam>
public class Relation<T>(string name, RelationExpression expression) : IRelation<T> 
{
    /// <inheritdoc />
    public string Name => name;

    /// <inheritdoc />
    public Type RowType => typeof(T);

    /// <inheritdoc />
    public RelationExpression Expression => expression;
}