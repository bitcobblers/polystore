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
            logger.LogInformation("Hello World!");

            // Dummy objects for API syntax testing.
            IRelationContext context = null!;
            await using ITransaction tx = null!;

            var customers =
                from c in context.From<Customer>()
                where c.FirstName == "John"
                select c;

            await tx.ExecuteAsync(customers
                .Update(c => new
                {
                    LastName = "Doe"
                })
                .Select(u => u.Id));

#pragma warning disable CS0618 // Type or member is obsolete
            await tx.ExecuteAsync(context
                .Insert(new Customer
                {
                    Id = 1,
                    CreatedAt = DateTime.Now
                }));
#pragma warning restore CS0618 // Type or member is obsolete

            await tx.ExecuteAsync(customers
                .Update(c => new
                {
                    LastName = "Doe"
                })
                .Update(c => new
                {
                    State = "UT"
                }));

            await tx.ExecuteAsync(customers
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
                    }));

            await tx.ExecuteAsync(context
                .From<Customer>()
                .Update(
                    target: c => c,
                    update: c => new
                    {
                        LastName = "Doe"
                    }));

            await tx.ExecuteAsync(context
                .Get<Customer>()
                .Insert(new Customer
                {
                    Id = 1,
                    CreatedAt = DateTime.Now
                }));

            await tx.ExecuteAsync(context
                .From<Customer>()
                .Where(c => c.Expired == true)
                .Insert(c => new ArchivedCustomer
                {
                    Id = c.Id,
                    FirstName = c.FirstName,
                    ExpiredAt = DateTime.Now
                }));

            lifetime.StopApplication();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Application stopped");
            return Task.CompletedTask;
        }
    }
}