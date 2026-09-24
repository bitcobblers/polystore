using System.Linq;

namespace PolyStore.Core;

/// <summary>
/// Defines the context for handling changes in a relation.
/// </summary>
public interface IRelationChangeContext : IRelationContext
{
    /// <summary>
    /// Gets the inserts as a queryable collection.
    /// </summary>
    /// <typeparam name="T">The relation type.</typeparam>
    /// <returns>A queryable representing the inserts.</returns>
    IQueryable<T> Inserts<T>();

    /// <summary>
    /// Gets the updates as a queryable collection.
    /// </summary>
    /// <typeparam name="T">The relation type.</typeparam>
    /// <returns>A queryable representing the updates.</returns>
    IQueryable<Change<T>> Updates<T>();

    /// <summary>
    /// Gets the deletes as a queryable collection.
    /// </summary>
    /// <typeparam name="T">The relation type.</typeparam>
    /// <returns>A queryable representing the updates.</returns>
    IQueryable<T> Deletes<T>();

    /// <summary>
    /// Combines a collection of queryables into a single collection.
    /// </summary>
    /// <param name="paths">The queryables to combine.</param>
    /// <typeparam name="T">The relation type.</typeparam>
    /// <returns>A queryable representing the combined relations.</returns>
    IQueryable<T> Combine<T>(params IQueryable<T>[] paths);
}
