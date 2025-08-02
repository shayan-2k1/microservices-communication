using AutoMapper;
using Confluent.Kafka;
using Grpc.Core;
using Grpc.Net.Client;
using GrpcService.Protos;
using KafkaEventHandler.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OrderDomain.Interfaces;
using OrderDomain.Models;
using RabbitEventHandler.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace OrderAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly ProductGrpc.ProductGrpcClient _channel;
        private readonly RabbitMqEventService _rabbitService;
        private readonly KafkaEventHandlerService _kafkaService;
        private readonly IMapper _mapper;


        public OrderController(IOrderService orderService, ProductGrpc.ProductGrpcClient channel, IConfiguration configuration, RabbitMqEventService rabbitService, KafkaEventHandlerService kafkaService, IMapper mapper)
        {
            _orderService = orderService;
            _channel = channel;
            _rabbitService = rabbitService;
            _kafkaService = kafkaService;
            _mapper = mapper;
        }



        [HttpGet(Name = "GetAllOrders")]
        public async Task<IActionResult> GetAllOrderAsync()
        {
            var orders = await _orderService.GetAllOrdersAsync();
            return Ok(orders);
        }

        [HttpGet("{id}", Name = "GetOrderById")]
        public async Task<IActionResult> GetOrderByIdAsync(int id)
        {
            try
            {
                var order = await _orderService.GetOrderByIdAsync(id);
                return Ok(order);
            }
            catch (Exception ex)
            {
                return NotFound($"Order with ID {id} not found");
 
            }
        }
        [HttpPost( Name = "CreateOrder")]
        public async Task<IActionResult> CreateOrderAsync([FromBody]CreateOrderRequest request)
        {
            try
            {
                //-------------------------------------------------------------gRPC Usage-----------------------------------------------------
                //var product = await _channel.GetProductAsync(
                //    new GetProductRequest { Id = request.ProductId }
                //);
                //------------------------------------------------------------Kafka Async Usage-----------------------------------------------
                //var product = await _kafkaService.GetProductAsync(request.ProductId);

                //------------------------------------------------------------RabbitMQ Client-----------------------------------------------------------------------------------------------------------------------------------
                var product = await _rabbitService.GetProductAsync(request.ProductId.ToString());

                //--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
                if (product == null)
                {
                    return NotFound($"Sorry product with Id{request.ProductId} is not available");
                }

                if (request.Quantity > product.StockQuantity)
                {
                    return Problem($"Only {product.StockQuantity} items are left. Not sufficient stock to place your order");
                }

                product.StockQuantity -= request.Quantity;
                if (!product.HasOrders)
                {
                    product.HasOrders = true;


                    //------------------------------------------------Update Using Kafka----------------------------------------------------------------------------------------------------------------------------------------
                    //var productDTO = _mapper.Map<ProductDTO>(product);
                    //await _kafkaService.UpdateProductAsync(productDTO);
                    //-----------------------------------------------Update Using gRPC------------------------------------------------------------------------------------------------------------------------------------------
                    //try
                    //{
                    //    var updateProduct = await _channel.UpdateProductAsync(
                    //        new UpdateProductRequest { Id = product.Id, Name = product.Name, Description = product.Description, StockQuantity = product.StockQuantity, Price = product.Price, HasOrders = product.HasOrders });
                    //    if (updateProduct.StatusCode == 200)
                    //    {
                    //        Console.WriteLine($"Update for product with Id {product.Id} has been successful!");
                    //    }
                    //}
                    //catch (Exception ex)
                    //{
                    //    Console.WriteLine($"Update for product with Id {product.Id} has failed");
                    //}
                    //-----------------------------------------------Update Using RabbitMQ--------------------------------------------------------------------------------------------------------------------------------------
                    var productDTO = _mapper.Map<ProductDTO>(product);
                    var updateProduct = await _rabbitService.UpdateProductAsync(productDTO);
                    if (updateProduct)
                    {
                        Console.WriteLine($"Product with id {product.Id} has been updated successfully");
                    }
                    //----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
                }
                else
                {
                    Console.WriteLine($"Product with Id{product.Id} already has orders");
                }

                var order = await _orderService.CreateOrderAsync(request.ProductId, product.Price, request.Quantity);
                return Created($"/api/orders/{order.Id}", order);
            }
            catch (RpcException ex)
            {
                return Problem($"Error creating order: {ex.Message}");
            }
        }
        public record CreateOrderRequest(int ProductId, int Quantity);

    };
}
