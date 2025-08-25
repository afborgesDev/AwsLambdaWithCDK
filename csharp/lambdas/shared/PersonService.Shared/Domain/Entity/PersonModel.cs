using Amazon.DynamoDBv2.DataModel;

namespace PersonService.Shared.Domain.Entity;

[DynamoDBTable("Persons")]
public class PersonModel
{
    [DynamoDBHashKey("Id")]
    public string? Id { get; set; }
    
    [DynamoDBProperty("FirstName")]
    public string? FirstName { get; set; }
    
    [DynamoDBProperty("LastName")]
    public string? LastName { get; set; }
    
    [DynamoDBProperty("PhoneNumber")]
    public string? PhoneNumber { get; set; }
    
    [DynamoDBProperty("Address")]
    public string? Address { get; set; }
}