using System.Net;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using FluentAssertions;
using ListPerson;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PersonService.Shared.Domain.Entity;
using PersonService.Shared.Repositories;
using Xunit;

namespace ListPersons.Unit.Tests;

public class FunctionTests
{
    private readonly IPersonRepository _personRepository = Substitute.For<IPersonRepository>();
    private readonly ILambdaContext _context = Substitute.For<ILambdaContext>();
    private readonly Function _function;

    public FunctionTests()
    {
        _function = new(_personRepository);
    }
    
    [Fact]
    public async Task ListPerson_ShouldReturnInternalServerError_WhenAnErrorHappen()
    {
        _personRepository.GetAllAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new Exception("x"));
        
        var req = new APIGatewayProxyRequest();
        
        var result = await _function.FunctionHandler(req, _context);
        
        result.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
    }
    
    [Fact]
    public async Task ListPerson_ShouldReturnBodyAndOkStatus_WhenSuccess()
    {
        
        var expected = new PersonModel { FirstName = "Alexandre", LastName = "Borges" };
        _personRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([expected]);
        
        var req = new APIGatewayProxyRequest ();
        var result = await _function.FunctionHandler(req, _context);
        
        result.StatusCode.Should().Be((int)HttpStatusCode.OK);
        result.Body.Should().Contain("\"FirstName\":\"Alexandre\"");
        await _personRepository.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }
}