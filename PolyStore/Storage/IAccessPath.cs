namespace PolyStore.Storage;

/// <summary>
/// Defines an access path into the relation.
/// </summary>
/// <typeparam name="T">The relation type.</typeparam>
public interface IAccessPath<T>
{
    /// <summary>
    /// Configures the path.
    /// </summary>
    void Configure();
}
