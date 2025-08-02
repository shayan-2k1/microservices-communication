using Grpc.Core;
using GrpcService.Protos;
using System.Text.Json;
using ProductDomain.Models;

namespace GrpcService.Services
{
    public class ProductService:ProductGrpc.ProductGrpcBase
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ProductService> _logger;
        private readonly IConfiguration _config;

        public ProductService(HttpClient httpClient, ILogger<ProductService> logger, IConfiguration config)
        {
            _httpClient = httpClient;
            _logger = logger;
            _config = config;
        }
        public override async Task<ProductResponse> GetProduct(GetProductRequest request, ServerCallContext context)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, _config.GetValue<string>("product_api_url") + $"{request.Id}");
            var response = await _httpClient.SendAsync(req);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var product = JsonSerializer.Deserialize<ProductResponse>(content, options);
                return await Task.FromResult(product);
            }
            return await Task.FromResult(new ProductResponse());
        }

        public override async Task<GetAllProductsResponse> GetAllProducts(GetAllProductsRequest request, ServerCallContext context)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, _config.GetValue<string>("product_api_url"));
            var res = await _httpClient.SendAsync(req);
            if (res.IsSuccessStatusCode)
            {
                var content = await res.Content.ReadAsStringAsync();
                var option = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var products = JsonSerializer.Deserialize<GetAllProductsResponse>(content, option);
                return products;
            }
            return new GetAllProductsResponse();
        }

        public override async Task<ProductResponse> CreateProduct(CreateProductRequest request, ServerCallContext context)
        {
            var product = new Product
            {
                Name = request.Name,
                Description = request.Description,
                Price = request.Price,
                StockQuantity = request.StockQuantity,
            };

            var req = new HttpRequestMessage(HttpMethod.Post, _config.GetValue<string>("product_api_url"));
            var content = new StringContent(JsonSerializer.Serialize(product), System.Text.Encoding.UTF8, "application/json");
            req.Content = content;
            var res = await _httpClient.SendAsync(req);
            if (res.IsSuccessStatusCode)
            {
                return await Task.FromResult(new ProductResponse
                {
                    Name = product.Name,
                    Description = product.Description,
                    Price = product.Price,
                    StockQuantity = product.StockQuantity,
                    HasOrders= product.HasOrders,
                });
            }
            return await Task.FromResult(new ProductResponse());

        }
        public override async Task<ProductUpdateResponse> UpdateProduct(UpdateProductRequest request, ServerCallContext context)
        {
            var product = new Product
            {
                Id = request.Id,
                Name = request.Name,
                Description = request.Description,
                Price = request.Price,
                StockQuantity = request.StockQuantity,
                HasOrders = request.HasOrders,
            };

            var req = new HttpRequestMessage(HttpMethod.Put, $"{_config.GetValue<string>("product_api_url")}+{request.Id}");
            var content = new StringContent(JsonSerializer.Serialize(product), System.Text.Encoding.UTF8, "application/json");
            req.Content = content;
            var res = await _httpClient.SendAsync(req);
            if (res.IsSuccessStatusCode)
            {
                return await Task.FromResult(new ProductUpdateResponse
                {
                    StatusCode = 200
                });
            }
            return await Task.FromResult(new ProductUpdateResponse {StatusCode=404});

        }
    }
}
