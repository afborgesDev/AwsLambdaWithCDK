using Amazon.CDK;
using Amazon.CDK.Assertions;
using Infra;
using Environment = Amazon.CDK.Environment;

namespace Lambdas.Infra.Unit.Tests;

public class DynamoStackTests
{
    private static Template Build()
    {
        var app = new App();
        var environment = new Environment { Account = "111111111111", Region = "eu-west-1" };
        var dynamoStack = new DynamoStack(app, "test-dynamo", new() { Env = environment });
        return Template.FromStack(dynamoStack);
    }
    
    [Fact]
    public void DynamoInfra_ShouldBeUpToCreatesTables_WhenInfraIsUp()
    {
        const int _expectedTables = 3;
        var template = Build();

        template.ResourceCountIs("AWS::DynamoDB::Table", _expectedTables);

        template.HasResourceProperties("AWS::DynamoDB::Table", new Dictionary<string, object> {
            ["TableName"] = "Persons",
            ["KeySchema"] = new object[] { new Dictionary<string, object> { ["AttributeName"] = "Id", ["KeyType"] = "HASH" } },
            ["AttributeDefinitions"] = new object[] { new Dictionary<string, object> { ["AttributeName"] = "Id", ["AttributeType"] = "S" } }
        });

        template.HasResourceProperties("AWS::DynamoDB::Table", new Dictionary<string, object> {
            ["TableName"] = "OutboxEvents",
            ["StreamSpecification"] = new Dictionary<string, object> { ["StreamViewType"] = "NEW_IMAGE" },
            ["TimeToLiveSpecification"] = new Dictionary<string, object> { ["Enabled"] = true, ["AttributeName"] = "ttl" }
        });

        template.HasResourceProperties("AWS::DynamoDB::Table", new Dictionary<string, object> {
            ["TableName"] = "IdempotencyKeys",
            ["TimeToLiveSpecification"] = new Dictionary<string, object> { ["Enabled"] = true, ["AttributeName"] = "ttl" }
        });
    }

    [Fact]
    public void DynamoInfra_ShouldSetupCorrectDeletionPolicyPerTable_WhenInfraIsUp()
    {
        var template = Build();

        template.HasResource("AWS::DynamoDB::Table", new Dictionary<string, object> {
            ["DeletionPolicy"] = "Retain",
            ["UpdateReplacePolicy"] = "Retain",
            ["Properties"] = new Dictionary<string, object> { ["TableName"] = "Persons" }
        });

        template.HasResource("AWS::DynamoDB::Table", new Dictionary<string, object> {
            ["DeletionPolicy"] = "Retain",
            ["UpdateReplacePolicy"] = "Retain",
            ["Properties"] = new Dictionary<string, object> { ["TableName"] = "OutboxEvents" }
        });

        template.HasResource("AWS::DynamoDB::Table", new Dictionary<string, object> {
            ["DeletionPolicy"] = "Delete",
            ["UpdateReplacePolicy"] = "Delete",
            ["Properties"] = new Dictionary<string, object> { ["TableName"] = "IdempotencyKeys" }
        });
    }
}