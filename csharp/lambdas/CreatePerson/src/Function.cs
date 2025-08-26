using System.Text.Json;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using PersonService.Shared.Domain.Entity;
using PersonService.Shared.Extensions;
using PersonService.Shared.Repositories;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace CreatePerson;

public class Function
{
    private readonly APIGatewayProxyResponse _badRequestResponse = new() { StatusCode = 400, Body = "Invalid input" };
    private readonly IPersonRepository _personRepository;

    public Function(IPersonRepository personRepository)
    {
        _personRepository = personRepository;
    }
    
    [LambdaFunction]
    [RestApi(LambdaHttpMethod.Post, "/persons")]
    public async Task<APIGatewayProxyResponse> FunctionHandler(
        APIGatewayProxyRequest request,
        ILambdaContext context)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Body))
            {
                return _badRequestResponse;
            }
            
            var itemToCreate = JsonSerializer.Deserialize<PersonModel>(request.Body);
            if (itemToCreate == null)
            {
                return _badRequestResponse;
            }

            using var cts = context.GetCancellationTokenSource();
            var createdItem = await _personRepository.CreateOneAsync(itemToCreate, cts.Token);
            return new() { Body = JsonSerializer.Serialize(createdItem), StatusCode = 201 };
        }
        catch (JsonException e)
        {
            context.Logger.LogLine(e.Message);
            return _badRequestResponse;
        }
        catch (Exception e)
        {
            context.Logger.LogLine(e.Message);
            return new() {StatusCode = 500};
        }
    }
}
