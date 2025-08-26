using System.Text.Json;
using Amazon.DynamoDBv2.Model;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using Amazon.Lambda.DynamoDBEvents;
using Amazon.Lambda.TestUtilities;
using FluentAssertions;
using NSubstitute;
using OutboxPublisherFunction;
using PersonService.Shared.Mappers;
using PersonService.Shared.Repositories;
using LAttr = Amazon.Lambda.DynamoDBEvents.DynamoDBEvent.AttributeValue;

namespace OutboxPublisher.Unit.Tests;

public class FunctionTests
{
    private static Dictionary<string, LAttr> NewImage(
        string outboxId,
        string status = "PENDING",
        string eventType = "PersonCreated",
        string aggregateId = "p-1",
        object? payload = null)
    {
        payload ??= new { personId = aggregateId, firstName = "Alexandre" };
        return new()
        {
            [OutboxMapper.Id] = new() { S = outboxId },
            [OutboxMapper.Status] = new() { S = status },
            [OutboxMapper.EventType] = new() { S = eventType },
            [OutboxMapper.AggregateId] = new() { S = aggregateId },
            [OutboxMapper.PayloadJson] = new() { S = JsonSerializer.Serialize(payload) },
        };
    }

    private static DynamoDBEvent WithRecords(params Dictionary<string, LAttr>[] images)
    {
        var ev = new DynamoDBEvent { Records = new List<DynamoDBEvent.DynamodbStreamRecord>() };
        foreach (var img in images)
        {
            ev.Records.Add(new()
            {
                Dynamodb = new()
                    { NewImage = img }
            });
        }
        return ev;
    }
    
    [Fact]
    public async Task PendingRecord_PublishesEvent_AndMarksSent()
    {
        var eventBridge = Substitute.For<IAmazonEventBridge>();
        eventBridge.PutEventsAsync(Arg.Any<PutEventsRequest>(), Arg.Any<CancellationToken>())
          .Returns(new PutEventsResponse
           {
               FailedEntryCount = 0,
               Entries = [new() { EventId = Guid.NewGuid().ToString() }]
           });

        var outboxRepository = Substitute.For<IOutboxRepository>();
        var sut = new Function(eventBridge, outboxRepository);
        var ctx = new TestLambdaContext();

        var outboxId = "obx-123";
        var dynamoDbEvent = WithRecords(NewImage(outboxId));

        await sut.FunctionHandler(dynamoDbEvent, ctx);

        await eventBridge.Received(1).PutEventsAsync(Arg.Is<PutEventsRequest>(x =>
            x.Entries.Count == 1 &&
            x.Entries[0].DetailType == "PersonCreated" &&
            x.Entries[0].Source == "person.service" &&
            !string.IsNullOrWhiteSpace(x.Entries[0].Detail)
        ), Arg.Any<CancellationToken>());

        await outboxRepository.Received(1).MarkSentAsync(
            outboxId,
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
    }
    
    [Fact]
    public async Task NonPendingRecord_IsIgnored()
    {
        var eventBridge = Substitute.For<IAmazonEventBridge>();
        var outboxRepository = Substitute.For<IOutboxRepository>();
        var sut = new Function(eventBridge, outboxRepository);
        var ctx = new TestLambdaContext();

        var dynamoDbEvent = WithRecords(NewImage("obx-1", status: "SENT"));

        await sut.FunctionHandler(dynamoDbEvent, ctx);

        await eventBridge.DidNotReceiveWithAnyArgs().PutEventsAsync(default!);
        await outboxRepository.DidNotReceiveWithAnyArgs().MarkSentAsync(default!, default, default);
    }

    [Fact]
    public async Task EventBridgeFailure_DoesNotMarkSent()
    {
        var eventBridge = Substitute.For<IAmazonEventBridge>();
        eventBridge.PutEventsAsync(Arg.Any<PutEventsRequest>(), Arg.Any<CancellationToken>())
          .Returns(new PutEventsResponse
          {
              FailedEntryCount = 1,
              Entries = [new() { ErrorCode = "SomeError", ErrorMessage = "Boom" }]
          });

        var outboxRepository = Substitute.For<IOutboxRepository>();
        var sut = new Function(eventBridge, outboxRepository);
        var ctx = new TestLambdaContext();

        await sut.FunctionHandler(WithRecords(NewImage("obx-2")), ctx);

        await outboxRepository.DidNotReceiveWithAnyArgs().MarkSentAsync(default!, default, default);
    }

    [Fact]
    public async Task MarkSent_ConditionalCheck_IsSwallowed()
    {
        var eventBridge = Substitute.For<IAmazonEventBridge>();
        eventBridge.PutEventsAsync(Arg.Any<PutEventsRequest>(), Arg.Any<CancellationToken>())
          .Returns(new PutEventsResponse
          {
              FailedEntryCount = 0,
              Entries = [new() { EventId = Guid.NewGuid().ToString() }]
          });

        var outboxRepository = Substitute.For<IOutboxRepository>();
        outboxRepository.MarkSentAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new ConditionalCheckFailedException("race"));

        var sut = new Function(eventBridge, outboxRepository);
        var ctx = new TestLambdaContext();

        var action = async () => await sut.FunctionHandler(WithRecords(NewImage("obx-3")), ctx);

        await action.Should().NotThrowAsync();
        await outboxRepository.Received(1).MarkSentAsync("obx-3", Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MultipleRecords_OnlyPendingAreProcessed()
    {
        var eventBridge = Substitute.For<IAmazonEventBridge>();
        eventBridge.PutEventsAsync(Arg.Any<PutEventsRequest>(), Arg.Any<CancellationToken>())
          .Returns(new PutEventsResponse
          {
              FailedEntryCount = 0,
              Entries = [new() { EventId = Guid.NewGuid().ToString() }]
          });

        var outboxRepository = Substitute.For<IOutboxRepository>();
        var sut = new Function(eventBridge, outboxRepository);
        var ctx = new TestLambdaContext();

        var dynamoDbEvent = WithRecords(
            NewImage("p1", status: "PENDING"),
            NewImage("s1", status: "SENT"),
            NewImage("p2", status: "PENDING")
        );

        await sut.FunctionHandler(dynamoDbEvent, ctx);

        await eventBridge.Received(2).PutEventsAsync(Arg.Any<PutEventsRequest>(), Arg.Any<CancellationToken>());
        await outboxRepository.Received(2).MarkSentAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }
}