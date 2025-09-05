using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using StackExchange.Redis;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

public interface IOrderService
{
    Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request);
}

public class OrderService : IOrderService
{
    private readonly AppDbContext _context;
    private readonly IDatabase _redis;
    private readonly IAmazonDynamoDB _dynamoDB;
    private readonly IAmazonSQS _sqs;
    private readonly ILogger<OrderService> _logger;

    public static readonly ActivitySource ActivitySource = new("MinimalApi.Orders");
    private static readonly Meter Meter = new("MinimalApi.Orders");

    // Métricas customizadas
    private static readonly Counter<int> OrdersCreatedCounter = Meter.CreateCounter<int>("orders_created_total", "Total number of orders created");
    private static readonly Histogram<double> OrderAmountHistogram = Meter.CreateHistogram<double>("order_amount", "Order amount distribution");
    private static readonly Counter<int> CacheHitsCounter = Meter.CreateCounter<int>("cache_hits_total", "Total cache hits");
    private static readonly Counter<int> CacheMissesCounter = Meter.CreateCounter<int>("cache_misses_total", "Total cache misses");

    public OrderService(
        AppDbContext context,
        IDatabase redis,
        IAmazonDynamoDB dynamoDB,
        IAmazonSQS sqs,
        ILogger<OrderService> logger)
    {
        _context = context;
        _redis = redis;
        _dynamoDB = dynamoDB;
        _sqs = sqs;
        _logger = logger;
    }

