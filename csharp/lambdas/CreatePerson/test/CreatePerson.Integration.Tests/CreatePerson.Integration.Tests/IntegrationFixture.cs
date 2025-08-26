using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Options;
using PersonService.Shared.Options;
using PersonService.Shared.Repositories;
using Testcontainers.DynamoDb;

namespace CreatePerson.Integration.Tests;

public class IntegrationFixture : IAsyncLifetime, IDisposable
{
    private readonly DynamoDbContainer _container;
    private string ServiceUrl { get; set; } = default!;
    private IAmazonDynamoDB DynamoDbClient { get; set; } = default!;
    private string PersonsTableName => "Persons";
    private string OutboxTableName => "Outbox";
    
    public IPersonRepository PersonRepository { get; private set; }
    public IOutboxRepository OutboxRepository { get; private set; }
    
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
        
        var options = Options.Create(new DynamoDbOptions { PersonTable = PersonsTableName, OutboxTable = OutboxTableName});
        await EnsureTablesAsync();
        
        OutboxRepository = new OutboxRepository(DynamoDbClient, options);
        PersonRepository = new PersonRepository(DynamoDbClient, OutboxRepository, options);
    }
    
    private async Task EnsureTablesAsync()
    {
        var existing = await DynamoDbClient.ListTablesAsync();
        if (!existing.TableNames.Contains(PersonsTableName))
        {
            if (!await TableExistsAsync(DynamoDbClient, PersonsTableName))
            {
                await DynamoDbClient.CreateTableAsync(new()
                {
                    TableName = PersonsTableName,
                    AttributeDefinitions = [new("Id", ScalarAttributeType.S),],
                    KeySchema = [new("Id", KeyType.HASH)],
                    BillingMode = BillingMode.PAY_PER_REQUEST
                });
                await WaitForActiveAsync(DynamoDbClient, PersonsTableName, TimeSpan.FromSeconds(30));
            }
        }
        
        if (!existing.TableNames.Contains(OutboxTableName))
        {
            if (!await TableExistsAsync(DynamoDbClient, OutboxTableName))
            {
                await DynamoDbClient.CreateTableAsync(new()
                {
                    TableName = OutboxTableName,
                    AttributeDefinitions = [new("Id", ScalarAttributeType.S),],
                    KeySchema = [new("Id", KeyType.HASH)],
                    BillingMode = BillingMode.PAY_PER_REQUEST
                });
                await WaitForActiveAsync(DynamoDbClient, OutboxTableName, TimeSpan.FromSeconds(30));
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

    private static async Task WaitForActiveAsync(
        IAmazonDynamoDB ddb, string tableName, TimeSpan timeout, int pollMs = 500)
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
            catch (ResourceNotFoundException)
            {
            }
            await Task.Delay(pollMs);
        }
        throw new TimeoutException(
            $"DynamoDB table '{tableName}' did not become ACTIVE within {timeout.TotalSeconds}s.");
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