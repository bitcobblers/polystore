using System.Linq;

namespace PolyStore.Core;

/// <summary>
/// Defines a propagator of changes.
/// </summary>
/// <typeparam name="T">The relation type.</typeparam>
public interface IDerived<out T>
{
    /// <summary>
    /// Defines the query used to initialize the relation.
    /// </summary>
    /// <param name="context">The context to use.</param>
    /// <returns>A queryable used to derive the relation.</returns>
    public IQueryable<T> Define(IRelationContext context);
}
