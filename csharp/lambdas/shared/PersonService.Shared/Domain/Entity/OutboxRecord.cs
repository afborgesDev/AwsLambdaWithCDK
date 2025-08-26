namespace PersonService.Shared.Domain.Entity;

public class OutboxRecord
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string EventType { get; init; } = "PersonCreated";
    public string AggregateType { get; init; } = "Person";
    public string AggregateId { get; init; } = default!;
    public string PayloadJson { get; init; } = default!;
    public OutboxStatus Status { get; init; } = OutboxStatus.PENDING;
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string? IdempotencyKey { get; init; }
    public int Attempts { get; init; } = 0;
    public DateTimeOffset SentAt { get; init; } = DateTimeOffset.MinValue;
}

public enum OutboxStatus
{
    PENDING,
    COMPLETED,
    FAILED
}