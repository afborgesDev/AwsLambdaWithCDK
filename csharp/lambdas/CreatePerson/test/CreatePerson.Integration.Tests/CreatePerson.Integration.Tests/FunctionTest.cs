using System.Text.Json;
using Amazon.XRay.Recorder.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CreatePerson.Integration.Tests;

public abstract class FunctionTest<TFunc>
{
    public FunctionTest()
    {
        AWSXRayRecorder.InitializeInstance();
        AWSXRayRecorder.Instance.BeginSegment("UnitTests");
    }
    
    protected IOptions<JsonSerializerOptions> JsonOptions { get; } =
        Options.Create(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    protected ILogger<TFunc> Logger { get; } = Substitute.For<ILogger<TFunc>>();
}