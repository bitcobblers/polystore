using HelloWorld;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PolyStore.Core;
using PolyStore.Hosting;
using PolyStore.Execution;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddSimpleConsole();
builder.Services.AddHostedService<HelloWorldService>();

using var host = builder
    .Build()
    .StartAsync();

namespace HelloWorld
{
    public class HelloWorldService(ILogger<HelloWorldService> logger, IHostApplicationLifetime lifetime) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Dummy objects for API syntax testing.
            IRelationContext context = null!;
            await using ITransaction tx = null!;

            var customers =
                from c in context.From<Customer>()
                where c.FirstName == "John"
                select c;

            _ = tx.ExecuteAsync(customers
                .Update(c => new
                {
                    LastName = "Doe"
                })
                .Update(c => new
                {
                    State = "UT"
                }), cancellationToken);

            _ = tx.ExecuteAsync(customers
                .Select(c => new
                {
                    C1 = c, // First customer
                    C2 = c // Second customer (or some other relation).
                })
                .Update(c => c.C1, // Update first customer.
                    c => new
                    {
                        LastName = "Doe"
                    })
                .Update(c => c.C2, // Update second customer.
                    c => new
                    {
                        State = "UT"
                    }), cancellationToken);

            _ = tx.ExecuteAsync(context
                .From<Customer>()
                .Update(
                    target: c => c,
                    update: c => new
                    {
                        LastName = "Doe"
                    }), cancellationToken);

            _ = tx.ExecuteAsync(context
                .FromValues<Customer>(new Customer
                {
                    Id = 1,
                    CreatedAt = DateTime.Now
                })
                .Insert(), cancellationToken);

            _ = tx.ExecuteAsync(context
                .From<Customer>()
                .Where(c => c.Expired == true)
                .Insert(c => new ArchivedCustomer
                {
                    Id = c.Id,
                    FirstName = c.FirstName,
                    ExpiredAt = DateTime.Now
                }), cancellationToken);

            lifetime.StopApplication();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Application stopped");
            return Task.CompletedTask;
        }
    }
}
