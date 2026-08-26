namespace PolyStore.Hosting;

/// <summary>
/// Defines a configurable database module.
/// </summary>
public abstract class DatabaseModule
{
    /// <summary>
    /// Configures the module.
    /// </summary>
    /// <param name="db">The database to build.</param>
    public abstract void Configure(DatabaseBuilder db);
}
