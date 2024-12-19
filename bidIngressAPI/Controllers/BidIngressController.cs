using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RabbitMQ.Client;
using BidIngressAPI.Models;
using Microsoft.AspNetCore.Authorization;
using System.Diagnostics;

namespace BidIngressAPI.Controllers
{
    [ApiController]
    [Route("ingress")]
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

            try
            {
                var rabbitHost = config["RABBITMQ_HOST"] ?? "localhost";
                var factory = new ConnectionFactory { HostName = rabbitHost };
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
                _logger.LogInformation("Connected to RabbitMQ at {RabbitHost}.", rabbitHost);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to connect to RabbitMQ. Check configuration and RabbitMQ availability.");
                throw;
            }
        }

        [AllowAnonymous]
        [HttpGet("version")]
        public async Task<IActionResult> GetVersion()
        {
            var properties = new Dictionary<string, string>();

            var ver = FileVersionInfo.GetVersionInfo(
                typeof(Program).Assembly.Location).ProductVersion ?? "N/A";
            properties.Add("version", ver);

            return Ok(new {properties});
        }

        [Authorize]
        [HttpPost]
        public IActionResult RouteBid([FromBody] Bid bid)
        {
            _logger.LogInformation("Received bid: {@Bid}", bid);

            if (bid == null || string.IsNullOrWhiteSpace(bid.ItemId)) // Check for missing ItemId
            {
                _logger.LogWarning("Invalid bid received. Missing ItemId or bid details.");
                return BadRequest("Invalid bid. Missing ItemId or bid details.");
            }

            var queueName = $"{bid.ItemId}bid"; // Queue name is based on ItemId

            try
            {
                _logger.LogInformation("Checking existence of queue {QueueName} for ItemId {ItemId}.", queueName, bid.ItemId);
                _channel.QueueDeclarePassive(queueName); // Check if queue exists
            }
            catch (RabbitMQ.Client.Exceptions.OperationInterruptedException ex) 
            {
                _logger.LogWarning(ex, "Queue {QueueName} for ItemId {ItemId} does not exist.", queueName, bid.ItemId);
                return NotFound($"Queue for item {bid.ItemId} does not exist.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while checking for queue {QueueName}.", queueName);
                return StatusCode(500, "An error occurred while processing the request.");
            }

            try
            {
                // Serialize bid to JSON
                var message = JsonSerializer.Serialize(bid);
                var body = Encoding.UTF8.GetBytes(message);

                _logger.LogInformation("Publishing bid to queue {QueueName} for ItemId {ItemId}.", queueName, bid.ItemId);
                _channel.BasicPublish( // Publish bid to queue
                    exchange: "",
                    routingKey: queueName,
                    basicProperties: null,
                    body: body
                );

                _logger.LogInformation("Successfully routed bid for ItemId {ItemId} to queue {QueueName}.", bid.ItemId, queueName);
                return Ok(new { message = "Bid routed successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to route bid for ItemId {ItemId} to queue {QueueName}.", bid.ItemId, queueName);
                return StatusCode(500, "An error occurred while routing the bid.");
            }
        }

        public void Dispose()
        {
            _logger.LogInformation("Disposing RabbitMQ resources.");
            _channel?.Close();
            _connection?.Close();
        }
    }
}
