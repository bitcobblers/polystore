using System;
using System.Linq;
using PolyStore.Core;
using PolyStore.Storage;

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
    public IQueryable<T> From<T, TPath>() where TPath : IAccessPath<T>
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public IQueryable<T> FromValues<T>(params T[] values)
    {
        throw new NotImplementedException();
    }
}
