using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.Elasticsearch;
using StackExchange.Redis;
using Amazon.DynamoDBv2;
using Amazon.SQS;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Amazon.Runtime;

var builder = WebApplication.CreateBuilder(args);

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://localhost:9200"))
    {
        IndexFormat = "minimal-api-logs-{0:yyyy.MM.dd}",
        AutoRegisterTemplate = true,
        NumberOfShards = 2,
        NumberOfReplicas = 1
    })
    .Enrich.WithProperty("Application", "MinimalApi")
    .CreateLogger();

builder.Host.UseSerilog();

// Configuração de serviços
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Entity Framework - PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL")
        ?? "Host=localhost;Database=app_db;Username=app_user;Password=app_password"));

// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    return ConnectionMultiplexer.Connect(configuration);
});
builder.Services.AddSingleton<IDatabase>(sp =>
    sp.GetService<IConnectionMultiplexer>()!.GetDatabase());

// AWS Services
builder.Services.AddSingleton<IAmazonDynamoDB>(sp =>
{
    var config = new AmazonDynamoDBConfig
    {
        ServiceURL = builder.Configuration["AWS:DynamoDB:ServiceURL"] ?? "http://localhost:8000",
        AuthenticationRegion = "us-east-1"
    };
    return new AmazonDynamoDBClient("dummy", "dummy", config);
});

builder.Services.AddSingleton<IAmazonSQS>(sp =>
{
    var config = new AmazonSQSConfig
    {
        ServiceURL = builder.Configuration["AWS:SQS:ServiceURL"] ?? "http://localhost:4566",
        AuthenticationRegion = "us-east-1"
    };
    return new AmazonSQSClient("dummy", "dummy", config);
});

// Business Services
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IValidator<CreateOrderRequest>, CreateOrderValidator>();

// Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("PostgreSQL")!)
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!)
    .AddDynamoDb(options =>
    {
        options.AccessKey = "dummy";
        options.SecretKey = "dummy";
        options.RegionEndpoint = Amazon.RegionEndpoint.USEast1;
    })
    .AddCheck<SQSHealthCheck>("sqs");

// OpenTelemetry
const string serviceName = "MinimalApi";
const string serviceVersion = "1.0.0";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName, serviceVersion)
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = builder.Environment.EnvironmentName,
            ["service.instance.id"] = Environment.MachineName
        }))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
        {
            options.RecordException = true;
            options.EnrichWithHttpRequest = (activity, request) =>
            {
                activity.SetTag("http.client_ip", request.HttpContext.Connection.RemoteIpAddress?.ToString());
                activity.SetTag("http.user_agent", request.Headers.UserAgent.FirstOrDefault());
            };
        })
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation(options =>
        {
            options.SetDbStatementForText = true;
            options.SetDbStatementForStoredProcedure = true;
        })
        .AddRedisInstrumentation()
        .AddSource("MinimalApi.Orders")
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("http://localhost:4317");
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddMeter("MinimalApi.Orders")
        .AddPrometheusExporter()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("http://localhost:4317");
        }));

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.MapHealthChecks("/health");
app.MapPrometheusScrapingEndpoint();

// Garantir que o banco existe
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await context.Database.EnsureCreatedAsync();
}

// Health Check Endpoint
app.MapGet("/health", async (IServiceProvider sp) =>
{
    var healthCheckService = sp.GetRequiredService<HealthCheckService>();
    var result = await healthCheckService.CheckHealthAsync();

    return Results.Ok(new
    {
        Status = result.Status.ToString(),
        Duration = result.TotalDuration.TotalMilliseconds,
        Checks = result.Entries.Select(e => new
        {
            Name = e.Key,
            Status = e.Value.Status.ToString(),
            Duration = e.Value.Duration.TotalMilliseconds,
            Description = e.Value.Description
        })
    });
}).WithTags("Health").WithOpenApi();

// Create Order Endpoint
app.MapPost("/orders", async (
    [FromBody] CreateOrderRequest request,
    IOrderService orderService,
    IValidator<CreateOrderRequest> validator,
    ILogger<Program> logger) =>
{
    using var activity = OrderService.ActivitySource.StartActivity("POST /orders");
    activity?.SetTag("order.user_id", request.UserId);
    activity?.SetTag("order.amount", request.Amount);

    // Validação
    var validationResult = await validator.ValidateAsync(request);
    if (!validationResult.IsValid)
    {
        activity?.SetStatus(ActivityStatusCode.Error, "Validation failed");
        logger.LogWarning("Validation failed for order creation: {Errors}",
            string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage)));

        return Results.BadRequest(validationResult.Errors.Select(e => e.ErrorMessage));
    }

    try
    {
        var result = await orderService.CreateOrderAsync(request);

        activity?.SetTag("order.id", result.Id);
        activity?.SetStatus(ActivityStatusCode.Ok);

        logger.LogInformation("Order created successfully: {OrderId}", result.Id);

        return Results.Created($"/orders/{result.Id}", result);
    }
    catch (Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        logger.LogError(ex, "Error creating order");

        return Results.Problem("An error occurred while creating the order");
    }
}).WithTags("Orders").WithOpenApi();

app.Run();

// DTOs
public record CreateOrderRequest(string UserId, decimal Amount, string Description);
public record OrderResponse(string Id, string UserId, decimal Amount, string Description, DateTime CreatedAt, string Status);

// Validation
public class CreateOrderValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("UserId is required");
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Amount must be greater than 0");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500).WithMessage("Description is required and must be less than 500 characters");
    }
}