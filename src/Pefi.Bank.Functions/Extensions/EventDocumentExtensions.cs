using System.Text.Json;
using Pefi.Bank.Infrastructure.EventStore;

namespace Pefi.Bank.Functions.Extensions
{
    public static class EventDocumentExtensions
    {

        public static T AsEvent<T>(this EventDocument eventDocument)
        {
            var evt = JsonSerializer.Deserialize<T>(eventDocument.Data);
            return evt is null
                ? throw new InvalidOperationException($"Failed to deserialize event data to {typeof(T).Name}")
                : evt;
        }

    }
}