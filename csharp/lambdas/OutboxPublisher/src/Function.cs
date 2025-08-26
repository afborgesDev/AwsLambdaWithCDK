using Amazon.DynamoDBv2.Model;
using Amazon.EventBridge;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using PersonService.Shared.Extensions;
using PersonService.Shared.Mappers;
using PersonService.Shared.Repositories;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace OutboxPublisherFunction;

public class Function
{
    private readonly IAmazonEventBridge _eventBridge;
    private readonly IOutboxRepository _outboxRepository;

    public Function(IAmazonEventBridge eventBridge, IOutboxRepository outboxRepository)
    {
        _eventBridge = eventBridge;
        _outboxRepository = outboxRepository;
    }

    [LambdaFunction]
    public async Task FunctionHandler(DynamoDBEvent evnt, ILambdaContext context)
    {
        foreach (var rec in evnt.Records.Where(r => r.Dynamodb?.NewImage != null))
        {
            var img = rec.Dynamodb.NewImage!;
            if (!img.TryGetValue(OutboxMapper.Status, out var status) || status.S != "PENDING")
                continue;

            var outboxId = img[OutboxMapper.Id].S;
            var eventType = img[OutboxMapper.EventType].S;
            var aggregateId = img[OutboxMapper.AggregateId].S;
            var payload = img[OutboxMapper.PayloadJson].S;

            var put = await _eventBridge.PutEventsAsync(new()
            {
                Entries =
                [
                    new()
                    {
                        EventBusName = Environment.GetEnvironmentVariable("EVENT_BUS") ?? "default",
                        DetailType = eventType,
                        Source = "person.service",
                        Detail = payload,
                        Resources = [aggregateId],
                        Time = DateTime.UtcNow
                    },
                ]
            });

            if (put.FailedEntryCount > 0)
            {
                context.Logger.LogLine(
                    $"PutEvents failed for {outboxId}: {put.Entries[0].ErrorCode} {put.Entries[0].ErrorMessage}");
                continue;
            }
            using var cts = context.GetCancellationTokenSource();
            try
            {
                await _outboxRepository.MarkSentAsync(outboxId, DateTime.UtcNow, cts.Token);
            }
            catch (ConditionalCheckFailedException e)
            {
                context.Logger.LogError(e, "Concurrency issue when updating outbox record {OutboxId}", outboxId);
            }
        }
    }
}