using System;
using System.Linq.Expressions;

namespace PolyStore.Storage;

public class HeapPath<T> : IAccessPath<T>
{
    /// <summary>
    /// Gets or sets the default page size to use.
    /// </summary>
    public int PageSize { get; set; } = 8192;

    /// <summary>
    /// Gets or sets the default fill factor for a page.
    /// </summary>
    public double FillFactor { get; set; } = 0.90;

    /// <inheritdoc />
    public virtual void Configure()
    {
    }

    /// <summary>
    /// Includes a column in the heap.
    /// </summary>
    /// <param name="getColumn">The expression for the column to get.</param>
    /// <remarks>
    /// A heap entry always stores the tuple's RID, the row identifier that locates the
    /// tuple within the underlying keystore. The RID is the minimum payload of every heap
    /// row, so a heap page can be used to discover and order tuples even when no other
    /// column has been included.
    ///
    /// Columns passed to this method are stored inline in the heap page alongside the RID.
    /// Because they are already present in the page, a query can read them directly from the
    /// heap without a separate lookup into the underlying keystore for that additional tuple
    /// data.
    ///
    /// This avoids the round-trip to the keystore for columns a query is likely to need,
    /// reducing I/O and latency. The trade-off is that each heap page must be large enough to
    /// hold the included columns in addition to the RID, which is governed by
    /// <see cref="PageSize"/> and <see cref="FillFactor"/>.
    /// </remarks>
    protected void Include(Expression<Func<T, object?>> getColumn)
    {
    }
}
