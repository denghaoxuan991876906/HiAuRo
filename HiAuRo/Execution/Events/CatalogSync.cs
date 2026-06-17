using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HiAuRo.Execution.Events;

public static class CatalogSync
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
        DefaultRequestHeaders = { { "User-Agent", "HiAuRo-CatalogSync/1.0" } }
    };

    /// <summary>上传 catalog JSON 到 GitHub 仓库</summary>
    /// <param name="catalogJson">完整 catalog JSON 字符串</param>
    /// <param name="repo">owner/repo 格式</param>
    /// <param name="branch">分支名</param>
    /// <param name="token">GitHub personal access token (repo scope)</param>
    /// <param name="onlyCloudSync">只上传 cloudSync=true 的类型</param>
    public static async Task<CatalogUploadResult> UploadToGitHubAsync(
        string catalogJson, string repo, string branch, string token,
        bool onlyCloudSync = false)
    {
        if (string.IsNullOrWhiteSpace(catalogJson) || string.IsNullOrWhiteSpace(token))
            return new CatalogUploadResult { Success = false, Message = "catalog 或 token 为空" };

        try
        {
            if (onlyCloudSync)
                catalogJson = FilterCloudSync(catalogJson);

            var content = Convert.ToBase64String(Encoding.UTF8.GetBytes(catalogJson));
            var filePath = "trigger-catalog.json";

            var sha = await GetExistingShaAsync(repo, filePath, branch, token);

            // GitHub Contents API：新建文件不传 sha，更新文件必须传当前 sha
            var bodyDict = new Dictionary<string, object?>
            {
                ["message"] = $"Update trigger catalog ({DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC)",
                ["content"] = content,
                ["branch"] = branch
            };
            if (sha != null)
                bodyDict["sha"] = sha;

            var url = $"https://api.github.com/repos/{repo}/contents/{filePath}";
            using var req = new HttpRequestMessage(HttpMethod.Put, url);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            req.Content = new StringContent(JsonSerializer.Serialize(bodyDict), Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(req);
            var respBody = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                var commitUrl = $"https://github.com/{repo}/commits/{branch}";
                return new CatalogUploadResult { Success = true, Message = "上传成功", CommitUrl = commitUrl };
            }

            return new CatalogUploadResult { Success = false, Message = $"GitHub API {(int)resp.StatusCode}: {respBody[..Math.Min(200, respBody.Length)]}" };
        }
        catch (Exception ex)
        {
            return new CatalogUploadResult { Success = false, Message = ex.Message };
        }
    }

    private static async Task<string?> GetExistingShaAsync(string repo, string filePath, string branch, string token)
    {
        var url = $"https://api.github.com/repos/{repo}/contents/{filePath}?ref={branch}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("sha", out var sha) ? sha.GetString() : null;
    }

    private static string FilterCloudSync(string catalogJson)
    {
        var node = JsonNode.Parse(catalogJson);
        if (node is null) return catalogJson;

        var root = node.AsObject();
        foreach (var key in new[] { "conditions", "actions", "scripts" })
        {
            if (!root.TryGetPropertyValue(key, out var val) || val is not JsonArray arr) continue;
            var filtered = new JsonArray();
            foreach (var item in arr)
            {
                var cloudSync = item?["cloudSync"];
                if (cloudSync is null || cloudSync.GetValue<bool>())
                    filtered.Add(item?.DeepClone());
            }
            root[key] = filtered;
        }

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    public sealed record CatalogUploadResult
    {
        public bool Success { get; init; }
        public string? Message { get; init; }
        public string? CommitUrl { get; init; }
    }
}
