using Amazon.SQS;
using Microsoft.Extensions.Diagnostics.HealthChecks;

public class SQSHealthCheck : IHealthCheck
{
    private readonly IAmazonSQS _sqs;
    private readonly ILogger<SQSHealthCheck> _logger;

    public SQSHealthCheck(IAmazonSQS sqs, ILogger<SQSHealthCheck> logger)
    {
        _sqs = sqs;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _sqs.ListQueuesAsync(new Amazon.SQS.Model.ListQueuesRequest(), cancellationToken);

            var healthData = new Dictionary<string, object>
            {
                ["queues_count"] = response.QueueUrls.Count,
                ["queues"] = response.QueueUrls
            };

            _logger.LogDebug("SQS health check successful. Found {Count} queues", response.QueueUrls.Count);

            return HealthCheckResult.Healthy("SQS is responding", healthData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SQS health check failed");
            return HealthCheckResult.Unhealthy("SQS is not responding", ex);
        }
    }
}