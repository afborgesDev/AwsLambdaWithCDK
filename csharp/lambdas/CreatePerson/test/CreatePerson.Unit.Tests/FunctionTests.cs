using System.Net;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PersonService.Shared.Domain.Entity;
using PersonService.Shared.Repositories;
using Xunit;

namespace CreatePerson.Unit.Tests;

public class FunctionTests
{
    private readonly IPersonRepository _personRepository = Substitute.For<IPersonRepository>();
    private readonly ILambdaContext _context = Substitute.For<ILambdaContext>();
    private readonly Function _function;
    
    public FunctionTests()
    {
        _function = new(_personRepository);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{invalid}")]   
    public async Task CreatePerson_ShouldReturnBadRequest_WhenModelIsNotCorrect(string? body)
    {
        var req = new APIGatewayProxyRequest { Body = body };
        
        var result = await _function.FunctionHandler(req, _context);
        
        result.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        await _personRepository.DidNotReceiveWithAnyArgs().CreateOneAsync(default!, CancellationToken.None);
    }
    
    [Fact]
    public async Task CreatePerson_ShouldReturnInternalServerError_WhenAnErrorHapen()
    {
        var input = new PersonModel { FirstName = "X", LastName = "Y" };
        _personRepository.CreateOneAsync(
            Arg.Any<PersonModel>(), Arg.Any<CancellationToken>()).ThrowsAsync(new Exception("x"));
        
        var req = new APIGatewayProxyRequest { Body = JsonSerializer.Serialize(input) };
        
        var result = await _function.FunctionHandler(req, _context);
        
        result.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
    }
    
    [Fact]
    public async Task CreatePerson_ShouldReturnBodyAndCreatesStatus_WhenSuccess()
    {
        var input = new PersonModel { FirstName = "Alexandre", LastName = "Borges" };
        var expected = new PersonModel { FirstName = "Alexandre", LastName = "Borges" };
        _personRepository.CreateOneAsync(Arg.Any<PersonModel>(), Arg.Any<CancellationToken>()).Returns(expected);
        
        var req = new APIGatewayProxyRequest { Body = JsonSerializer.Serialize(input) };
        var result = await _function.FunctionHandler(req, _context);
        
        result.StatusCode.Should().Be((int)HttpStatusCode.OK);
        result.Body.Should().Contain("\"FirstName\":\"Alexandre\"");
        await _personRepository.Received(1).CreateOneAsync(Arg.Any<PersonModel>(), Arg.Any<CancellationToken>());
    }
}