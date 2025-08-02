using Microsoft.EntityFrameworkCore;
using OrderDomain.Interfaces;
using OrderDomain.Models;
using OrderInfrastructure.Databases;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrderInfrastructure.Repository
{
    public class OrderRepository : IOrderService
    {

        private readonly OrderDbContext _dbContext;
        public OrderRepository(OrderDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Order> CreateOrderAsync(int productId, double price, int quantity)
        {

            var order = new Order
            {
                ProductId = productId,
                Quantity = quantity,
                TotalPrice = price * quantity
            };

            await _dbContext.Orders.AddAsync(order);
            await _dbContext.SaveChangesAsync();
            return order;
        }

        public async Task<Order> GetOrderByIdAsync(int id)
        {
            var order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == id);
            if (order == null)
                throw new Exception($"Order with ID {id} not found");
            return order;
        }

        public async Task<List<Order>> GetAllOrdersAsync()
        {
            try
            {
                return await _dbContext.Orders.ToListAsync();
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }
}
