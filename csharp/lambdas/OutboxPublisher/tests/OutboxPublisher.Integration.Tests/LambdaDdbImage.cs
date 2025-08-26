using LAttr = Amazon.Lambda.DynamoDBEvents.DynamoDBEvent.AttributeValue;
using SdkAttr = Amazon.DynamoDBv2.Model.AttributeValue;

namespace OutboxPublisher.Integration.Tests;

public static class LambdaDdbImage
{
    public static Dictionary<string, LAttr> ToLambdaImage(Dictionary<string, SdkAttr> src) =>
        src.ToDictionary(kv => kv.Key, kv => Map(kv.Value));

    private static LAttr Map(SdkAttr a) =>
        a.S != null ? new() { S = a.S } :
        a.N != null ? new() { N = a.N } :
        a.BOOL.HasValue ? new() { BOOL = a.BOOL.Value } :
        a.NULL == true ? new() { NULL = true } :
        a.SS is { Count: > 0 } ? new() { SS = a.SS } :
        a.NS is { Count: > 0 } ? new() { NS = a.NS } :
        a.L is { Count: > 0 } ? new() { L = a.L.Select(Map).ToList() } :
        a.M is { Count: > 0 } ? new() { M = a.M.ToDictionary(p => p.Key, p => Map(p.Value)) } :
        new LAttr { NULL = true };
}