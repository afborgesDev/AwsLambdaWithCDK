using System.Net;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using FluentAssertions;
using PersonService.Shared.Domain.Entity;

namespace CreatePerson.Integration.Tests;

public class FunctionTests : IClassFixture<IntegrationFixture>
{
    private readonly IntegrationFixture _fixture;

    public FunctionTests(IntegrationFixture fixture)
    {
        _fixture = fixture;
    }
    
    [Fact]
    public async Task PostPerson_ShouldWriteIntoDB_WhenCorrectData()
    {
        var function = new Function(_fixture.PersonRepository);
        
        var input = new PersonModel
        {
            FirstName = "Alexandre",
            LastName = "Borges"
        };
        
        var request = new APIGatewayProxyRequest() {Body = JsonSerializer.Serialize(input)};

        var response = await function.FunctionHandler(request, new TestLambdaContext());
        
        response.StatusCode.Should().Be((int)HttpStatusCode.Created);
    }
}