using PolyStore.Core;

namespace PolyStore.Hosting;

/// <summary>
/// Defines a builder for the database.
/// </summary>
public sealed class DatabaseBuilder
{
    /// <summary>
    /// Defines a new source.
    /// </summary>
    /// <param name="name">The name of the source.</param>
    /// <typeparam name="T">The type representing the source.</typeparam>
    /// <returns>A new source.</returns>
    public ISource<T> Source<T>(string name)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Defines a new relation.
    /// </summary>
    /// <param name="name">The name of the relation.</param>
    /// <param name="factory">The factory used to create the relation.</param>
    /// <typeparam name="T">The type representing the relation.</typeparam>
    /// <returns>A new relation.</returns>
    public IRelation<T> Relation<T>(string name, Func<DatabaseContext, IRelation<T>> factory)
    {
        throw new NotImplementedException();
    }
}
