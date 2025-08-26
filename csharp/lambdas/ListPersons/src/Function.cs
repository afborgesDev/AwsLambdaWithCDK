using System.Text.Json;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using PersonService.Shared.Extensions;
using PersonService.Shared.Repositories;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ListPerson;

public class Function
{
    private readonly IPersonRepository _personRepository;

    public Function(IPersonRepository personRepository)
    {
        _personRepository = personRepository;
    }
    
    [LambdaFunction]
    [RestApi(LambdaHttpMethod.Get, "/persons")]
    public async Task<APIGatewayProxyResponse> FunctionHandler(
        APIGatewayProxyRequest request, ILambdaContext context)
    {
        using var cts = context.GetCancellationTokenSource();
        try
        {
            var items = await _personRepository.GetAllAsync(cts.Token);
            if (items.Count == 0)
            {
                return new() { StatusCode = 204 };
            }
            
            return new() { Body = JsonSerializer.Serialize(items), StatusCode = 200 };
        }
        catch (Exception e)
        {
            context.Logger.LogLine(e.Message);
            return new() {StatusCode = 500};
        }
    }
}
