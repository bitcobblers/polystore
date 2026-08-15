namespace PolyStore.Hosting;

/// <summary>
/// Defines a configurable database module.
/// </summary>
public abstract class DatabaseModule<TSchema>
    where TSchema : DatabaseSchema
{
    /// <summary>
    /// Configures the module.
    /// </summary>
    /// <param name="db">The database to build.</param>
    public abstract TSchema Configure(DatabaseBuilder db);
}