    public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request)
    {
        using var activity = ActivitySource.StartActivity("CreateOrder");
        activity?.SetTag("user_id", request.UserId);
        activity?.SetTag("amount", request.Amount);

        var orderId = Guid.NewGuid().ToString();
        var cacheKey = $"order:{orderId}";

        try
        {
            // 1. Implementar idempotência com Redis
            using var idempotencyActivity = ActivitySource.StartActivity("CheckIdempotency");
            var idempotencyKey = $"idempotency:{request.UserId}:{request.Amount}:{request.Description.GetHashCode()}";

            var existingOrderId = await _redis.StringGetAsync(idempotencyKey);
            if (existingOrderId.HasValue)
            {
                CacheHitsCounter.Add(1, new KeyValuePair<string, object?>("cache_type", "idempotency"));
                _logger.LogInformation("Returning existing order due to idempotency: {OrderId}", existingOrderId);

                // Buscar order existente do cache ou DB
                var cachedOrder = await GetOrderFromCacheOrDb(existingOrderId!);
                if (cachedOrder != null)
                {
                    activity?.SetTag("idempotent", true);
                    return cachedOrder;
                }
            }
            else
            {
                CacheMissesCounter.Add(1, new KeyValuePair<string, object?>("cache_type", "idempotency"));
            }

            // 2. Verificar se usuário existe
            using var userActivity = ActivitySource.StartActivity("ValidateUser");
            if (!Guid.TryParse(request.UserId, out var userGuid))
            {
                throw new ArgumentException("Invalid UserId format");
            }

            var userExists = await _context.Users.AnyAsync(u => u.Id == userGuid);
            if (!userExists)
            {
                // Criar usuário se não existir (para o exemplo)
                var newUser = new User
                {
                    Id = userGuid,
                    Name = $"User {userGuid}",
                    Email = $"user{userGuid}@example.com"
                };
                _context.Users.Add(newUser);
                _logger.LogInformation("Created new user: {UserId}", userGuid);
            }

            // 3. Criar pedido no PostgreSQL
            using var postgresActivity = ActivitySource.StartActivity("SaveToPostgreSQL");
            var order = new Order
            {
                Id = Guid.Parse(orderId),
                UserId = userGuid,
                Amount = request.Amount,
                Description = request.Description,
                Status = "pending"
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            postgresActivity?.SetTag("order_id", orderId);

            _logger.LogInformation("Order saved to PostgreSQL: {OrderId}", orderId);

            // 4. Persistir no DynamoDB
            using var dynamoActivity = ActivitySource.StartActivity("SaveToDynamoDB");
            var dynamoItem = new Dictionary<string, AttributeValue>
            {
                ["id"] = new AttributeValue { S = orderId },
                ["user_id"] = new AttributeValue { S = request.UserId },
                ["amount"] = new AttributeValue { N = request.Amount.ToString() },
                ["description"] = new AttributeValue { S = request.Description },
                ["status"] = new AttributeValue { S = "pending" },
                ["created_at"] = new AttributeValue { S = DateTime.UtcNow.ToString("O") },
                ["ttl"] = new AttributeValue { N = ((DateTimeOffset)DateTime.UtcNow.AddDays(30)).ToUnixTimeSeconds().ToString() }
            };

            await _dynamoDB.PutItemAsync(new PutItemRequest
            {
                TableName = "orders",
                Item = dynamoItem
            });

            dynamoActivity?.SetTag("order_id", orderId);
            _logger.LogInformation("Order saved to DynamoDB: {OrderId}", orderId);

            // 5. Cache no Redis
            using var cacheActivity = ActivitySource.StartActivity("SaveToCache");
            var orderResponse = new OrderResponse(
                orderId,
                request.UserId,
                request.Amount,
                request.Description,
                DateTime.UtcNow,
                "pending"
            );

            var serializedOrder = JsonSerializer.Serialize(orderResponse);
            await _redis.StringSetAsync(cacheKey, serializedOrder, TimeSpan.FromMinutes(30));
            await _redis.StringSetAsync(idempotencyKey, orderId, TimeSpan.FromMinutes(5));

            cacheActivity?.SetTag("cache_key", cacheKey);
            _logger.LogInformation("Order cached in Redis: {OrderId}", orderId);

            // 6. Enviar evento para SQS
            using var sqsActivity = ActivitySource.StartActivity("SendToSQS");
            var eventMessage = new
            {
                EventType = "OrderCreated",
                OrderId = orderId,
                UserId = request.UserId,
                Amount = request.Amount,
                Description = request.Description,
                Timestamp = DateTime.UtcNow,
                TraceId = Activity.Current?.TraceId.ToString(),
                SpanId = Activity.Current?.SpanId.ToString()
            };

            await _sqs.SendMessageAsync(new SendMessageRequest
            {
                QueueUrl = "http://localhost:4566/000000000000/test-queue",
                MessageBody = JsonSerializer.Serialize(eventMessage),
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    ["OrderId"] = new MessageAttributeValue { StringValue = orderId, DataType = "String" },
                    ["EventType"] = new MessageAttributeValue { StringValue = "OrderCreated", DataType = "String" },
                    ["TraceId"] = new MessageAttributeValue { StringValue = Activity.Current?.TraceId.ToString(), DataType = "String" }
                }
            });

            sqsActivity?.SetTag("queue", "test-queue");
            sqsActivity?.SetTag("message_id", eventMessage.OrderId);
            _logger.LogInformation("Event sent to SQS for order: {OrderId}", orderId);

            // Métricas customizadas
            OrdersCreatedCounter.Add(1,
                new KeyValuePair<string, object?>("status", "success"),
                new KeyValuePair<string, object?>("user_id", request.UserId));

            OrderAmountHistogram.Record(Convert.ToDouble(request.Amount),
                new KeyValuePair<string, object?>("currency", "USD"));

            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("order_created", true);

            _logger.LogInformation("Order creation completed successfully: {OrderId}", orderId);

            return orderResponse;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);

            OrdersCreatedCounter.Add(1,
                new KeyValuePair<string, object?>("status", "error"),
                new KeyValuePair<string, object?>("error_type", ex.GetType().Name));

            _logger.LogError(ex, "Error creating order: {ErrorMessage}", ex.Message);
            throw;
        }
    }

    private async Task<OrderResponse?> GetOrderFromCacheOrDb(string orderId)
    {
        using var activity = ActivitySource.StartActivity("GetOrderFromCache");

        // Tentar buscar no cache primeiro
        var cacheKey = $"order:{orderId}";
        var cachedOrder = await _redis.StringGetAsync(cacheKey);

        if (cachedOrder.HasValue)
        {
            CacheHitsCounter.Add(1, new KeyValuePair<string, object?>("cache_type", "order_lookup"));
            activity?.SetTag("cache_hit", true);
            return JsonSerializer.Deserialize<OrderResponse>(cachedOrder!);
        }

        CacheMissesCounter.Add(1, new KeyValuePair<string, object?>("cache_type", "order_lookup"));
        activity?.SetTag("cache_hit", false);

        // Se não encontrar no cache, buscar no banco
        if (Guid.TryParse(orderId, out var orderGuid))
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderGuid);
            if (order != null)
            {
                var response = new OrderResponse(
                    order.Id.ToString(),
                    order.UserId.ToString(),
                    order.Amount,
                    order.Description,
                    order.CreatedAt,
                    order.Status
                );

                // Cachear para próximas consultas
                var serialized = JsonSerializer.Serialize(response);
                await _redis.StringSetAsync(cacheKey, serialized, TimeSpan.FromMinutes(30));

                return response;
            }
        }

        return null;
    }
}