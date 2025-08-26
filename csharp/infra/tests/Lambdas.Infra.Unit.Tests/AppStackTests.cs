using Amazon.CDK;
using Amazon.CDK.Assertions;
using Infra;
using Environment = Amazon.CDK.Environment;

namespace Lambdas.Infra.Unit.Tests;

public class AppStackTests
{
    private static Template Build()
    {
        var app = new App();
        var env = new Environment { Account = "111111111111", Region = "eu-west-1" };

        var dynamo = new DynamoStack(app, "dyn", new() { Env = env });
        var appStack = new AppStack(app, "app", new()
        {
            Env = env,
            PersonsTable = dynamo.PersonsTable,
            OutboxTable  = dynamo.OutboxTable,
            IdempotencyTable = dynamo.IdempotencyTable
        });
        appStack.AddDependency(dynamo);

        return Template.FromStack(appStack);
    }
    
    [Fact]
    public void Lambdas_ShouldHaveDynamoDBStreamEVentSource_WhenUp()
    {
        var template = Build();

        template.HasResourceProperties("AWS::Lambda::EventSourceMapping", new Dictionary<string, object> {
            ["StartingPosition"] = "LATEST",
            ["BatchSize"] = 10
        });

        template.HasResourceProperties("AWS::Lambda::EventSourceMapping", new Dictionary<string, object> {
            ["EventSourceArn"] = Match.ObjectLike(new Dictionary<string, object> {
                ["Fn::ImportValue"] = Match.AnyValue()
            })
        });
    }

    [Fact]
    public void Lambdas_ShouldTargetsSqsCorrectly_WhenUp()
    {
        var template = Build();

        template.HasResourceProperties("AWS::Events::Rule", new Dictionary<string, object> {
            ["EventPattern"] = Match.ObjectLike(new Dictionary<string, object> {
                ["detail-type"] = Match.ArrayWith(["PersonCreated"]),
                ["source"] = Match.ArrayWith(["person.service"])
            }),
            ["Targets"] = Match.ArrayWith(
            [
                Match.ObjectLike(new Dictionary<string, object> {
                    ["Arn"] = Match.AnyValue()
                }),
            ]
            )
        });
    }

    [Fact]
    public void Lambdas_ShouldHaveExpectedEnvironmentVariables_WhenUp()
    {
        var template = Build();

        AssertFn("Person-CreateFunction");
        AssertFn("Person-ListFunction");
        AssertFn("Person-outbox-publisher");
        
        void AssertFn(string functionName)
        {
            template.HasResourceProperties("AWS::Lambda::Function",
                Match.ObjectLike(new Dictionary<string, object> {
                    ["FunctionName"] = functionName,
                    ["PackageType"] = "Image",
                    ["Environment"] = Match.ObjectLike(new Dictionary<string, object> {
                        ["Variables"] = Match.ObjectLike(new Dictionary<string, object> {
                            ["TABLE_PERSONS"] = Match.AnyValue(),
                            ["TABLE_OUTBOX"] = Match.AnyValue(),
                            ["TABLE_IDEMPOTENCY"] = Match.AnyValue(),
                            ["EVENT_BUS"] = Match.AnyValue(),
                            ["REQUIRE_IDEMPOTENCY_KEY"] = "true",
                            ["LOG_LEVEL"] = "Info",
                        })
                    })
                })
            );
        }
    }
}