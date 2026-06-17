using System.Text.Json;
using HiAuRo.UI;

namespace HiAuRo.Authoring;

/// <summary>
/// 三轴时间线 Web 编辑器后端 —— 注册 WebSocket 消息处理器
/// 消息流: Web 前端 ←→ WebUiBridge ←→ 文件系统
/// </summary>
public sealed class AuthoringServer
{
    public static AuthoringServer Instance { get; } = new();

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private string ConfigDir => DService.Instance().PI.ConfigDirectory.FullName;

    private AuthoringServer() { }

    public void Register(WebUiBridge bridge)
    {
        bridge.On("editorCatalog", _data =>
        {
            var catalogPath = Path.Combine(ConfigDir, "trigger-catalog.json");
            string? json = null;
            string? error = null;
            try
            {
                if (File.Exists(catalogPath))
                    json = File.ReadAllText(catalogPath);
                else
                    error = "trigger-catalog.json 未生成 — 请先启动 HiAuRo";
            }
            catch (Exception ex) { error = ex.Message; }

            bridge.SendAsync(new { type = "editorCatalogResult", data = new { json, error } }).ContinueWith(
                    t => { if (t.Exception != null) DService.Instance().Log.Warning($"[Authoring] SendAsync 失败: {t.Exception.InnerException?.Message}"); },
                    TaskContinuationOptions.OnlyOnFaulted);
        });
    }

    private string GetTimelineDir(string axis) => axis switch
    {
        "fact" => Path.Combine(ConfigDir, "FactTimelines"),
        "assist" => Path.Combine(ConfigDir, "AssistTimelines"),
        _ => Path.Combine(ConfigDir, "ExecutionTimelines")
    };

    private static string? GetProp(JsonElement elem, string name) =>
        elem.TryGetProperty(name, out var val) ? val.GetString() : null;

    private static object GetDefaultTemplate(string axis) => axis switch
    {
        "fact" => new
        {
            name = "新事实轴",
            territoryId = 0,
            author = "",
            phases = Array.Empty<object>()
        },
        _ => new
        {
            Name = "新执行轴",
            TerritoryTypeId = 0,
            Note = "",
            ExposedVars = Array.Empty<string>(),
            TreeRoot = new
            {
                TypeName = "HiAuRo.Execution.TreeRoot, HiAuRo",
                DisplayName = "Root",
                Id = 1,
                Enable = true,
                Remark = "",
                Tag = "",
                Childs = Array.Empty<object>()
            }
        }
    };
}
