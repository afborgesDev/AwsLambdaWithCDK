using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.EventSources;
using Amazon.CDK.AWS.SQS;
using Constructs;
using EventBus = Amazon.CDK.AWS.Events.EventBus;
using EventBusProps = Amazon.CDK.AWS.Events.EventBusProps;
using IResource = Amazon.CDK.AWS.APIGateway.IResource;
using Stack = Amazon.CDK.Stack;

namespace Infra;

public class AppStackProps : StackProps
{
    public required Table PersonsTable { get; init; }
    public required Table OutboxTable { get; init; }
    public required Table IdempotencyTable { get; init; }
}

public class AppStack : Stack
{
    private const string Env = "dev";
    private const string appStackPrefix = "Person";
    private const int DefaultMemSize = 256;
    private readonly Duration _lambdaTimeout = Duration.Seconds(15);
    
    public AppStack(Construct scope, string id, AppStackProps props) : base(scope, id, props)
    {
        var bus = new EventBus(this, $"{appStackPrefix}-{Env}", new EventBusProps
        {
            EventBusName = $"{appStackPrefix}-{Env}-bus"
        });

        var demoQueue = new Queue(this, $"{appStackPrefix}-CreatedDemoQueue", new QueueProps
        {
            QueueName = $"{appStackPrefix}-CreatedDemoQueue",
            RetentionPeriod = Duration.Days(4)
        });
        
        _ = new Rule(this, $"{appStackPrefix}-CreatedToSqs", new RuleProps {
            EventBus = bus,
            EventPattern = new EventPattern { DetailType = ["PersonCreated"], Source = ["person.service"] },
            Targets = [new SqsQueue(demoQueue)]
        });
        
        var createFunction = new DockerImageFunction(this, $"{appStackPrefix}-CreateFunction", new DockerImageFunctionProps
        {
            FunctionName = $"{appStackPrefix}-CreateFunction",
            Code = BuildDockerImageCode("CreatePerson"),
            MemorySize = DefaultMemSize,
            Timeout = _lambdaTimeout,
        });
        
        var listFunction = new DockerImageFunction(this, $"{appStackPrefix}-ListFunction", new DockerImageFunctionProps {
            FunctionName = $"{appStackPrefix}--list",
            Code = BuildDockerImageCode("ListPerson"),
            Timeout = _lambdaTimeout,
            MemorySize = DefaultMemSize
        });
        
        var outBoxFunction = new DockerImageFunction(this, $"{appStackPrefix}-OutboxFunction", new DockerImageFunctionProps {
            FunctionName = $"{appStackPrefix}-outbox-publisher",
            Code = BuildDockerImageCode("OutboxPublisher"),
            Timeout = Duration.Seconds(30),
            MemorySize = DefaultMemSize
        });
        
        var lambdaEnv = new Dictionary<string,string> {
            ["TABLE_PERSONS"] = props.PersonsTable.TableName,
            ["TABLE_OUTBOX"]  = props.OutboxTable.TableName,
            ["TABLE_IDEMPOTENCY"] = props.IdempotencyTable.TableName,
            ["BUS_ARN"] = bus.EventBusArn,
            ["REQUIRE_IDEMPOTENCY_KEY"] = "true",
            ["LOG_LEVEL"] = "Info"
        };
        
        SetupEnvironments(lambdaEnv, createFunction);
        SetupEnvironments(lambdaEnv, listFunction);
        SetupEnvironments(lambdaEnv, outBoxFunction);
        
        props.PersonsTable.GrantReadWriteData(createFunction);
        props.OutboxTable.GrantReadWriteData(createFunction);
        props.IdempotencyTable.GrantReadWriteData(createFunction);

        props.PersonsTable.GrantReadData(listFunction);
        
        props.OutboxTable.GrantStreamRead(outBoxFunction);
        bus.GrantPutEventsTo(outBoxFunction);
        
        var streamSource = new DynamoEventSource(props.OutboxTable, new DynamoEventSourceProps {
            BatchSize = 10,
            StartingPosition = StartingPosition.LATEST,
            BisectBatchOnError = true,
            RetryAttempts = 3,
            Filters = [FilterCriteria.Filter(new Dictionary<string, object> { { "eventName", new [] { "INSERT" } } })],
            ReportBatchItemFailures = true
        });
        outBoxFunction.AddEventSource(streamSource);
        
        var api = new RestApi(this, $"{appStackPrefix}Api", new RestApiProps {
            RestApiName = $"{appStackPrefix}-api"
        });

        var persons = api.Root.AddResource("persons");
        persons.AddMethod("POST", new LambdaIntegration(createFunction));
        persons.AddMethod("GET", new LambdaIntegration(listFunction));
        AddCorsOptions(persons);
        
        _ = new CfnOutput(this, "ApiUrl", new CfnOutputProps { Value = api.Url });
        _ = new CfnOutput(this, "EventBusArn", new CfnOutputProps { Value = bus.EventBusArn });
        _ = new CfnOutput(this, "PersonsTable", new CfnOutputProps { Value = props.PersonsTable.TableName });
        _ = new CfnOutput(this, "OutboxTable", new CfnOutputProps { Value = props.OutboxTable.TableName });
        _ = new CfnOutput(this, "IdempotencyTable", new CfnOutputProps { Value = props.IdempotencyTable.TableName });
    }
    
    private static DockerImageCode BuildDockerImageCode(string dockerImageName) =>
        DockerImageCode.FromImageAsset(
            "Lambdas", 
            new AssetImageCodeProps { File = $"{dockerImageName}/Dockerfile" });
    
    private static void AddCorsOptions(IResource apiResource)
    {
        apiResource.AddMethod("OPTIONS",
            new MockIntegration(new IntegrationOptions {
                PassthroughBehavior = PassthroughBehavior.NEVER,
                RequestTemplates = new Dictionary<string, string> { ["application/json"] = "{\"statusCode\": 200}" },
                IntegrationResponses =
                [
                    new IntegrationResponse {
                        StatusCode = "200",
                        ResponseParameters = new Dictionary<string,string> {
                            ["method.response.header.Access-Control-Allow-Headers"] = "'Content-Type,X-Amz-Date,Authorization,X-Api-Key,X-Amz-Security-Token'",
                            ["method.response.header.Access-Control-Allow-Methods"] = "'OPTIONS,GET,POST'",
                            ["method.response.header.Access-Control-Allow-Origin"]  = "'*'",
                            ["method.response.header.Access-Control-Allow-Credentials"] = "'false'"
                        }
                    },
                ]
            }),
            new MethodOptions {
                MethodResponses =
                [
                    new MethodResponse {
                        StatusCode = "200",
                        ResponseParameters = new Dictionary<string,bool> {
                            ["method.response.header.Access-Control-Allow-Headers"] = true,
                            ["method.response.header.Access-Control-Allow-Methods"] = true,
                            ["method.response.header.Access-Control-Allow-Origin"]  = true,
                            ["method.response.header.Access-Control-Allow-Credentials"] = true
                        }
                    },
                ]
            });
    }
    
    private static void SetupEnvironments(Dictionary<string, string> lambdaEnv, Function lambda)
    {
        foreach (var (key, value) in lambdaEnv)
        {
            lambda.AddEnvironment(key, value);
        }
    }
}