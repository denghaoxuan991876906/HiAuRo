using System.Linq;
using System.Text.Json;
using Browsingway;
using HiAuRo.Infrastructure;

namespace HiAuRo.UI;

/// <summary>
/// Browsingway IPC 服务 —— 通过 Dalamud IPC 控制 Browsingway 的 overlay 窗口
/// 同时暴露 Provider 端点供 Browsingway 反向查询 HiAuRo 的 WebUI 设置
/// </summary>
internal sealed class BrowsingwayIpc : IDisposable
{
    private readonly int _port;
    private readonly Func<UIMode> _getUIMode;
    private OverlayWindowSetting[] _overlayConfigs = [];
    public bool IsReady { get; private set; }
    private readonly CancellationTokenSource _cts = new();
    private bool _inOverlayOp; // 重入保护

    public BrowsingwayIpc(int port, Func<UIMode> getUIMode)
    {
        _port = port;
        _getUIMode = getUIMode;
        RegisterProviders();
    }

    /// <summary>注册 HiAuRo → Browsingway 的 IPC Provider 端点</summary>
    private void RegisterProviders()
    {
        var pi = DService.Instance().PI;
        try
        {
            pi.GetIpcProvider<int>("HiAuRo.GetWebUiPort").RegisterFunc(() => _port);
            pi.GetIpcProvider<string>("HiAuRo.GetOverlaysJson").RegisterFunc(() =>
                JsonSerializer.Serialize(_overlayConfigs,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            pi.GetIpcProvider<bool>("HiAuRo.IsWebUIMode").RegisterFunc(() => _getUIMode() == UIMode.WebUI);
            DService.Instance().Log.Information("[BrowsingwayIpc] IPC Provider 端点已注册");
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[BrowsingwayIpc] IPC Provider 注册失败: {ex.Message}");
        }
    }

    /// <summary>注销 IPC Provider 端点</summary>
    private void UnregisterProviders()
    {
        var pi = DService.Instance().PI;
        try { pi.GetIpcProvider<int>("HiAuRo.GetWebUiPort").UnregisterFunc(); } catch { }
        try { pi.GetIpcProvider<string>("HiAuRo.GetOverlaysJson").UnregisterFunc(); } catch { }
        try { pi.GetIpcProvider<bool>("HiAuRo.IsWebUIMode").UnregisterFunc(); } catch { }
    }

    /// <summary>初始化 overlay（不等待 Browsingway 就绪，失败静默忽略）</summary>
    public void Init(OverlayWindowSetting[] overlays)
    {
        try
        {
            _overlayConfigs = overlays;

            foreach (var ol in overlays)
                CreateOrUpdateOverlay(ol);

            IsReady = true;
            DService.Instance().Log.Information($"[BrowsingwayIpc] 已注册 {overlays.Length} 个 overlay");
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[BrowsingwayIpc] 初始化失败: {ex.Message}");
        }
    }

    /// <summary>创建或更新 overlay（替换 URL 中的端口占位符）</summary>
    public void CreateOrUpdateOverlay(OverlayWindowSetting ol)
    {
        if (_inOverlayOp) return; // 重入保护
        _inOverlayOp = true;
        try
        {
            var url = ol.Url.Replace("localhost:5678", $"localhost:{_port}");
            DService.Instance().Framework.RunOnFrameworkThread(() =>
            {
                try
                {
                    DService.Instance().PI
                        .GetIpcSubscriber<CreateOrUpdateArgs, object>("Browsingway.Overlay.CreateOrUpdate")
                        .InvokeAction(new CreateOrUpdateArgs
                        {
                            Name = ol.Name,
                            Url = url,
                            Width = ol.Width,
                            Height = ol.Height,
                            Zoom = ol.Zoom,
                            Locked = ol.Locked
                        });
                }
                catch (Exception ex)
                {
                    DService.Instance().Log.Warning($"[BrowsingwayIpc] IPC CreateOrUpdate 不可用 ({ol.Name}): {ex.Message}");
                }
            });
            if (ol.Visible)
                SetOverlayVisible(ol.Name, true);
            else
                SetOverlayVisible(ol.Name, false);
            DService.Instance().Log.Debug($"[BrowsingwayIpc] CreateOrUpdate: {ol.Name} {url} {ol.Width}x{ol.Height}");
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[BrowsingwayIpc] CreateOrUpdate 失败 ({ol.Name}): {ex.Message}");
        }
        finally { _inOverlayOp = false; }
    }

    /// <summary>调整 overlay 尺寸（保留其他属性从配置读取）</summary>
    public void ResizeOverlay(string name, int width, int height)
    {
        var cfg = _overlayConfigs.FirstOrDefault(o => o.Name == name);
        if (cfg == null) return;
        try
        {
            var url = cfg.Url.Replace("localhost:5678", $"localhost:{_port}");
            DService.Instance().Framework.RunOnFrameworkThread(() =>
            {
                try
                {
                    DService.Instance().PI
                        .GetIpcSubscriber<CreateOrUpdateArgs, object>("Browsingway.Overlay.CreateOrUpdate")
                        .InvokeAction(new CreateOrUpdateArgs
                        {
                            Name = name,
                            Url = url,
                            Width = width,
                            Height = height,
                            Zoom = cfg.Zoom,
                            Locked = cfg.Locked
                        });
                }
                catch (Exception ex)
                {
                    DService.Instance().Log.Warning($"[BrowsingwayIpc] IPC Resize 不可用 ({name}): {ex.Message}");
                }
            });
            DService.Instance().Log.Debug($"[BrowsingwayIpc] Resize: {name} {width}x{height}");
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[BrowsingwayIpc] Resize 失败 ({name}): {ex.Message}");
        }
    }

    /// <summary>显示/隐藏 overlay</summary>
    public void SetOverlayVisible(string name, bool visible)
    {
        if (_inOverlayOp) return; // 重入保护
        _inOverlayOp = true;
        try
        {
            DService.Instance().Framework.RunOnFrameworkThread(() =>
            {
                try
                {
                    DService.Instance().PI
                        .GetIpcSubscriber<SetVisibilityArgs, object>("Browsingway.Overlay.SetVisibility")
                        .InvokeAction(new SetVisibilityArgs
                        {
                            Name = name,
                            Visible = visible
                        });
                }
                catch (Exception ex)
                {
                    DService.Instance().Log.Warning($"[BrowsingwayIpc] IPC SetVisibility 不可用 ({name}): {ex.Message}");
                }
            });
            DService.Instance().Log.Debug($"[BrowsingwayIpc] SetVisibility: {name} = {visible}");
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[BrowsingwayIpc] SetVisibility 失败 ({name}): {ex.Message}");
        }
        finally { _inOverlayOp = false; }
    }

    /// <summary>按配置显示/隐藏所有 overlay</summary>
    public void ShowConfigured(OverlayWindowSetting[] overlays)
    {
        foreach (var ol in overlays)
            SetOverlayVisible(ol.Name, ol.Visible);
    }

    /// <summary>禁用所有 overlay（销毁 CEF 实例，释放资源）</summary>
    public void DisableAll(OverlayWindowSetting[] overlays)
    {
        foreach (var ol in overlays)
            SetOverlayDisabled(ol.Name, true);
    }

    /// <summary>启用所有 overlay（重新创建 CEF 实例）</summary>
    public void EnableAll(OverlayWindowSetting[] overlays)
    {
        foreach (var ol in overlays)
            SetOverlayDisabled(ol.Name, false);
    }

    /// <summary>禁用/启用 overlay</summary>
    private void SetOverlayDisabled(string name, bool disabled)
    {
        try
        {
            DService.Instance().Framework.RunOnFrameworkThread(() =>
            {
                try
                {
                    DService.Instance().PI
                        .GetIpcSubscriber<SetDisabledArgs, object>("Browsingway.Overlay.SetDisabled")
                        .InvokeAction(new SetDisabledArgs { Name = name, Disabled = disabled });
                }
                catch (Exception ex)
                {
                    DService.Instance().Log.Warning($"[BrowsingwayIpc] IPC SetDisabled 不可用 ({name}): {ex.Message}");
                }
            });
            DService.Instance().Log.Debug($"[BrowsingwayIpc] SetDisabled: {name} = {disabled}");
        }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[BrowsingwayIpc] SetDisabled 失败 ({name}): {ex.Message}");
        }
    }

    /// <summary>隐藏所有 overlay</summary>
    public void HideAll(OverlayWindowSetting[] overlays)
    {
        foreach (var ol in overlays)
            SetOverlayVisible(ol.Name, false);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        IsReady = false;
        UnregisterProviders();
    }
}
