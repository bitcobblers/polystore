using System;
using System.Linq;

namespace PolyStore.Hosting;

/// <summary>
/// Defines the context used for configuring a database.
/// </summary>
public abstract class DatabaseContext : IRelationContext
{
    /// <inheritdoc />
    public IQueryable<T> From<T>()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public IQueryable<T> FromValues<T>(params object[] values)
    {
        throw new NotImplementedException();
    }
}
