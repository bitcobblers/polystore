using PolyStore.Core;

namespace PolyStore.Compiler;

/// <summary>
/// Defines a pipeline for optimizing relation expressions.
/// </summary>
public sealed class OptimizationPipeline
{
    private readonly List<IRelationRewriter> _optimizers = [];

    /// <summary>
    /// Adds a new optimizer to the pipeline.
    /// </summary>
    /// <typeparam name="T">The type of optimizer to add.</typeparam>
    /// <returns>The current pipeline.</returns>
    public OptimizationPipeline Add<T>()
        where T : IRelationRewriter, new()
    {
        _optimizers.Add(new T());
        return this;
    }

    /// <summary>
    /// Runs the optimization pipeline
    /// </summary>
    /// <param name="expression">The expression to optimize.</param>
    /// <returns>The optimized expression.</returns>
    public RelationExpression Optimize(RelationExpression expression)
        => _optimizers
            .Aggregate(
                expression,
                (current, optimizer) => optimizer.Rewrite(current));
}