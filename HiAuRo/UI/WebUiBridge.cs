using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HiAuRo.UI;

/// <summary>
/// C# ↔ JS 消息路由（JSON 序列化 / 分发）
/// </summary>
public sealed class WebUiBridge : IDisposable
{
    private readonly List<WebSocket> _clients = [];
    private readonly object _lock = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly Dictionary<string, Action<JsonElement?>> _handlers = [];

    // 缓存初始数据，新连接时补发（避免 controls/uiSettings 消息在 WS 连接前丢失）
    private byte[]? _cachedControls;
    private byte[]? _cachedUiSettings;

    /// <summary>缓存控件定义 JSON，供新 WebSocket 连接补发</summary>
    public void CacheControls(List<UiControlDef> controls)
    {
        var json = JsonSerializer.Serialize(new { type = "controls", data = controls }, _jsonOptions);
        _cachedControls = Encoding.UTF8.GetBytes(json);
    }

    /// <summary>缓存 UI 设置 JSON，供新 WebSocket 连接补发</summary>
    public void CacheUiSettings(object uiSettings)
    {
        var json = JsonSerializer.Serialize(new { type = "uiSettings", data = uiSettings }, _jsonOptions);
        _cachedUiSettings = Encoding.UTF8.GetBytes(json);
    }

    /// <summary>注册消息处理器</summary>
    public void On(string type, Action<JsonElement?> handler)
    {
        _handlers[type] = handler;
    }

    /// <summary>推送消息到所有已连接客户端</summary>
    public async Task SendAsync(object message)
    {
        var json = JsonSerializer.Serialize(message, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var segment = new ArraySegment<byte>(bytes);

        List<WebSocket> sendTargets;
        lock (_lock)
        {
            _clients.RemoveAll(c => c.State != WebSocketState.Open);
            sendTargets = [.._clients];
        }

        foreach (var client in sendTargets)
        {
            try
            {
                await _writeLock.WaitAsync();
                try
                {
                    await client.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                finally
                {
                    _writeLock.Release();
                }
            }
            catch (Exception ex)
            {
                DService.Instance().Log.Warning($"[WebUiBridge] SendAsync 失败: {ex.Message}");
                lock (_lock) _clients.Remove(client);
            }
        }
    }

    /// <summary>处理单个 WebSocket 连接</summary>
    public async Task HandleConnection(WebSocket ws, CancellationToken ct)
    {
        lock (_lock) _clients.Add(ws);
        DService.Instance().Log.Information($"[WS] 客户端已连接 (当前{_clients.Count}个)");

        // 连接时推送初始状态
        await PushInitialStatus(ws);

        var buffer = new byte[8192];
        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var type = doc.RootElement.GetProperty("type").GetString();
                    if (type != null && _handlers.TryGetValue(type, out var handler))
                    {
                        var data = doc.RootElement.TryGetProperty("data", out var dataElement) ? dataElement : (JsonElement?)null;
                        handler(data);
                    }
                }
                catch (Exception ex)
                {
                    DService.Instance().Log.Warning($"[WebUiBridge] 消息解析失败: {ex.Message}");
                }
            }
        }
        catch (WebSocketException) { }
        finally
        {
            lock (_lock) _clients.Remove(ws);
            DService.Instance().Log.Information($"[WS] 客户端已断开 (剩余{_clients.Count}个)");
            if (ws.State != WebSocketState.Closed)
            {
                try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); }
                catch { }
            }
        }
    }

    /// <summary>释放所有 WebSocket 连接和资源</summary>
    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var client in _clients)
            {
                try { client.Abort(); } catch { }
            }
            _clients.Clear();
        }
        _handlers.Clear();
        _cachedControls = null;
        _cachedUiSettings = null;
        _writeLock.Dispose();
    }

    private async Task PushInitialStatus(WebSocket ws)
    {
        try
        {
            var hotkeys = ACR.HotkeyHelper.GetAll().Select(r => new
            {
                id = r.Id,
                label = r.Label,
                iconId = r.IconId,
                iconUrl = IconServer.GetIconUrl(r.IconId),
                available = r.Check() >= 0,
                binding = ACR.HotkeyHelper.GetBinding(r.Id)
            }).ToList();

            var qts = ACR.QTHelper.GetAll().Select(q => new
            {
                id = q.Id,
                label = q.Label,
                value = q.Value,
                tooltip = q.Tooltip,
                color = q.Color,
                binding = q.HotkeyBinding
            }).ToList();

            DService.Instance().Log.Information($"[WS] PushInitialStatus: hks={hotkeys.Count} qts={qts.Count} acr={HiAuRo.Runtime.ACRLifecycle.CurrentAcrName}");

            var json = JsonSerializer.Serialize(new
            {
                type = "status",
                data = new
                {
                    job = HiAuRo.Runtime.ACRLifecycle.CurrentAcrName,
                    enabled = HiAuRo.Runtime.RuntimeCore.IsRunning,
                    paused = HiAuRo.ACR.MainControlHelper.IsPaused,
                    inCombat = HiAuRo.Runtime.CombatContext.IsInCombat,
                    currentSpell = "Idle",
                    gcdRemaining = 0,
                    gcdDuration = 2500,
                    hotkeys,
                    qts
                }
            }, _jsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);

            await _writeLock.WaitAsync();
            try
            {
                await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);

                // 补发缓存的 controls / uiSettings（避免 WS 连接前消息丢失）
                if (_cachedControls != null)
                    await ws.SendAsync(new ArraySegment<byte>(_cachedControls), WebSocketMessageType.Text, true, CancellationToken.None);
                if (_cachedUiSettings != null)
                    await ws.SendAsync(new ArraySegment<byte>(_cachedUiSettings), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            finally
            {
                _writeLock.Release();
            }
        }
        catch (Exception ex) { DService.Instance().Log.Error($"[WS] PushInitialStatus 失败: {ex.Message}"); }
    }
}
