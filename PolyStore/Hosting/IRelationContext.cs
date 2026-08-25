using PolyStore.Core;

namespace PolyStore.Hosting;

/// <summary>
/// Defines the context for a single transaction.
/// </summary>
public interface IRelationContext
{
    /// <summary>
    /// Gets a relation as a queryable collection.
    /// </summary>
    /// <typeparam name="T">The relation type to get.</typeparam>
    /// <returns>A queryable for the relation.</returns>
    IQueryable<T> From<T>();

    /// <summary>
    /// Gets the underlying relation definition.
    /// </summary>
    /// <typeparam name="T">the relation type to get/</typeparam>
    /// <returns>The relation definition.</returns>
    IRelation<T> Get<T>();

    /// <summary>
    /// Inserts a single value into a relation.
    /// </summary>
    /// <param name="item">The item to insert.</param>
    /// <typeparam name="T">The relation type to update.</typeparam>
    /// <returns>A queryable representing the number of records inserted.</returns>
    [Obsolete("This method will be replaced with context short-hands.")]
    IQueryable<int> Insert<T>(T item);
}