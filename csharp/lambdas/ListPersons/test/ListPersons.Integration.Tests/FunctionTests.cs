using System.Net;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestUtilities;
using FluentAssertions;
using ListPerson;

namespace ListPersons.Integration.Tests;

public class FunctionTests: IClassFixture<IntegrationFixture>
{
    private readonly IntegrationFixture _fixture;

    public FunctionTests(IntegrationFixture fixture)
    {
        _fixture = fixture;
    }
    
    [Fact]
    public async Task GetPerson_ShouldReturnNoContent_WhenHasNoData()
    {
        var function = new Function(_fixture.PersonRepository);
        
        var request = new APIGatewayProxyRequest();

        var response = await function.FunctionHandler(request, new TestLambdaContext());
        
        response.StatusCode.Should().Be((int)HttpStatusCode.NoContent);
    }
    
    [Fact]
    public async Task GetPerson_ShouldReturnOKAndData_WhenHasData()
    {
        await _fixture.PersonRepository.CreateOneAsync(
            new() { FirstName = "Alexandre", LastName = "Borges" },
            CancellationToken.None
        );
        
        var function = new Function(_fixture.PersonRepository);
        
        var request = new APIGatewayProxyRequest();

        var response = await function.FunctionHandler(request, new TestLambdaContext());
        
        response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        response.Body.Should().Contain("\"FirstName\":\"Alexandre\"");
    }
    
}