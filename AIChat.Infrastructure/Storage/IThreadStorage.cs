using System.Text.Json;

namespace AIChat.Infrastructure.Storage;

public interface IThreadStorage
{
    Task<JsonElement?> LoadThreadAsync(string threadId, CancellationToken ct = default);
    Task SaveThreadAsync(string threadId, JsonElement threadData, CancellationToken ct = default);
    Task<bool> ThreadExistsAsync(string threadId, CancellationToken ct = default);
    string CreateNewThreadId();
}