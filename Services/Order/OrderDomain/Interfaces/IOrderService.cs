using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OrderDomain.Models;

namespace OrderDomain.Interfaces
{
    public interface IOrderService
    {
        Task<Order> CreateOrderAsync(int productId, double price, int quantity);
        Task<Order> GetOrderByIdAsync(int id);
        Task<List<Order>> GetAllOrdersAsync();
    }
}
