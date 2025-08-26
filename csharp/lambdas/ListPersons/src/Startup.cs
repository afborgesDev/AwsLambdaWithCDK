using Amazon.DynamoDBv2;
using Amazon.Lambda.Annotations;
using Microsoft.Extensions.DependencyInjection;
using PersonService.Shared.Repositories;

namespace ListPerson;

[LambdaStartup]
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddAWSService<IAmazonDynamoDB>();
        services.AddSingleton<IPersonRepository, PersonRepository>();
        services.AddSingleton<IOutboxRepository, OutboxRepository>();
    }
}