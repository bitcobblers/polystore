using System.Linq.Expressions;

namespace PolyStore.Core;

public abstract record RelationExpression
{
    /// <summary>
    /// Gets the row type for the expression.
    /// </summary>
    public abstract Type RowType { get; }
}

public sealed record SourceExpression(string Name, Type Type) : RelationExpression
{
    /// <inheritdoc />
    public override Type RowType => Type;
}

public sealed record FilterExpression(RelationExpression Source, LambdaExpression Predicate) 
    : RelationExpression
{
    /// <inheritdoc />
    public override Type RowType => Source.RowType;
}

public sealed record ProjectExpression(RelationExpression Source, LambdaExpression Projection, Type Type)
    : RelationExpression
{
    /// <inheritdoc />
    public override Type RowType => Type;
}

public sealed record JoinExpression(
    RelationExpression Left,
    RelationExpression Right,
    LambdaExpression LeftKey,
    LambdaExpression RightKey,
    LambdaExpression Projection,
    Type Type) : RelationExpression
{
    /// <inheritdoc />
    public override Type RowType => Type;
}