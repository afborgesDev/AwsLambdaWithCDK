using Amazon.DynamoDBv2;
using Amazon.EventBridge;
using Amazon.Lambda.Annotations;
using Microsoft.Extensions.DependencyInjection;
using PersonService.Shared.Repositories;

namespace OutboxPublisherFunction;

[LambdaStartup]
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddAWSService<IAmazonDynamoDB>();
        services.AddAWSService<IAmazonEventBridge>();
        services.AddSingleton<IOutboxRepository, OutboxRepository>();
    }
}