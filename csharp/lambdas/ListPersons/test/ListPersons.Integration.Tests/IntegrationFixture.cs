using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Options;
using PersonService.Shared.Options;
using PersonService.Shared.Repositories;
using Testcontainers.DynamoDb;

namespace ListPersons.Integration.Tests;

public class IntegrationFixture : IAsyncLifetime, IDisposable
{
    private readonly DynamoDbContainer _container;
    public string ServiceUrl { get; private set; } = default!;
    public IAmazonDynamoDB DynamoDbClient { get; private set; } = default!;
    public string PersonsTableName => "Persons";
    
    public IPersonRepository PersonRepository { get; private set; }
    
    public IntegrationFixture()
    {
        _container = new DynamoDbBuilder()
                    .WithImage("amazon/dynamodb-local:latest")
                    .WithPortBinding(8000, true)
                    .Build();
    }
    
    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        ServiceUrl = $"http://localhost:{_container.GetMappedPublicPort(8000)}";
        DynamoDbClient = new AmazonDynamoDBClient(
            new BasicAWSCredentials("test", "test"),
            new AmazonDynamoDBConfig { ServiceURL = ServiceUrl });

        await EnsureTablesAsync();
        
        PersonRepository = new PersonRepository(DynamoDbClient, Options.Create(new DynamoDbOptions {PersonTable = "Persons"}));
    }
    
    private async Task EnsureTablesAsync()
    {
        var existing = await DynamoDbClient.ListTablesAsync();
        if (!existing.TableNames.Contains(PersonsTableName))
        {
            if (!await TableExistsAsync(DynamoDbClient, PersonsTableName))
            {
                await DynamoDbClient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = PersonsTableName,
                    AttributeDefinitions = new()
                    {
                        new("Id", ScalarAttributeType.S),
                    },
                    KeySchema = new() { new KeySchemaElement("Id", KeyType.HASH) },
                    BillingMode = BillingMode.PAY_PER_REQUEST
                });
                await WaitForActiveAsync(DynamoDbClient, PersonsTableName, TimeSpan.FromSeconds(30));
            }
        }
    }
    
    private static async Task<bool> TableExistsAsync(IAmazonDynamoDB ddb, string tableName)
    {
        try
        {
            var resp = await ddb.DescribeTableAsync(new DescribeTableRequest { TableName = tableName });
            return resp.Table.TableStatus == TableStatus.ACTIVE || resp.Table.TableStatus == TableStatus.UPDATING;
        }
        catch (ResourceNotFoundException)
        {
            return false;
        }
    }

    private static async Task WaitForActiveAsync(IAmazonDynamoDB ddb, string tableName, TimeSpan timeout, int pollMs = 500)
    {
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < timeout)
        {
            try
            {
                var resp = await ddb.DescribeTableAsync(new DescribeTableRequest { TableName = tableName });
                var status = resp.Table.TableStatus;
                if (status == TableStatus.ACTIVE || status == TableStatus.UPDATING) return;
            }
            catch (ResourceNotFoundException) { /* still creating */ }
            await Task.Delay(pollMs);
        }
        throw new TimeoutException($"DynamoDB table '{tableName}' did not become ACTIVE within {timeout.TotalSeconds}s.");
    }

    public async Task DisposeAsync()
    {
        DynamoDbClient?.Dispose();
        await _container.StopAsync();
        await _container.DisposeAsync();
    }

    public void Dispose()
    {
    }
}