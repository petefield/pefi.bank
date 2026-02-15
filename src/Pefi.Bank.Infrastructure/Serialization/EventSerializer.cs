using System.Text.Json;
using System.Text.Json.Serialization;
using Pefi.Bank.Domain;
using Pefi.Bank.Domain.Events;

namespace Pefi.Bank.Infrastructure.Serialization;

public static class EventSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly Dictionary<string, Type> EventTypeMap = new()
    {
        [nameof(CustomerCreated)] = typeof(CustomerCreated),
        [nameof(CustomerUpdated)] = typeof(CustomerUpdated),
        [nameof(AccountOpened)] = typeof(AccountOpened),
        [nameof(FundsDeposited)] = typeof(FundsDeposited),
        [nameof(FundsWithdrawn)] = typeof(FundsWithdrawn),
        [nameof(AccountClosed)] = typeof(AccountClosed),
        [nameof(TransferInitiated)] = typeof(TransferInitiated),
        [nameof(TransferCompleted)] = typeof(TransferCompleted),
        [nameof(TransferFailed)] = typeof(TransferFailed),
    };

    public static string Serialize(IEvent @event) =>
        JsonSerializer.Serialize(@event, @event.GetType(), Options);

    public static IEvent Deserialize(string eventType, string json)
    {
        if (!EventTypeMap.TryGetValue(eventType, out var type))
            throw new InvalidOperationException($"Unknown event type: {eventType}");

        return (IEvent)(JsonSerializer.Deserialize(json, type, Options)
            ?? throw new InvalidOperationException($"Failed to deserialize event: {eventType}"));
    }

    public static JsonSerializerOptions GetOptions() => Options;
}
