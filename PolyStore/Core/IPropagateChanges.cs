using System.Linq;

namespace PolyStore.Core;

/// <summary>
/// Defines a derived relation that supports propagation.
/// </summary>
/// <typeparam name="T">The relation type.</typeparam>
public interface IPropagateChanges<out T> : IDerived<T>
{
    /// <summary>
    /// Defines the query used to propagate changes to the relation.
    /// </summary>
    /// <param name="context">The context to use.</param>
    /// <returns>A queryable used to derive the changes to apply.</returns>
    public IQueryable<T> Propagate(IRelationChangeContext context);
}
