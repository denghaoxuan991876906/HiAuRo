using System.Text.Json;
using OmenTools.Dalamud;

namespace HiAuRo.Runtime.AcrDistribution;

public sealed class AcrSourceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _sourcesPath;

    public AcrSourceStore(string acrRoot)
    {
        Directory.CreateDirectory(acrRoot);
        _sourcesPath = Path.Combine(acrRoot, "Sources.json");
    }

    public List<AcrSourceRecord> Load()
    {
        if (!File.Exists(_sourcesPath))
            return [];

        try
        {
            var json = File.ReadAllText(_sourcesPath);
            if (string.IsNullOrWhiteSpace(json))
                return [];

            return JsonSerializer.Deserialize<List<AcrSourceRecord>>(json) ?? [];
        }
        catch (JsonException ex)
        {
            LogWarning($"[AcrSourceStore] Sources.json 解析失败，已回退为空列表: {ex.Message}");
            return [];
        }
    }

    public void Save(List<AcrSourceRecord> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var json = JsonSerializer.Serialize(sources, JsonOptions);
        AtomicWriteAllText(_sourcesPath, json);
    }

    private static void AtomicWriteAllText(string path, string content)
    {
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";

        try
        {
            File.WriteAllText(tempPath, content);

            if (File.Exists(path))
                File.Replace(tempPath, path, null);
            else
                File.Move(tempPath, path);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private static void LogWarning(string message)
    {
        try
        {
            if (!DService.IsInitialized)
                return;

            var log = DService.Instance().GetType().GetProperty("Log")?.GetValue(DService.Instance());
            log?.GetType().GetMethod("Warning", [typeof(string)])?.Invoke(log, [message]);
        }
        catch
        {
            // Logging must never break source loading in offline/test environments.
        }
    }
}
