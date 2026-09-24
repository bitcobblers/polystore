using PolyStore.Core;
using PolyStore.Storage;

namespace HelloWorld;

[Relation(Name = "customer")]
[RowStore]
public record Customer
{
    public long Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool? Expired { get; set; }
    public DateTime CreatedAt { get; set; }
}

[Relation(Name = "ArchivedCustomer")]
[ColumnStorage(Compression = CompressionType.Brotli)]
[RowStore(FillFactor = 0.75)]
public record ArchivedCustomer : Customer, IPropagateChanges<ArchivedCustomer>
{
    public DateTime ExpiredAt { get; set; }

    /// <inheritdoc />
    public IQueryable<ArchivedCustomer> Define(IRelationContext context) =>
        context.From<Customer>()
            .Where(c => c.Expired == true)
            .Select(c => new ArchivedCustomer
            {
                Id = c.Id,
                CreatedAt = c.CreatedAt,
                ExpiredAt = DateTime.UtcNow,
                Expired = c.Expired,
                FirstName = c.FirstName,
                LastName = c.LastName
            });

    /// <inheritdoc />
    public IQueryable<ArchivedCustomer> Propagate(IRelationChangeContext context) =>
        context
            .Combine(
                context
                    .Combine(
                        context.Inserts<Customer>()
                            .Where(c => c.Expired == true)
                            .Select(c => new ArchivedCustomer
                            {
                                Id = c.Id,
                                CreatedAt = c.CreatedAt,
                                ExpiredAt = DateTime.UtcNow,
                                Expired = c.Expired,
                                FirstName = c.FirstName,
                                LastName = c.LastName
                            }),
                        context.Updates<Customer>()
                            .Where(c => c.OldValue.Expired == false && c.NewValue.Expired == true)
                            .Select(c => new ArchivedCustomer
                            {
                                Id = c.NewValue.Id,
                                CreatedAt = c.NewValue.CreatedAt,
                                Expired = c.NewValue.Expired,
                                ExpiredAt = DateTime.UtcNow,
                                FirstName = c.NewValue.FirstName,
                                LastName = c.NewValue.LastName
                            })
                    ).Insert(),
                context
                    .Combine(
                        context.Deletes<Customer>()
                            .Where(c => c.Expired == true)
                            .Select(c => new ArchivedCustomer
                            {
                                Id = c.Id
                            }),
                        context.Updates<Customer>()
                            .Where(c => c.OldValue.Expired == true && c.NewValue.Expired == false)
                            .Select(c => new ArchivedCustomer
                            {
                                Id = c.NewValue.Id
                            })
                    ).Delete());
}

[Relation, RowStore]
public class OrderByCustomer : IPropagateChanges<OrderByCustomer>
{
    public long CustomerId { get; set; }
    public int OrderCount { get; set; }

    /// <inheritdoc />
    public IQueryable<OrderByCustomer> Define(IRelationContext context) =>
        context.From<Order>()
            .GroupBy(c => c.CustomerId)
            .Select(g => new OrderByCustomer
            {
                CustomerId = g.Key,
                OrderCount = g.Count()
            });

    /// <inheritdoc />
    public IQueryable<OrderByCustomer> Propagate(IRelationChangeContext context) =>
        context
            .Inserts<Order>()
            .GroupBy(o => o.CustomerId)
            .LeftJoin(
                context.From<OrderByCustomer>(),
                k => k.Key,
                agg => agg.CustomerId,
                (src, dest) => new
                {
                    CustomerId = src.Key,
                    OrderCount = src.Count(),
                    Existing = dest
                })
            .Fork(
                chg => chg
                    .Where(c => c.Existing == null)
                    .Insert(c => new OrderByCustomer
                    {
                        CustomerId = c.CustomerId,
                        OrderCount = c.OrderCount
                    }),
                chg => chg
                    .Where(x => x.Existing != null)
                    .Update(c => new OrderByCustomer
                    {
                        CustomerId = c.CustomerId,
                        OrderCount = c.OrderCount + c.Existing!.OrderCount
                    }));
}

[Relation, RowStore]
public class Address
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public required string Type { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
}

[Relation, RowStore]
public class Product
{
    public long Id { get; set; }
    public long SkuId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
}

[Relation, RowStore]
public class Order
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public long ShippingAddressId { get; set; }
    public DateTime CreatedAt { get; set; }
}

[Relation, RowStore]
public class OrderItem
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public long ProductId { get; set; }
    public int Quantity { get; set; }
}
