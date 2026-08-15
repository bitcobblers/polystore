using System.Linq.Expressions;
using PolyStore.Storage;

namespace PolyStore.Core;

public static class RelationExtensions
{
    extension<T>(IRelation<T> source)
    {
        /// <summary>
        /// Sets a realization for the relation.
        /// </summary>
        /// <typeparam name="TProvider">The realization provider to use.</typeparam>
        /// <returns>The current relation.</returns>
        public IRelation<T> Realize<TProvider>()
            where TProvider : IStorageProvider, new()
            => source;

        public IRelation<T> Filter(Expression<Func<T, bool>> predicate) => new Relation<T>(
            $"{source.Name}.Filter",
            new FilterExpression(
                source.Expression,
                predicate));

        public IRelation<TResult> Project<TResult>(Expression<Func<T, TResult>> projection) where TResult : class
            => new Relation<TResult>(
                $"{source.Name}.project",
                new ProjectExpression(
                    source.Expression,
                    projection,
                    typeof(TResult)));

        public IRelation<TResult> Join<TRight, TKey, TResult>(IRelation<TRight> right,
            Expression<Func<T, TKey>> leftKey,
            Expression<Func<TRight, TKey>> rightKey,
            Expression<Func<T, TRight, TResult>> projection) where TRight : class
            => new Relation<TResult>(
                $"{source.Name}.join.{right.Name}",
                new JoinExpression(
                    source.Expression,
                    right.Expression,
                    leftKey,
                    rightKey,
                    projection,
                    typeof(TResult)));
    }
}