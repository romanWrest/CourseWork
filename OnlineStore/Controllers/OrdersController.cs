using Microsoft.AspNetCore.Mvc;
using OnlineStore.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using RabbitMQ.Client; // Для RabbitMQ
using System.Text;
using Newtonsoft.Json;
using Confluent.Kafka; // Для Kafka

namespace OnlineStore.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly OnlineStoreContext _context;
        private readonly ProducerConfig _kafkaConfig = new ProducerConfig { BootstrapServers = "localhost:9092" };

        public OrdersController(OnlineStoreContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Authorize]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrders()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var orders = await _context.Orders.Include(o => o.Products).ToListAsync();

            // Отправка метрик в Kafka (Задание 5)
            stopwatch.Stop();
            SendMetricsToKafka("GetOrders", stopwatch.ElapsedMilliseconds);

            return orders;
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<Order>> GetOrder(int id)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var order = await _context.Orders.Include(o => o.Products).FirstOrDefaultAsync(o => o.Id == id);

            // Отправка метрик в Kafka (Задание 5)
            stopwatch.Stop();
            SendMetricsToKafka("GetOrder", stopwatch.ElapsedMilliseconds);

            return order ?? (ActionResult<Order>)NotFound();
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<Order>> CreateOrder([FromBody] Order order)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            if (order == null || order.Products == null || !order.Products.Any())
            {
                return BadRequest("Products are required.");
            }

            var productIds = order.Products.Select(p => p.Id).ToList();
            var existingProducts = await _context.Products.Where(p => productIds.Contains(p.Id)).ToListAsync();
            if (existingProducts.Count != productIds.Count)
            {
                return BadRequest("One or more product IDs are invalid.");
            }

            var newOrder = new Order
            {
                UserId = order.UserId,
                Products = existingProducts,
                OrderDate = DateTime.UtcNow,
                TotalPrice = existingProducts.Sum(p => p.Price),
                Status = "Pending" 
            };

            _context.Orders.Add(newOrder);
            await _context.SaveChangesAsync();

            // Отправка сообщения в RabbitMQ 
            var factory = new ConnectionFactory { HostName = "localhost" };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();
            channel.QueueDeclare(queue: "order_created_queue", durable: false, exclusive: false, autoDelete: false, arguments: null);

            var message = JsonConvert.SerializeObject(new { newOrder.Id, newOrder.UserId, newOrder.TotalPrice, newOrder.OrderDate });
            var body = Encoding.UTF8.GetBytes(message ?? string.Empty);
            channel.BasicPublish(exchange: "", routingKey: "order_created_queue", basicProperties: null, body: body);

            // Отправка метрик в Kafka 
            stopwatch.Stop();
            SendMetricsToKafka("CreateOrder", stopwatch.ElapsedMilliseconds);

            Console.WriteLine($"Order {newOrder.Id} created and sent to RabbitMQ queue 'order_created_queue'");
            return CreatedAtAction(nameof(GetOrder), new { id = newOrder.Id }, newOrder);
        }

        [HttpPut("{id}/status")]
        [Authorize]
        public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] OrderStatusUpdate update)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            if (update == null || string.IsNullOrEmpty(update.Status))
            {
                return BadRequest("Status is required.");
            }

            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            order.Status = update.Status;
            await _context.SaveChangesAsync();

            // Отправка сообщения в RabbitMQ (Задание 4)
            var factory = new ConnectionFactory { HostName = "localhost" };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();
            channel.QueueDeclare(queue: "order_status_updates", durable: false, exclusive: false, autoDelete: false, arguments: null);

            var message = JsonConvert.SerializeObject(new { order.Id, order.UserId, order.Status });
            var body = Encoding.UTF8.GetBytes(message);
            channel.BasicPublish(exchange: "", routingKey: "order_status_updates", basicProperties: null, body: body);

            // Отправка метрик в Kafka (Задание 5)
            stopwatch.Stop();
            SendMetricsToKafka("UpdateOrderStatus", stopwatch.ElapsedMilliseconds);

            Console.WriteLine($"Order {order.Id} status updated to '{order.Status}' and sent to RabbitMQ");
            return NoContent();
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> UpdateOrder(int id, [FromBody] Order order)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            if (id != order.Id) return BadRequest("Order ID mismatch.");
            var existingOrder = await _context.Orders.Include(o => o.Products).FirstOrDefaultAsync(o => o.Id == id);
            if (existingOrder == null) return NotFound();

            var productIds = order.Products.Select(p => p.Id).ToList();
            var existingProducts = await _context.Products.Where(p => productIds.Contains(p.Id)).ToListAsync();
            if (existingProducts.Count != productIds.Count) return BadRequest("One or more product IDs are invalid.");

            existingOrder.UserId = order.UserId;
            existingOrder.Products = existingProducts;
            existingOrder.TotalPrice = existingProducts.Sum(p => p.Price);
            existingOrder.OrderDate = DateTime.UtcNow;

            _context.Entry(existingOrder).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            // Отправка метрик в Kafka (Задание 5)
            stopwatch.Stop();
            SendMetricsToKafka("UpdateOrder", stopwatch.ElapsedMilliseconds);

            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();

            // Отправка метрик в Kafka (Задание 5)
            stopwatch.Stop();
            SendMetricsToKafka("DeleteOrder", stopwatch.ElapsedMilliseconds);

            return NoContent();
        }

        // Вспомогательный метод для отправки метрик в Kafka
        private void SendMetricsToKafka(string operation, long elapsedMs)
        {
            using var producer = new ProducerBuilder<Null, string>(_kafkaConfig).Build();
            var metric = JsonConvert.SerializeObject(
                new { 
                    Service = "OnlineStore", 
                    Operation = operation, 
                    ElapsedMs = elapsedMs, 
                    Timestamp = DateTime.UtcNow });
            producer.Produce("system_metrics", new Message<Null, string> { Value = metric });
            producer.Flush(TimeSpan.FromSeconds(10));
        }
    }

    public class OrderStatusUpdate
    {
        public string? Status { get; set; } // Сделано nullable для устранения CS8618
    }
}