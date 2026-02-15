
namespace Pefi.Bank.Functions.Extensions;

public static class StreamIdExtensions
{
    public static (string entityType, string entityId) ToEntityInfo(this string streamId)
    {
        try
        {
            var dashIndex = streamId.IndexOf('-');
            if (dashIndex == -1)
            {
                throw new ArgumentException("Invalid stream ID format", nameof(streamId));
            }

            var entityType = streamId[..dashIndex].ToLowerInvariant();
            var entityId = streamId[(dashIndex + 1)..].ToLowerInvariant();
            return (entityType, entityId);
        }
        catch (Exception ex)
        {
            // Log the error and rethrow or return a default value
            Console.WriteLine($"Failed to extract entity ID from stream ID: {streamId}. Error: {ex.Message}");
            throw;
        }
    }
}

