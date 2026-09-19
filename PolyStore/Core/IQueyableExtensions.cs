using System;
using System.Linq;
using System.Linq.Expressions;

namespace PolyStore.Core;

/// <summary>
/// Defines common DML extensions for queryables.
/// </summary>
public static class IQueyableExtensions
{
    /// <summary>
    /// Defines extensions for <see cref="IQueryable"/>
    /// </summary>
    /// <param name="source">The source queryable.</param>
    /// <typeparam name="T">The queryable type.</typeparam>
    extension<T>(IQueryable<T> source)
    {
        /// <summary>
        /// Builds an update expression.
        /// </summary>
        /// <param name="update">The update clause to apply</param>
        /// <typeparam name="TResult">An anonymous object describing the changes to the object.</typeparam>
        /// <returns>The updated queryable.</returns>
        public IQueryable<T> Update<TResult>(Expression<Func<T, TResult>> update)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Builds an update expression.
        /// </summary>
        /// <param name="target">The target relation to update.</param>
        /// <param name="update">The update clause to apply.</param>
        /// <typeparam name="TTarget">The type representing the type to update.</typeparam>
        /// <typeparam name="TUpdate">An anonymous object describing the changes to the object.</typeparam>
        /// <returns>The source queryable.</returns>
        public IQueryable<T> Update<TTarget, TUpdate>(
            Expression<Func<T, TTarget>> target,
            Expression<Func<T, TUpdate>> update)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Inserts a collection of objects into the relation.
        /// </summary>
        /// <param name="insert">The insert handler to apply.</param>
        /// <typeparam name="TInsert">The source record being inserted.</typeparam>
        /// <returns>The source queryable.</returns>
        public IQueryable<T> Insert<TInsert>(Expression<Func<T, TInsert>> insert)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Inserts a collection of objects into the relation.
        /// </summary>
        /// <returns>The source queryable.</returns>
        public IQueryable<T> Insert()
        {
            throw new NotImplementedException();
        }
    }
}
