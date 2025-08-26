using Amazon.Lambda.DynamoDBEvents;
using Amazon.Lambda.TestUtilities;
using FluentAssertions;
using OutboxPublisherFunction;
using PersonService.Shared.Mappers;

namespace OutboxPublisher.Integration.Tests;

public class FunctionTests : IClassFixture<IntegrationFixture>
{
    private readonly IntegrationFixture _fixture;

    public FunctionTests(IntegrationFixture fixture)
    {
        _fixture = fixture;
    }
    
    [Fact]
    public async Task PersonCreated_WritesOutbox_Publishes_EventBridge_And_MarksSent()
    {
        _ = await _fixture.PersonRepository.CreateOneAsync(new()
            {
                FirstName = "Alexandre",
                LastName = "Borges",
                PhoneNumber = "+31123456789",
                Address = "Somewhere 1"
            }, CancellationToken.None);
        

        var scan = await _fixture.DynamoDbClient.ScanAsync(new() { TableName = _fixture.OutboxTableName, Limit = 1 });
        scan.Items.Should().NotBeEmpty();
        var outbox = scan.Items.Single();
        var outboxId = outbox[OutboxMapper.Id].S;

        var img = LambdaDdbImage.ToLambdaImage(outbox);
        var ev = new DynamoDBEvent
        {
            Records = new List<DynamoDBEvent.DynamodbStreamRecord>
            {
                new() { Dynamodb = new() { NewImage = img } }
            }
        };

        var fn = new Function(_fixture.EventBridgeClient, _fixture.OutboxRepository);
        await fn.FunctionHandler(ev, new TestLambdaContext());

        var messages = await _fixture.ReceiveAllAsync();
        messages.Should().NotBeEmpty();
        messages.Any(m => m.Body.Contains("PersonCreated")).Should().BeTrue();

        var get = await _fixture.DynamoDbClient.GetItemAsync(
            _fixture.OutboxTableName,
            new() { [OutboxMapper.Id] = new() { S = outboxId } });
        
        get.IsItemSet.Should().BeTrue();
        get.Item[OutboxMapper.Status].S.Should().Be("SENT");
    }
}