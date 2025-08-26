using Amazon.CDK;
using Environment = Amazon.CDK.Environment;

namespace Infra;

sealed class Program
{
    public static void Main(string[] args)
    {
        var app = new App();
        var envName =  "dev";
        var awsEnv = new Environment {
            Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
            Region  = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_REGION") ?? "eu-west-1"
        };
        
        var dynamo = new DynamoStack(app, $"person-{envName}-dynamo", new() { Env = awsEnv });
        var appStack = new AppStack(app, $"person-{envName}-app", new()
        {
            Env = awsEnv,
            PersonsTable = dynamo.PersonsTable,
            OutboxTable = dynamo.OutboxTable,
            IdempotencyTable = dynamo.IdempotencyTable
        });
        
        appStack.AddDependency(dynamo);
        
        app.Synth();
    }
}