using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Options;
using PersonService.Shared.Domain.Entity;
using PersonService.Shared.Mappers;
using PersonService.Shared.Options;

namespace PersonService.Shared.Repositories;

public interface IPersonRepository
{
    Task<PersonModel> CreateOneAsync(PersonModel item, CancellationToken cancellationToken);
    Task DeleteOneAsync(string id, CancellationToken cancellationToken);
    Task<IList<PersonModel>> GetAllAsync(CancellationToken cancellationToken);
    Task<PersonModel> GetOneAsync(string id, CancellationToken cancellationToken);
    Task UpdateAsync(PersonModel item, CancellationToken cancellationToken);
}

public sealed class PersonRepository : IPersonRepository
{
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IOptions<DynamoDbOptions> _options;

    public PersonRepository(
        IAmazonDynamoDB dynamoDbClient, 
        IOutboxRepository outboxRepository, 
        IOptions<DynamoDbOptions> options)
    {
        _dynamoDbClient = dynamoDbClient;
        _outboxRepository = outboxRepository;
        _options = options;
    }

    public async Task<PersonModel> CreateOneAsync(PersonModel item, CancellationToken cancellationToken)
    {
        item.Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id;

        var personPut = new TransactWriteItem
        {
            Put =
                new()
                {
                    TableName = _options.Value.PersonTable,
                    Item = PersonMapper.MapToAttributes(item),
                    ConditionExpression = "attribute_not_exists(#id)",
                    ExpressionAttributeNames =
                        new()
                        {
                            ["#id"] = PersonMapper.Id
                        }
                }
        };
        
        var outboxEvent = new
        {
            personId = item.Id,
            firstName = item.FirstName,
            lastName = item.LastName,
            phoneNumber = item.PhoneNumber,
            address = item.Address
        };
        
        var outbox = new OutboxRecord
        {
            AggregateId = item.Id,
            PayloadJson = JsonSerializer.Serialize(outboxEvent)
        };
        
        try
        {
            await _dynamoDbClient.TransactWriteItemsAsync(new()
            {
                TransactItems = [personPut, _outboxRepository.BuildPutPending(outbox)],
            }, cancellationToken);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

        return await GetOneAsync(item.Id, cancellationToken);
    }

    public Task DeleteOneAsync(string id, CancellationToken cancellationToken)
    {
        return _dynamoDbClient.DeleteItemAsync(
            _options.Value.PersonTable,
            new(capacity: 1) { [PersonMapper.Id] = new(id) },
            cancellationToken);
    }

    public async Task<IList<PersonModel>> GetAllAsync(CancellationToken cancellationToken)
    {
        var scan = await _dynamoDbClient.ScanAsync(new()
        {
            TableName = _options.Value.PersonTable
        }, cancellationToken);

        return scan.Items.Select(PersonMapper.MapToModel).ToList();
    }

    public async Task<PersonModel> GetOneAsync(string id, CancellationToken cancellationToken)
    {
        var resp = await _dynamoDbClient.GetItemAsync(
            _options.Value.PersonTable,
            new(capacity: 1) { [PersonMapper.Id] = new(id) }, cancellationToken);

        if (!resp.IsItemSet || resp.Item is null || resp.Item.Count == 0)
            throw new KeyNotFoundException($"Person '{id}' not found.");

        return PersonMapper.MapToModel(resp.Item);
    }

    public Task UpdateAsync(PersonModel item, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(item.Id))
            throw new ArgumentException("Item.Id is required for update.", nameof(item));

        var put = new PutItemRequest
        {
            TableName = _options.Value.PersonTable,
            Item = PersonMapper.MapToAttributes(item),
            ConditionExpression = "attribute_exists(#id)",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#id"] = PersonMapper.Id
            }
        };
        
        return _dynamoDbClient.PutItemAsync(put, cancellationToken);
    }
}
