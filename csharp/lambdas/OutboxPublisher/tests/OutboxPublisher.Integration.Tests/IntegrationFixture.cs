using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Options;
using PersonService.Shared.Options;
using PersonService.Shared.Repositories;
using Testcontainers.LocalStack;
using ResourceNotFoundException = Amazon.DynamoDBv2.Model.ResourceNotFoundException;

namespace OutboxPublisher.Integration.Tests;

public class IntegrationFixture : IAsyncLifetime, IDisposable
{
    private const string PersonsTableName = "Persons";
    
    private const string BusName = "person-int-bus";
    private const string RuleName = "personcreated-rule";
    private const string QueueName = "personcreated-target";
    
    private readonly LocalStackContainer _localstack;
    private string Endpoint { get; set; } = default!;
    private IAmazonSQS SqsClient { get; set; } = default!;
    private string QueueUrl  { get; set; } = default!;
    private string QueueArn { get; set; } = default!;
    private string RuleArn { get; set; } = default!;
    
    public string OutboxTableName => "Outbox";
    public IAmazonDynamoDB DynamoDbClient { get; private set; } = default!;
    public IAmazonEventBridge EventBridgeClient { get; private set; } = default!;
    public IPersonRepository PersonRepository { get; private set; }
    public IOutboxRepository OutboxRepository { get; private set; }
    
    public IntegrationFixture()
    {
        _localstack = new LocalStackBuilder()
                     .WithImage("localstack/localstack:latest")
                     .WithEnvironment("SERVICES", "dynamodb,sqs,events")
                     .WithEnvironment("DEFAULT_REGION", "us-east-1")
                     .WithPortBinding(4566, true)
                     .WithEnvironment("HOSTNAME_EXTERNAL", "localhost")
                     .Build();
    }
    
    public async Task InitializeAsync()
    {
        await _localstack.StartAsync();

        Endpoint = $"http://localhost:{_localstack.GetMappedPublicPort(4566)}";
        var credentials = new BasicAWSCredentials("test", "test");
        
        DynamoDbClient = new AmazonDynamoDBClient(
            credentials, 
            new AmazonDynamoDBConfig { ServiceURL = Endpoint, AuthenticationRegion = "us-east-1" });
        
        EventBridgeClient = new AmazonEventBridgeClient(
            credentials, 
            new AmazonEventBridgeConfig { ServiceURL = Endpoint, AuthenticationRegion = "us-east-1" });
        
        SqsClient = new AmazonSQSClient(
            credentials, 
            new AmazonSQSConfig { ServiceURL = Endpoint, AuthenticationRegion = "us-east-1" });
        
        await EnsureTablesAsync();
        await EnsureBusRuleAndTargetAsync();

        Environment.SetEnvironmentVariable("EVENT_BUS", BusName);
        
        var opts = Options.Create(new DynamoDbOptions
        {
            PersonTable = PersonsTableName,
            OutboxTable = OutboxTableName,
            EventBusName = BusName
        });
        
        OutboxRepository = new OutboxRepository(DynamoDbClient, opts);
        PersonRepository = new PersonRepository(DynamoDbClient, OutboxRepository, opts);
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
        throw new TimeoutException($"DynamoDB table '{tableName}' did not become ACTIVE within {timeout.TotalSeconds}s.");
    }
    
    private async Task EnsureBusRuleAndTargetAsync()
    {
        _ = await EventBridgeClient.ListEventBusesAsync(new());
        try
        {
            await EventBridgeClient.CreateEventBusAsync(new() { Name = BusName });
        }
        catch (ResourceAlreadyExistsException)
        {
        }

        QueueUrl = (await SqsClient.CreateQueueAsync(new CreateQueueRequest { QueueName = QueueName })).QueueUrl;
        QueueArn = (await SqsClient.GetQueueAttributesAsync(QueueUrl, ["QueueArn"])).QueueARN;

        var putRule = await EventBridgeClient.PutRuleAsync(new()
        {
            Name = RuleName,
            EventBusName = BusName,
            EventPattern = "{\"detail-type\":[\"PersonCreated\"],\"source\":[\"person.service\"]}"
        });
        RuleArn = putRule.RuleArn;

        var policy = $$$"""
                        {
                          "Version": "2012-10-17",
                          "Statement": [{
                            "Sid": "AllowEventBridge",
                            "Effect": "Allow",
                            "Principal": { "Service": "events.amazonaws.com" },
                            "Action": "sqs:SendMessage",
                            "Resource": "{{{QueueArn}}}",
                            "Condition": { "ArnEquals": { "aws:SourceArn": "{{{RuleArn}}}" } }
                          }]
                        }
                        """;

        await SqsClient.SetQueueAttributesAsync(new()
        {
            QueueUrl = QueueUrl,
            Attributes = new() { ["Policy"] = policy }
        });

        await EventBridgeClient.PutTargetsAsync(new()
        {
            Rule = RuleName,
            EventBusName = BusName,
            Targets = new() { new() { Id = "sqs-target", Arn = QueueArn } }
        });
    }
    
    public async Task<List<Message>> ReceiveAllAsync(int maxBatches = 5)
    {
        if (SqsClient is null) throw new InvalidOperationException("SqsClient not initialized.");
        if (string.IsNullOrWhiteSpace(QueueUrl)) throw new InvalidOperationException("QueueUrl not initialized.");

        var msgs = new List<Message>();
        for (var i = 0; i < maxBatches; i++)
        {
            var r = await SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = QueueUrl,
                MaxNumberOfMessages = 10,
                WaitTimeSeconds = 1
            });

            if (r.Messages == null || r.Messages.Count == 0) 
                break;

            msgs.AddRange(r.Messages);
            foreach (var m in r.Messages)
            {
                await SqsClient.DeleteMessageAsync(QueueUrl, m.ReceiptHandle);
            }
        }
        return msgs;
    }
    
    public async Task DisposeAsync()
    {
        DynamoDbClient?.Dispose();
        EventBridgeClient?.Dispose();
        SqsClient?.Dispose();
        await _localstack.StopAsync();
        await _localstack.DisposeAsync();
    }

    public void Dispose()
    {
    }
}