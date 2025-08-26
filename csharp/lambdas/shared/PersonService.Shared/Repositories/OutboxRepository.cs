using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Options;
using PersonService.Shared.Domain.Entity;
using PersonService.Shared.Mappers;
using PersonService.Shared.Options;

namespace PersonService.Shared.Repositories;

public interface IOutboxRepository
{
    TransactWriteItem BuildPutPending(OutboxRecord record);

    Task MarkSentAsync(string outboxId, DateTime utcNow, CancellationToken cancellationToken);
}
    
public class OutboxRepository : IOutboxRepository
{
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly IOptions<DynamoDbOptions> _options;

    public OutboxRepository(IAmazonDynamoDB dynamoDbClient, IOptions<DynamoDbOptions> options)
    {
        _dynamoDbClient = dynamoDbClient;
        _options = options;
    }

    public TransactWriteItem BuildPutPending(OutboxRecord record)
    {
        return new()
        {
            Put = new()
            {
                TableName = _options.Value.OutboxTable,
                Item = OutboxMapper.MapToAttributes(record),
                ConditionExpression = "attribute_not_exists(#pk)",
                ExpressionAttributeNames = new() { ["#pk"] = OutboxMapper.Id }
            }
        };
    }
    
    public Task MarkSentAsync(string outboxId, DateTime utcNow, CancellationToken cancellationToken)
    {
        return _dynamoDbClient.UpdateItemAsync(
            new()
            {
                TableName = _options.Value.OutboxTable,
                Key = new() { [OutboxMapper.Id] = new() { S = outboxId } },
                ConditionExpression = "#s = :pending",
                UpdateExpression = "SET #s = :sent, #sentAt = :now, #attempts = if_not_exists(#attempts, :z) + :one",
                ExpressionAttributeNames =
                    new()
                    {
                        ["#s"] = OutboxMapper.Status,
                        ["#sentAt"] = OutboxMapper.SentAt,
                        ["#attempts"] = OutboxMapper.Attempts
                    },
                ExpressionAttributeValues =
                    new()
                    {
                        [":pending"] = new() { S = "PENDING" },
                        [":sent"] = new() { S = "SENT" },
                        [":now"] = new() { S = utcNow.ToString("O") },
                        [":z"] = new() { N = "0" },
                        [":one"] = new() { N = "1" }
                    }
            },
            cancellationToken
        );
    }
}