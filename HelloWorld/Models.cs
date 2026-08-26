using PolyStore.Core;
using PolyStore.Storage;

namespace HelloWorld;

[Relation, Store]
public record Customer
{
    public long Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool? Expired { get; set; }
    public DateTime CreatedAt { get; set; }
}

[Relation, Store]
public record ArchivedCustomer : Customer
{
    public DateTime ExpiredAt { get; set; }
}

[Relation, Store]
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

[Relation, Store]
public class Product
{
    public long Id { get; set; }
    public long SkuId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
}

[Relation, Store]
public class Order
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public long ShippingAddressId { get; set; }
    public DateTime CreatedAt { get; set; }
}

[Relation, Store(Storage: StorageType.Column)]
public class OrderItem
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public long ProductId { get; set; }
    public int Quantity { get; set; }
}
