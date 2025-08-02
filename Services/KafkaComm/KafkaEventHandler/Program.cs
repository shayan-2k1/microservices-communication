using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Confluent.Kafka;
using KafkaEventHandler.Services;
public class Program
{
    static async Task Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddHttpClient<KafkaEventHandlerService>();
        services.AddSingleton<KafkaEventHandlerService>();

        var serviceProvider=services.BuildServiceProvider();
        var consumerService=serviceProvider.GetRequiredService<KafkaEventHandlerService>();

        Console.WriteLine("Starting Kafka consumer...");

        var cancellationTokenSource = new CancellationTokenSource();
        var consumerTask = consumerService.StartAsync(cancellationTokenSource.Token);

        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationTokenSource.Cancel();
        };

        await consumerTask;
    }
}

