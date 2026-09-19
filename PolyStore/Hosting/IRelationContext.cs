using System.Linq;

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
    /// Gets a relation as a collection of values.
    /// </summary>
    /// <param name="values">The values to create the relation from.</param>
    /// <typeparam name="T">The relation type to create.</typeparam>
    /// <returns>A queryable for the values collection.</returns>
    IQueryable<T> FromValues<T>(params object[] values);
}
