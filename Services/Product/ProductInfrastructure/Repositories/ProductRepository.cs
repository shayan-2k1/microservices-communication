using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProductDomain.Interfaces;
using ProductDomain.Models;
using ProductInfrastructure.Databases;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductInfrastructure.Repository
{
    public class ProductRepository : IProductService
    {
        private readonly ProductDbContext _dbContext;
        private readonly ILogger<ProductRepository> _logger;

        public ProductRepository(ProductDbContext dbContext, ILogger<ProductRepository> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<Product> GetProductByIdAsync(int id)
        {
            var product = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == id);
            if (product == null)
                throw new Exception($"Product with ID {id} not found");
            return product;
        }

        public async Task<List<Product>> GetAllProductsAsync()
        {
            return await _dbContext.Products.ToListAsync();
        }

        public async Task<Product> CreateProductAsync(Product product)
        {
            await _dbContext.Products.AddAsync(product);
            await _dbContext.SaveChangesAsync();
            return product;
        }

        public async Task UpdateProductAsync(Product product)
        {
            try
            {
                // Check if product exists
                var existingProduct = await _dbContext.Products
                    .FirstOrDefaultAsync(p => p.Id == product.Id);

                if (existingProduct == null)
                    throw new Exception($"Product with ID {product.Id} not found");

                // Update properties
                existingProduct.Name = product.Name;
                existingProduct.Description = product.Description;
                existingProduct.Price = product.Price;
                existingProduct.StockQuantity = product.StockQuantity;

                // Mark as modified and save
                _dbContext.Products.Update(existingProduct);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Product {Id} updated successfully", product.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product {Id}", product.Id);
                throw new Exception($"Error updating product: {ex.Message}", ex);
            }
        }

        public async Task DeleteProductAsync(int id)
        {
            try
            {
                var product = await _dbContext.Products
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (product == null)
                    throw new Exception($"Product with ID {id} not found");

                _dbContext.Products.Remove(product);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Product {Id} deleted successfully", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product {Id}", id);
                throw new Exception($"Error deleting product: {ex.Message}", ex);
            }
        }
    }
}
