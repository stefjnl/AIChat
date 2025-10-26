using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AIChat.Infrastructure.Storage;

public class FileThreadStorage : IThreadStorage
{
    private readonly string _basePath;

    public FileThreadStorage(IOptions<StorageOptions> options)
    {
        _basePath = options.Value.ThreadsPath;
        Directory.CreateDirectory(_basePath);
    }

    public async Task<JsonElement?> LoadThreadAsync(string threadId, CancellationToken ct = default)
    {
        var filePath = GetThreadPath(threadId);
        if (!File.Exists(filePath))
            return null;

        var json = await File.ReadAllTextAsync(filePath, ct);
        return JsonDocument.Parse(json).RootElement;
    }

    public async Task SaveThreadAsync(string threadId, JsonElement threadData, CancellationToken ct = default)
    {
        var filePath = GetThreadPath(threadId);
        await File.WriteAllTextAsync(filePath, threadData.ToString(), ct);
    }

    public Task<bool> ThreadExistsAsync(string threadId, CancellationToken ct = default)
    {
        return Task.FromResult(File.Exists(GetThreadPath(threadId)));
    }

    public string CreateNewThreadId()
    {
        return Guid.NewGuid().ToString("N");
    }

    private string GetThreadPath(string threadId)
    {
        return Path.Combine(_basePath, $"{threadId}.json");
    }

    public async Task<IEnumerable<string>> ListThreadIdsAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(_basePath))
            return Array.Empty<string>();

        var files = Directory.GetFiles(_basePath, "*.json", SearchOption.TopDirectoryOnly);
        return files
            .Select(Path.GetFileNameWithoutExtension)
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .ToArray();
    }
}