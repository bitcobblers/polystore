namespace PolyStore.Core;

/// <summary>
/// Represents a single instance of a relation containing a value of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the value held by this relation. Must be a reference type.</typeparam>
public class Relation<T> : IRelation<T>
{
    /// <inheritdoc />
    public IQueryable<int> Insert(T item)
    {
        throw new NotImplementedException();
    }
}