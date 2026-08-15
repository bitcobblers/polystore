using PolyStore.Core;

namespace PolyStore.Compiler;

/// <summary>
/// Defines a rewriter of relation expressions.
/// </summary>
public interface IRelationRewriter
{
    /// <summary>
    /// Rewrites the relation.
    /// </summary>
    /// <param name="expression">The expression to rewrite.</param>
    /// <returns>The rewritten expression.</returns>
    RelationExpression Rewrite(RelationExpression expression);
}