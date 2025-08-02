using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using RabbitEventHandler.Services;
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
        services.AddHttpClient<RabbitMqEventService>();
        services.AddSingleton<RabbitMqEventService>();

        var serviceProvider = services.BuildServiceProvider();
        //var consumerService = serviceProvider.GetRequiredService<RabbitMqEventService>();
        //await consumerService.StartAsync();
        Console.ReadLine();
    }
}

