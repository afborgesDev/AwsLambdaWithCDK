using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Constructs;

namespace Infra;

public class DynamoStack : Stack
{
    private const string PersonTableName = "Persons";
    private const string OutboxTableName = "OutboxEvents";
    private const string IdepotencyTableName = "IdempotencyKeys";
    
    public Table PersonsTable { get; }
    public Table OutboxTable { get; }
    public Table IdempotencyTable { get; }

    public DynamoStack(Construct scope, string id, StackProps? props = null) : base(scope, id, props)
    {
        PersonsTable = new(this, PersonTableName, new TableProps
        {
            PartitionKey = new Attribute
            {
                Name = "Id",
                Type = AttributeType.STRING
            },
            RemovalPolicy = RemovalPolicy.RETAIN,
            TableName = PersonTableName,
        });
        
        OutboxTable = new(this, OutboxTableName, new TableProps
        {
            PartitionKey = new Attribute
            {
                Name = "Id",
                Type = AttributeType.STRING
            },
            RemovalPolicy = RemovalPolicy.RETAIN,
            TableName = OutboxTableName,
            Stream = StreamViewType.NEW_IMAGE,
            TimeToLiveAttribute = "ttl",
        });
        
        IdempotencyTable = new(this, IdepotencyTableName, new TableProps
        {
            PartitionKey = new Attribute
            {
                Name = "IdempotencyKey",
                Type = AttributeType.STRING
            },
            RemovalPolicy = RemovalPolicy.DESTROY,
            TableName = IdepotencyTableName,
            TimeToLiveAttribute = "ttl",
        });
    }
}