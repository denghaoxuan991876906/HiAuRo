using System.Text.Json;
using HiAuRo.Runtime;
using HiAuRo.UI;
using OmenTools;

namespace HiAuRo;

/// <summary>Plugin 的 WebUiBridge IPC 路由与状态推送（partial class 拆分自 Plugin.cs）</summary>
public partial class Plugin
{
    /// <summary>安全地 fire-and-forget 一个异步操作，异常时记录日志</summary>
    internal static void SafeFire(Task task, string label = "")
    {
        _ = task.ContinueWith(t =>
        {
            if (t.IsFaulted)
                DService.Instance()?.Log.Warning($"[FireAndForget] {label} 异常: {t.Exception?.InnerException?.Message}");
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    private static void RegisterUiHandlers(WebUiBridge bridge)
    {
        bridge.On("toggleACR", _d =>
        {
            if (RuntimeCore.IsRunning) RuntimeCore.Stop();
            else RuntimeCore.Start();
            SafeFire(SendStatusState(), "SendStatusState");
        });

        bridge.On("pause", _d =>
        {
            HiAuRo.ACR.MainControlHelper.TogglePause();
            SafeFire(SendPauseState(), "SendPauseState");
        });

        bridge.On("saveACR", data =>
        {
            // Web 模式：接收前端控件值 → 通过绑定写回 settings 字段
            if (data != null && HiAuRo.Runtime.ACRLifecycle.UiBuilder != null)
            {
                try
                {
                    foreach (var prop in data.Value.EnumerateObject())
                    {
                        var val = prop.Value;
                        object raw = val.ValueKind switch
                        {
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            JsonValueKind.Number => val.GetDouble(),
                            _ => val.GetString() ?? ""
                        };
                        HiAuRo.UI.UiBuilderImpl.WriteBack(
                            HiAuRo.Runtime.ACRLifecycle.UiBuilder, prop.Name, raw);
                    }
                }
                catch (Exception ex) { DService.Instance().Log.Warning($"[UI] saveACR 写回失败: {ex.Message}"); }
            }
            HiAuRo.ACR.MainControlHelper.Save();
        });

        bridge.On("hotkey", data =>
        {
            if (data == null) { DService.Instance().Log.Warning("[UI] hotkey: data is null"); return; }
            var id = data.Value.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            if (id == null) { DService.Instance().Log.Warning("[UI] hotkey: id not found in data"); return; }
            if (!RuntimeCore.IsRunning) { DService.Instance().Log.Information($"[UI] hotkey: '{id}' ignored (ACR 未启动)"); return; }
            var all = HiAuRo.ACR.HotkeyHelper.GetAll();
            var match = all.FirstOrDefault(r => r.Id == id);
            if (match == null) { DService.Instance().Log.Warning($"[UI] hotkey: '{id}' not found in {all.Count} registered resolvers"); return; }
            var check = match.Check();
            if (check < 0) { DService.Instance().Log.Information($"[UI] hotkey: '{id}' blocked (Check={check})"); return; }
            DService.Instance().Log.Information($"[UI] hotkey: executing '{id}' ({match.Label}) Check={check}");
            // 技能执行必须走 Dalamud 主线程
            var hotkeyId = id;
            DService.Instance().Framework.RunOnFrameworkThread(() => HiAuRo.ACR.HotkeyHelper.ExecuteById(hotkeyId));
        });

        bridge.On("qttoggle", data =>
        {
            if (data == null) return;
            var id = data.Value.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            if (id != null) HiAuRo.ACR.QTHelper.Toggle(id);
        });

        bridge.On("setHkBinding", data =>
        {
            if (data == null) return;
            var id = data.Value.TryGetProperty("id", out var i1) ? i1.GetString() : null;
            var key = data.Value.TryGetProperty("key", out var i2) ? i2.GetString() : null;
            if (id != null && key != null) HiAuRo.ACR.HotkeyHelper.SetBinding(id, key);
        });

        bridge.On("saveUiSettings", data =>
        {
            if (data == null) { DService.Instance().Log.Debug("[UI] saveUiSettings: data is null"); return; }
            var json = data.Value.GetRawText();
            DService.Instance().Log.Debug($"[UI] saveUiSettings 收到: {json.Length} 字节");
            try
            {
                var settings = HiAuRo.Runtime.ACRLifecycle.GetCurrentSettings();
                if (settings != null)
                {
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("qtCols", out var qtCols)) settings.QtCols = qtCols.GetInt32();
                    if (root.TryGetProperty("qtBtnW", out var qtBtnW)) settings.QtBtnW = qtBtnW.GetInt32();
                    if (root.TryGetProperty("qtVisible", out var qtVis))
                        settings.QtVisible = JsonSerializer.Deserialize<Dictionary<string, bool>>(qtVis.GetRawText()) ?? [];
                    if (root.TryGetProperty("hkCols", out var hkCols)) settings.HkCols = hkCols.GetInt32();
                    if (root.TryGetProperty("hkBtnSize", out var hkBtnSize)) settings.HkBtnSize = hkBtnSize.GetInt32();
                    if (root.TryGetProperty("hkVisible", out var hkVis))
                        settings.HkVisible = JsonSerializer.Deserialize<Dictionary<string, bool>>(hkVis.GetRawText()) ?? [];
                    if (root.TryGetProperty("hkBindings", out var hkBind))
                        settings.HkBindings = JsonSerializer.Deserialize<Dictionary<string, string>>(hkBind.GetRawText()) ?? [];

                    settings.Save();
                    _ = SendUiSettings(settings);
                }
            }
            catch (Exception ex) { DService.Instance().Log.Error($"[UI] saveUiSettings 异常: {ex}"); }
        });

        // 接收前端调试日志
        bridge.On("log", data =>
        {
            if (data == null) return;
            var msg = data.Value.TryGetProperty("msg", out var m) ? m.GetString() : "";
            var level = data.Value.TryGetProperty("level", out var l) ? l.GetString() : "info";
            var src = data.Value.TryGetProperty("src", out var s) ? s.GetString() : "web";
            var text = $"[Web:{src}] {msg}";
            switch (level)
            {
                case "error": DService.Instance().Log.Error(text); break;
                case "warn": DService.Instance().Log.Warning(text); break;
                default: DService.Instance().Log.Information(text); break;
            }
        });

        // 内容尺寸自适应：JS 上报 overlay 内容实际尺寸 → Browsingway IPC 调整窗口
        bridge.On("contentResize", data =>
        {
            if (data is null) return;
            if (Runtime.ACRLifecycle.IsLoadingRotation) return;
            var overlay = data.Value.TryGetProperty("overlay", out var o) ? o.GetString() : null;
            var width = data.Value.TryGetProperty("width", out var w) ? w.GetInt32() : 0;
            var height = data.Value.TryGetProperty("height", out var h) ? h.GetInt32() : 0;
            if (string.IsNullOrEmpty(overlay) || width <= 0 || height <= 0) return;

            // MainWindow 固定尺寸，Qt/Hotkey 自适应
            if (overlay == "MainWindow") return;

            // 更新 PluginConfig 中对应 overlay 的尺寸
            var ol = Instance._config.Overlays?.FirstOrDefault(x => x.Name == overlay);
            if (ol != null) { ol.Width = width; ol.Height = height; }

            // 通知 Browsingway（须在主线程执行）
            var ipc = Instance._uiManager?.BrowsingwayIpc;
            if (ipc != null)
            {
                var oName = overlay;
                var oW = width; var oH = height;
                DService.Instance().Framework.RunOnFrameworkThread(() => ipc.ResizeOverlay(oName, oW, oH));
            }
        });
    }

    private static async Task SendStatusState()
    {
        if (!IsWebUI) return;
        await Instance._uiBridge!.SendAsync(new
        {
            type = "acrState",
            data = new { enabled = RuntimeCore.IsRunning }
        });
    }

    private static async Task SendUiSettings(HiAuRo.ACR.AcrSettings s)
    {
        if (!IsWebUI) return;
        await Instance._uiBridge!.SendAsync(new
        {
            type = "uiSettings",
            data = new
            {
                qtCols = s.QtCols,
                qtBtnW = s.QtBtnW,
                qtVisible = s.QtVisible,
                hkCols = s.HkCols,
                hkBtnSize = s.HkBtnSize,
                hkVisible = s.HkVisible,
                hkBindings = s.HkBindings
            }
        });
    }

    private static async Task SendPauseState()
    {
        if (!IsWebUI) return;
        await Instance._uiBridge!.SendAsync(new
        {
            type = "pauseChanged",
            data = new { paused = HiAuRo.ACR.MainControlHelper.IsPaused }
        });
    }

    private static void OnHotkeyExecuted(string id, string label)
    {
        if (!IsWebUI) return;
        SafeFire(Instance._uiBridge!.SendAsync(new
        {
            type = "hotkeyExecuted",
            data = new { id, label }
        }), "OnHotkeyExecuted");
    }

    private static void OnQtChanged(string id, bool value)
    {
        if (!IsWebUI) return;
        _ = Instance._uiBridge!.SendAsync(new
        {
            type = "qtChanged",
            data = new { id, value }
        });
    }
}
