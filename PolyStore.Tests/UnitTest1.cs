using PolyStore.Hosting;

namespace PolyStore.Tests;

public class UnitTest1
{
    public class Order
    {
        public int CustomerId { get; set; }
        public int OrderId { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal Amount { get; set; }
    }

    public class OrderByCustomer
    {
        public int CustomerId { get; init; }
        public int OrderId { get; init; }
    }

    public class StoreDatabase : DatabaseModule
    {
        /// <inheritdoc />
        public override void Configure(DatabaseBuilder db)
        {
        }
    }

    [Fact]
    public async Task Test1()
    {
        await Task.CompletedTask;
    }
}