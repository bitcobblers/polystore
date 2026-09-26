namespace PolyStore.Storage;

/// <summary>
/// Defines the canonical tuple store for a relation of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The tuple type.</typeparam>
/// <remarks>
/// The canonical store owns the authoritative tuple representation. It maps
/// RIDs to complete tuples. Access paths do not replace this representation;
/// they provide alternative ways of locating RIDs and, optionally, obtaining
/// some tuple data without accessing the canonical representation.
/// </remarks>
public interface ICanonicalTupleStore<T>
{
    /// <summary>
    /// Gets the number of tuples in the store.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Inserts a tuple into the store and returns its RID.
    /// </summary>
    /// <param name="value">The tuple to insert.</param>
    /// <returns>The RID assigned to the tuple.</returns>
    Rid Insert(T value);

    /// <summary>
    /// Attempts to retrieve a tuple by its RID.
    /// </summary>
    /// <param name="rid">The RID of the tuple.</param>
    /// <param name="value">The retrieved tuple, or <c>default</c> if not found.</param>
    /// <returns><c>true</c> if the tuple was found; otherwise, <c>false</c>.</returns>
    bool TryGet(Rid rid, out T value);

    /// <summary>
    /// Deletes a tuple from the store by its RID.
    /// </summary>
    /// <param name="rid">The RID of the tuple to delete.</param>
    void Delete(Rid rid);
}
