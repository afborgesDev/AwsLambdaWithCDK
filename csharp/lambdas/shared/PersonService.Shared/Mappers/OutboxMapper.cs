using Amazon.DynamoDBv2.Model;
using PersonService.Shared.Domain.Entity;

namespace PersonService.Shared.Mappers;

public class OutboxMapper
{
    public const string Id = "Id";
    public const string EventType = "eventType";
    public const string AggregateType = "aggregateType";
    public const string AggregateId = "aggregateId";
    public const string PayloadJson = "payloadJson";
    public const string Status = "status";
    public const string OccurredAtUtc = "occurredAtUtc";
    public const string SentAt = "sentAt";
    public const string IdempotencyKey = "idempotencyKey";
    public const string Attempts = "attempts";
    
    public static Dictionary<string, AttributeValue> MapToAttributes(OutboxRecord model)
    {
        var item = new Dictionary<string, AttributeValue>(5)
        {
            [Id] = new() { S = model.Id },
            [EventType] = new() { S = model.EventType },
            [AggregateType] = new() { S = model.AggregateType },
            [AggregateId] = new() { S = model.AggregateId },
            [PayloadJson] = new() { S = model.PayloadJson },
            [Status] = new() { S = model.Status.ToString() },
            [OccurredAtUtc] = new() { S = model.OccurredAtUtc.ToString("O") },
            [Attempts] = new() { N = model.Attempts.ToString() },
            [SentAt] = new() {S = model.SentAt.ToString("O")},

        };
        return item;
    }
}