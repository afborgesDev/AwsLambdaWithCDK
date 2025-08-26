namespace PersonService.Shared.Options;

public class DynamoDbOptions
{
    public string PersonTable { get; set; } = string.Empty;
    public string OutboxTable { get; set; } = string.Empty;
    public string? EventBusName { get; set; }
}