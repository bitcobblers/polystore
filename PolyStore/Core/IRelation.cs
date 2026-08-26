namespace PolyStore.Core;

/// <summary>
/// Represents a generic relation of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the relation.</typeparam>
/// <remarks>
/// A relation is anything that can participate in a relational expression.
/// Relations are built from sources or by chaining other relations together.
/// </remarks>
public interface IRelation<in T>
{
    IQueryable<int> Insert(T item);
}
