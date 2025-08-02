using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace RabbitEventHandler.Services
{
    public class RabbitMqEventService:IDisposable
    {
        private string FETCH_QUEUE_NAME = string.Empty;
        private string UPDATE_QUEUE_NAME = string.Empty;
        private string REPLY_QUEUE_NAME=string.Empty;

        private const string EXCHANGE_NAME = "microservices";

        private readonly IConnectionFactory _factory;
        private IConnection? _connection;
        private IChannel? _channel;
        private string _apiEndpoint=String.Empty;
        private readonly IConfiguration _configuration;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<ProductDTO>> _getCallbackMapper = new();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _updateCallbackMapper = new();
        private readonly Lazy<Task> _initializeTask;

        public RabbitMqEventService(IConfiguration configuration)
        {
            _factory = new ConnectionFactory { HostName = "localhost" };
            _configuration = configuration;
            _initializeTask = new Lazy<Task>(() => StartAsync());
        }

        private async Task EnsureInitializedAsync()
        {
            await _initializeTask.Value;
        }

        public async Task StartAsync()
        {
            _connection = await _factory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();
            _apiEndpoint = _configuration["ProductApi"]!;

            await _channel.ExchangeDeclareAsync(EXCHANGE_NAME, type: ExchangeType.Direct);
            var fetchQueueResult = await _channel.QueueDeclareAsync(
                queue: "",
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null
             );

            FETCH_QUEUE_NAME=fetchQueueResult.QueueName;

            var updateQueueResult = await _channel.QueueDeclareAsync(
                 queue: "",
                 durable: false,
                 exclusive: false,
                 autoDelete: false,
                 arguments: null
             );

            UPDATE_QUEUE_NAME = updateQueueResult.QueueName;

            QueueDeclareOk replyQueueResult = await _channel.QueueDeclareAsync(
                 queue: "",
                 durable: false,
                 exclusive: false,
                 autoDelete: false,
                 arguments: null
            );

            REPLY_QUEUE_NAME = replyQueueResult.QueueName;

            await _channel.QueueBindAsync(FETCH_QUEUE_NAME, EXCHANGE_NAME, "fetch_product");
            await _channel.QueueBindAsync(UPDATE_QUEUE_NAME, EXCHANGE_NAME, "update_product");

            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.ReceivedAsync += async (sender, ea) =>
            {
                var routingKey = ea.RoutingKey;
                try
                {
                    await ConsumeEventsAsync(routingKey, sender, ea);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing message: {ex.Message}");
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
                }

            };

            var replyConsumer = new AsyncEventingBasicConsumer(_channel);
            replyConsumer.ReceivedAsync += async (model, ea) =>
            {
                var correlationId = ea.BasicProperties.CorrelationId;

                if (_getCallbackMapper.TryRemove(correlationId, out var tcs))
                {
                    try
                    {
                        var response = Encoding.UTF8.GetString(ea.Body.ToArray());
                        var product = JsonSerializer.Deserialize<ProductDTO>(response);
                        tcs.SetResult(product);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }
                else if (_updateCallbackMapper.TryRemove(correlationId, out var updateTcs))
                {
                    try
                    {
                        var success = true;
                        updateTcs.SetResult(success);
                    }
                    catch (Exception ex)
                    {
                        updateTcs.SetException(ex);
                    }
                }

                await _channel.BasicAckAsync(ea.DeliveryTag, false);
            };
            await _channel.BasicConsumeAsync(REPLY_QUEUE_NAME, false, replyConsumer);
            await _channel.BasicConsumeAsync(FETCH_QUEUE_NAME, false, consumer);
            await _channel.BasicConsumeAsync(UPDATE_QUEUE_NAME, false, consumer);

        }

        public async Task ConsumeEventsAsync(string routingKey, object sender, BasicDeliverEventArgs ea)
        {
            switch (routingKey)
            {
                case "fetch_product":
                    await GetProductAsync(sender, ea);
                    break;
                case "update_product":
                    await UpdateProductAsync(ea);
                    break;
                default:
                    Console.WriteLine($"Unknown routing key {ea.RoutingKey}");
                    await _channel!.BasicAckAsync(ea.DeliveryTag, false);
                    break;
            }
        }
        public async Task UpdateProductAsync(BasicDeliverEventArgs ea)
        {
            byte[] body = ea.Body.ToArray();
            var product = JsonSerializer.Deserialize<ProductDTO>(body);

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Put, $"https://localhost:7077/api/product/{product.Id}");
                request.Content = new StringContent(JsonSerializer.Serialize(product), Encoding.UTF8, "application/json");
                var response = await new HttpClient().SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Entry for product with id {product.Id} has been updated successfully.");
                    _channel.BasicAckAsync(ea.DeliveryTag, false);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to update product with Id {product.Id}");
            }
        }

        public async Task GetProductAsync(object sender, BasicDeliverEventArgs ea)
        {
            AsyncEventingBasicConsumer cons = (AsyncEventingBasicConsumer)sender;
            IChannel? ch = cons.Channel;

            byte[] body = ea.Body.ToArray();
            IReadOnlyBasicProperties props = ea.BasicProperties;
            var message = String.Empty;
            var replyProps = new BasicProperties
            {
                CorrelationId = props.CorrelationId,
            };
            try
            {
                var id = Encoding.UTF8.GetString(body);
                var request = new HttpRequestMessage(HttpMethod.Get, $"https://localhost:7077/api/product/{id}");
                var response = await new HttpClient().SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var p = await response.Content.ReadFromJsonAsync<ProductDTO>();
                    message = JsonSerializer.Serialize(p);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($" [.] {ex.Message}");
            }
            finally
            {
                var productBytes = Encoding.UTF8.GetBytes(message);
                await ch.BasicPublishAsync(exchange: string.Empty, routingKey: props.ReplyTo!,
                    mandatory: true, basicProperties: replyProps, body: productBytes);
                await ch.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
            }

        }

        public async Task<ProductDTO> GetProductAsync(string productId, CancellationToken cancellationToken = default)
        {
            await EnsureInitializedAsync();
            var correlationId = Guid.NewGuid().ToString();
            var props = new BasicProperties
            {
                CorrelationId = correlationId,
                ReplyTo = REPLY_QUEUE_NAME,
            };

            var tcs = new TaskCompletionSource<ProductDTO>(TaskCreationOptions.RunContinuationsAsynchronously);
            _getCallbackMapper.TryAdd(correlationId, tcs);

            var messageBytes = Encoding.UTF8.GetBytes(productId);

            await _channel!.BasicPublishAsync(exchange: EXCHANGE_NAME, routingKey: "fetch_product", mandatory: true, basicProperties: props, body: messageBytes);

            using CancellationTokenRegistration ctr = cancellationToken.Register(() =>
            {
                _getCallbackMapper.TryRemove(correlationId, out _);
                tcs.SetCanceled();
            });

            return await tcs.Task;
        }

        public async Task<bool> UpdateProductAsync(ProductDTO product, CancellationToken cancellationToken = default)
        {
            await EnsureInitializedAsync();

            var correlationId = Guid.NewGuid().ToString();
            var props = new BasicProperties
            {
                CorrelationId = correlationId,
                ReplyTo = REPLY_QUEUE_NAME,
            };

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _updateCallbackMapper.TryAdd(correlationId, tcs);

            var productData = JsonSerializer.Serialize(product);
            var body = Encoding.UTF8.GetBytes(productData);

            await _channel!.BasicPublishAsync(exchange: EXCHANGE_NAME, routingKey: "update_product", mandatory: true, basicProperties: props, body: body);

            using CancellationTokenRegistration ctr = cancellationToken.Register(() =>
            {
                _updateCallbackMapper.TryRemove(correlationId, out _);
                tcs.SetCanceled();
            });

            return await tcs.Task;
        }
        public void Dispose()
        {
            _connection?.Dispose();
            _channel?.Dispose();
        }

    }
}
