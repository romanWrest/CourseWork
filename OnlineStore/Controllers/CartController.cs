using Microsoft.AspNetCore.Mvc;
using Confluent.Kafka;
using Newtonsoft.Json;

namespace OnlineStore.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CartController : ControllerBase
    {
        private readonly ProducerConfig _config = new ProducerConfig { BootstrapServers = "localhost:9092" };

        [HttpPost("add")]
        public IActionResult AddToCart([FromBody] CartItem item)
        {
            using var producer = new ProducerBuilder<Null, string>(_config).Build();
            var message = JsonConvert.SerializeObject(new { UserId = item.UserId, ProductId = item.ProductId, Timestamp = DateTime.UtcNow });
            producer.Produce("user_events", new Message<Null, string> { Value = message });
            producer.Flush(TimeSpan.FromSeconds(10));

            Console.WriteLine($"Cart event: User {item.UserId} added Product {item.ProductId} to cart");
            return Ok(new { message = "Item added to cart" });
        }
    }

    public class CartItem
    {
        public int UserId { get; set; }
        public int ProductId { get; set; }
    }
}