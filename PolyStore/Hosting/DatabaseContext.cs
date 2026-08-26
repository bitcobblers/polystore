using System;
using System.Linq;
using PolyStore.Core;

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
    public IRelation<T> Get<T>()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public IQueryable<int> Insert<T>(T item)
    {
        throw new NotImplementedException();
    }
}
