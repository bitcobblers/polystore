using System;
using System.Linq.Expressions;

namespace PolyStore.Storage;

public class BTreePath<T> : IAccessPath<T>
{
    /// <inheritdoc />
    public virtual void Configure()
    {

    }

    /// <summary>
    /// Adds a searchable column to the b-tree
    /// </summary>
    /// <remarks>
    /// The column becomes part of the b-tree's key, which defines the ordering of the tree
    /// and the values that can be searched, sorted, or ranged on. Multiple columns form a
    /// composite key in the sequence they are added, the first added being the most
    /// significant.
    /// </remarks>
    /// <param name="getColumn">An expression to resolve the column to add.</param>
    protected void Column(Expression<Func<T, object?>> getColumn)
    {

    }

    /// <summary>
    /// Adds payload data to the b-tree path.
    /// </summary>
    /// <remarks>
    /// Included columns are stored inline in the b-tree leaf pages alongside the key, so a
    /// query reading them can be satisfied directly from the leaf without a lookup into the
    /// underlying keystore. Unlike <see cref="Column"/>, they are not part of the key and do
    /// not affect ordering or searchability.
    /// </remarks>
    /// <param name="getColumn">An expression to resolve the column to include.</param>
    protected void Include(Expression<Func<T, object?>> getColumn)
    {

    }
}
