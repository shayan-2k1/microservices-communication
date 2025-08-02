using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace KafkaEventHandler.Services
{
    public class KafkaEventHandlerService
    {
        private readonly IConsumer<string, string> _consumer;
        private readonly IProducer<string, string> _producer;
        private readonly HttpClient _httpClient;
        private readonly string _apiEndpoint;

        public KafkaEventHandlerService(IConfiguration configuration, HttpClient httpClient)
        {
            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = configuration["KafkaServer:Url"],
                GroupId = configuration["KafkaServer:GroupId"],
                AutoOffsetReset = AutoOffsetReset.Earliest,
            };

            var producerConfig = new ProducerConfig
            {
                BootstrapServers = configuration["KafkaServer:Url"]
            };

            _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
            _producer = new ProducerBuilder<string, string>(producerConfig).Build();
            _httpClient = httpClient;
            _apiEndpoint = configuration["ProductApi"]!;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Starting consumer...");
            _consumer.Subscribe(new List<string> { "product-update", "product-request" });
            CancellationTokenSource cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                // Prevent the process from terminating.
                e.Cancel = true;
                cts.Cancel();
            };

            try
            {
                while (true)
                {
                    try
                    {
                        var consumeResult = _consumer.Consume(cts.Token);
                        if (consumeResult == null)
                            continue;

                        await ProcessMessageAsync(consumeResult);
                    }
                    catch (ConsumeException e)
                    {
                        Console.WriteLine($"Error occurred: {e.Error.Reason}");
                    }
                }
            }
            finally
            {
                _consumer.Close();
            }
        }

        private async Task ProcessMessageAsync(ConsumeResult<string, string> consumeResult)
        {
            switch (consumeResult.Topic)
            {
                case "product-update":
                    var product = JsonConvert.DeserializeObject<ProductDTO>(consumeResult.Message.Value);
                    Console.WriteLine($"Received product: " +
                        $"Id: {product.Id}, " +
                        $"Name: {product.Name}, " +
                        $"Price: {product.Price}, " +
                        $"Stock: {product.StockQuantity}");
                    await ProcessUpdateMessageAsync(product);
                    break;
                case "product-request":
                    string productId = consumeResult.Message.Value;
                    var cId = consumeResult.Message.Key;
                    Console.WriteLine($"Received productId is {productId} ");
                    await ProcessFetchMessageAsync(productId, cId);
                    break;

                default:
                    Console.WriteLine("Error Processing topic");
                    break;
            }

        }

        private async Task ProcessFetchMessageAsync(string productId, string cId)
        {
            try
            {
                Int32.TryParse(productId, out var pId);
                var request = new HttpRequestMessage(HttpMethod.Get, $"{_apiEndpoint}{pId}");
                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var productInfo = await response.Content.ReadFromJsonAsync<ProductDTO>();
                    var productData = JsonConvert.SerializeObject(productInfo);
                    var dr = await _producer.ProduceAsync("product-response", new Message<string, string>
                    {
                        Key = cId,
                        Value = productData
                    });
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling API: {ex.Message}");
            }
        }

        private async Task ProcessUpdateMessageAsync(ProductDTO p)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Put, _apiEndpoint + p.Id);
                request.Content = new StringContent(JsonConvert.SerializeObject(p), Encoding.UTF8, "application/json");
                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"API call failed with status code: {response.StatusCode}");
                }
                else if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Entry for product with Id {p.Id} updated successfully.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling API: {ex.Message}");
            }
        }

        public async Task<ProductDTO> ProductResponseAsync(string cId, CancellationToken cancellationToken)
        {
            _consumer.Subscribe("product-response");

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var consumeResult = await Task.Run(() => _consumer.Consume(cancellationToken), cancellationToken);

                        if (consumeResult != null && consumeResult.Message.Key == cId)
                        {
                            return JsonConvert.DeserializeObject<ProductDTO>(consumeResult.Message.Value);
                        }
                    }
                    catch (ConsumeException e)
                    {
                        Console.WriteLine($"Error consuming message: {e.Error.Reason}");
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine("Consume operation was canceled.");
                        break;
                    }
                }
            }
            finally
            {
                _consumer.Unsubscribe();
            }
            return null;
        }

        public async Task UpdateProductAsync(ProductDTO product)
        {
            string pd = JsonConvert.SerializeObject(product);
            string key = product.Id.ToString();
            try
            {
                var dr = await _producer.ProduceAsync("product-update", new Message<string, string>
                {
                    Key = key,
                    Value = pd
                });
                Console.WriteLine($"Delivered '{dr.Value}' to '{dr.TopicPartitionOffset}'");

            }
            catch (ProduceException<string, string> e)
            {
                Console.WriteLine($"Delivery failed: {e.Error.Reason}");
            }
        }
        public async Task<ProductDTO> GetProductAsync(int productId, CancellationToken cancellationToken=default)
        {
            var correlationId = Guid.NewGuid().ToString();
            try
            {


                var dr=await _producer.ProduceAsync("product-request", new Message<string, string>
                {
                    Key = correlationId,
                    Value = productId.ToString()
                });
                Console.WriteLine($"Delivered '{dr.Value}' to '{dr.TopicPartitionOffset}'");
            }
            catch (ProduceException<string, string> e)
            {
                Console.WriteLine($"Delivery failed: {e.Error.Reason}");
            }
            return await ProductResponseAsync(correlationId, cancellationToken);
        }
    }
}
