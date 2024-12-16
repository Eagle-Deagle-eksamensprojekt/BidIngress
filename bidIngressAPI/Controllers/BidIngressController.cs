using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RabbitMQ.Client;
using BidIngressAPI.Models;

namespace BidIngressAPI.Controllers
{
    [ApiController]
    [Route("bid")]
    public class BidIngressController : ControllerBase, IDisposable
    {
        private readonly ILogger<BidIngressController> _logger;
        private readonly IConfiguration _config;
        private readonly IConnection _connection;
        private readonly IModel _channel;

        public BidIngressController(ILogger<BidIngressController> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;

            var rabbitHost = config["RABBITMQ_HOST"] ?? "localhost";
            var factory = new ConnectionFactory { HostName = rabbitHost };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
        }

        [HttpPost]
        public IActionResult RouteBid([FromBody] Bid bid)
        {
            if (bid == null || string.IsNullOrWhiteSpace(bid.ItemId))
            {
                _logger.LogWarning("Invalid bid. Missing ItemId or bid details.");
                return BadRequest("Invalid bid. Missing ItemId or bid details.");
            }

            var queueName = $"{bid.ItemId}bid";

            // Check if the queue exists in RabbitMQ
            try
            {
                _channel.QueueDeclarePassive(queueName);
            }
            catch (RabbitMQ.Client.Exceptions.OperationInterruptedException)
            {
                _logger.LogWarning("Queue {QueueName} does not exist. Rejecting bid.", queueName);
                return NotFound($"Queue for item {bid.ItemId} does not exist.");
            }

            // Serialize bid to JSON
            var message = JsonSerializer.Serialize(bid);
            var body = Encoding.UTF8.GetBytes(message);

            // Publish the message to RabbitMQ
            _channel.BasicPublish(
                exchange: "",
                routingKey: queueName,
                basicProperties: null,
                body: body
            );

            _logger.LogInformation("Routed bid for ItemId {ItemId} to queue {QueueName}.", bid.ItemId, queueName);
            return Ok("Bid routed successfully.");
        }

        public void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
        }
    }
}
