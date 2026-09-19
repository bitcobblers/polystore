using HelloWorld;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PolyStore.Core;
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
            await using ITransaction tx = null!;

            _ = tx.ExecuteAsync(context => context
                .From<Customer>()
                .Update(
                    target: c => c,
                    update: c => new
                    {
                        LastName = "Doe"
                    }), cancellationToken);

            _ = tx.ExecuteAsync(context => context
                .FromValues<Customer>(new Customer
                {
                    Id = 1,
                    CreatedAt = DateTime.Now
                })
                .Insert(), cancellationToken);

            _ = tx.ExecuteAsync(context => context
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
