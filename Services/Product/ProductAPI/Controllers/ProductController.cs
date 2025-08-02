using Microsoft.AspNetCore.Mvc;
using ProductDomain.Interfaces;
using ProductDomain.Models;

namespace ProductAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductController(IProductService productService)
        {
            _productService = productService;
        }
        [HttpGet]
        public async Task<IActionResult> GetAllProductsAsync()
        {
            var products = await _productService.GetAllProductsAsync();
            return Ok(products);
        }

        [HttpGet("{id}", Name = "GetProductById")]
        public async Task<IActionResult> GetProductByIdAsync(int id)
        {
            try
            {
                var product = await _productService.GetProductByIdAsync(id);
                return Ok(product);
            }
            catch (Exception ex)
            {
                return NotFound($"Product with ID {id} not found");
            }
        }

        [HttpPost(Name = "CreateProduct")]
        public async Task<IActionResult> CreateProductAsync(CreateProductRequest request)
        {
            var product = new Product
            {
                Name = request.Name,
                Description = request.Description,
                Price = request.Price,
                StockQuantity = request.StockQuantity,
                HasOrders=request.HasOrders
            };

            var createdProduct = await _productService.CreateProductAsync(product);
            return Created($"/api/products/{createdProduct.Id}", createdProduct);
        }

        [HttpPut("{id}", Name = "UpdateProduct")]
        public async Task<IActionResult> UpdateProductAsync(UpdateProductRequest request, int id)
        {
            try
            {
                var product = await _productService.GetProductByIdAsync(id);

                product.Name = request.Name;
                product.Description = request.Description;
                product.Price = request.Price;
                product.StockQuantity = request.StockQuantity;
                product.HasOrders = request.HasOrders;

                await _productService.UpdateProductAsync(product);
                return NoContent();
            }
            catch (Exception ex)
            {
                return NotFound($"Product with ID {id} not found");
            }
        }

        [HttpDelete("{id}",Name ="DeleteProduct")]
        public async Task<IActionResult> DeleteProductAsync(int id)
        {
            try
            {
                await _productService.DeleteProductAsync(id);
                return NoContent();
            }
            catch (Exception ex)
            {
                return NotFound($"Product with ID {id} not found");
            }
        }
    }
    public record CreateProductRequest(string Name, string Description, double Price, int StockQuantity, bool HasOrders);
    public record UpdateProductRequest(string Name, string Description, double Price, int StockQuantity, bool HasOrders);
}
