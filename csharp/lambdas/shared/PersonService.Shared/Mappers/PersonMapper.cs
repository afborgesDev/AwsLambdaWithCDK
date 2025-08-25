using Amazon.DynamoDBv2.Model;
using PersonService.Shared.Domain.Entity;

namespace PersonService.Shared.Mappers;

public static class PersonMapper
{
    public const string Id = "Id";
    public const string FirstName = "FirstName";
    public const string LastName = "LastName";
    public const string PhoneNumber = "PhoneNumber";
    public const string Address = "Address";

    public static PersonModel MapToModel(Dictionary<string, AttributeValue> items) => new()
    {
        Id = items.TryGetValue(Id, out var id) ? id.S : null,
        FirstName = items.TryGetValue(FirstName, out var firstName) ? firstName.S : null,
        LastName = items.TryGetValue(LastName, out var lastName) ? lastName.S : null,
        PhoneNumber = items.TryGetValue(PhoneNumber, out var phoneNumber) ? phoneNumber.S : null,
        Address = items.TryGetValue(Address, out var address) ? address.S : null,
    };

    public static Dictionary<string, AttributeValue> MapToAttributes(PersonModel model)
    {
        var item = new Dictionary<string, AttributeValue>(5)
        {
            [Id] = new() { S = model.Id },
        };

        if (!string.IsNullOrWhiteSpace(model.FirstName))
            item[FirstName] = new() { S = model.FirstName };

        if (!string.IsNullOrWhiteSpace(model.LastName))
            item[LastName] = new() { S = model.LastName };

        if (!string.IsNullOrWhiteSpace(model.PhoneNumber))
            item[PhoneNumber] = new() { S = model.PhoneNumber };

        if (!string.IsNullOrWhiteSpace(model.Address))
            item[Address] = new() { S = model.Address };

        return item;
    }
}