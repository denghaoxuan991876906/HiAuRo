using System.Text.Json;
using HiAuRo.Infrastructure;
using HiAuRo.Setting;
using OmenTools;

namespace HiAuRo;

/// <summary>Plugin 的配置加载与迁移（partial class 拆分自 Plugin.cs）</summary>
public partial class Plugin
{
    private PluginConfig LoadConfig()
    {
        var config = SettingMgr.GetMainSetting<PluginConfig>();
        if (config.LoadCount == 0)
        {
            // 试从 Dalamud 旧路径迁移
            try
            {
                var oldPath = Path.Combine(_pluginInterface.ConfigDirectory.FullName, "PluginConfig.json");
                if (File.Exists(oldPath))
                {
                    var oldJson = File.ReadAllText(oldPath);
                    var migrated = JsonSerializer.Deserialize<PluginConfig>(oldJson,
                        new JsonSerializerOptions { IncludeFields = true });
                    if (migrated is { LoadCount: > 0 }) { config = migrated; DService.Instance().Log.Information("[Config] 从 Dalamud 旧配置迁移"); }
                }
            }
            catch { }
        }

        PluginConfig.Instance = config;
        config.LoadCount++;

        config.BasicAcrSources ??= [];
        config.BasicAcrSources.RemoveAll(static source => source is null);
        if (config.BasicAcrSources.Count == 0
            && !string.IsNullOrWhiteSpace(config.BasicAcrScriptPath))
        {
            config.BasicAcrSources.Add(new BasicAcrSourceConfig
            {
                Path = config.BasicAcrScriptPath.Trim(),
                Enabled = true,
            });
        }
        config.BasicAcrScriptPath = null;
        config.Version = 3;

        if (config.Overlays?.Any(o => o.Name == "ActionPanel") == true)
            config.Overlays = config.Overlays.Where(o => o.Name != "ActionPanel")
                .Append(new OverlayWindowSetting { Name = "QtWindow", Url = "http://localhost:5678/qt.html", Width = 320, Height = 80 })
                .Append(new OverlayWindowSetting { Name = "HotkeyWindow", Url = "http://localhost:5678/hotkey.html", Width = 320, Height = 100 })
                .ToArray();

        var mw = config.Overlays?.FirstOrDefault(o => o.Name == "MainWindow");
        if (mw is { Height: < 100 }) { mw.Width = 310; mw.Height = 480; }

        config.Save();
        DService.Instance().Log.Information($"[Config] V={config.Version}, Load={config.LoadCount}, Debug={config.DebugEnabled}");
        return config;
    }
}
