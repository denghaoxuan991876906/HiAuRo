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
    private long _acrGeneration;
    private AcrSnapshot? _cachedAcrSnapshot;
    private bool _disposed;

    internal sealed record AcrSnapshot(
        long Generation,
        byte[] Status,
        byte[] Controls,
        byte[] UiSettings);

    internal AcrSnapshot? CachedAcrSnapshot
    {
        get
        {
            lock (_lock) return _cachedAcrSnapshot;
        }
    }

    internal static bool IsCurrentSnapshot(AcrSnapshot candidate, AcrSnapshot? current)
        => current?.Generation == candidate.Generation;

    internal static AcrSnapshot SelectCurrentSnapshot(AcrSnapshot captured, AcrSnapshot? current)
        => current is not null && current.Generation > captured.Generation ? current : captured;

    /// <summary>按代发布完整 ACR Web 状态</summary>
    internal async Task PublishAcrStateAsync(
        object statusData,
        List<UiControlDef> controls,
        object uiSettings)
    {
        var serialized = SerializeSnapshot(0, statusData, controls, uiSettings);
        AcrSnapshot snapshot;
        List<WebSocket> sendTargets;
        lock (_lock)
        {
            if (_disposed) return;
            snapshot = serialized with { Generation = ++_acrGeneration };
            _cachedAcrSnapshot = snapshot;
            _clients.RemoveAll(c => c.State != WebSocketState.Open);
            sendTargets = [.._clients];
        }

        if (sendTargets.Count == 0) return;

        List<WebSocket>? failedClients = null;
        await _writeLock.WaitAsync();
        try
        {
            lock (_lock)
            {
                if (_disposed || !IsCurrentSnapshot(snapshot, _cachedAcrSnapshot)) return;
            }

            foreach (var client in sendTargets)
            {
                try
                {
                    await SendSnapshotAsync(client, snapshot);
                }
                catch (Exception ex)
                {
                    DService.Instance().Log.Warning($"[WebUiBridge] PublishAcrStateAsync 失败: {ex.Message}");
                    (failedClients ??= []).Add(client);
                }
            }
        }
        finally
        {
            _writeLock.Release();
        }

        RemoveFailedClients(failedClients);
    }

    /// <summary>重置 ACR 状态、缓存并通知当前客户端</summary>
    internal Task ResetAcrStateAsync(bool isRunning) =>
        PublishAcrStateAsync(
            new
            {
                job = "无ACR",
                enabled = isRunning,
                paused = false,
                hotkeys = Array.Empty<object>(),
                qts = Array.Empty<object>()
            },
            [],
            CreateDefaultUiSettings());

    private AcrSnapshot SerializeSnapshot(
        long generation,
        object statusData,
        List<UiControlDef> controls,
        object uiSettings) =>
        new(
            generation,
            JsonSerializer.SerializeToUtf8Bytes(new { type = "status", data = statusData }, _jsonOptions),
            JsonSerializer.SerializeToUtf8Bytes(new { type = "controls", data = controls }, _jsonOptions),
            JsonSerializer.SerializeToUtf8Bytes(new { type = "uiSettings", data = uiSettings }, _jsonOptions));

    private static object CreateDefaultUiSettings() => new
    {
        qtCols = 0,
        qtBtnW = 0,
        qtVisible = new Dictionary<string, bool>(),
        hkCols = 0,
        hkBtnSize = 52,
        hkVisible = new Dictionary<string, bool>(),
        hkBindings = new Dictionary<string, string>()
    };

    private static async Task SendSnapshotAsync(WebSocket client, AcrSnapshot snapshot)
    {
        await client.SendAsync(snapshot.Status, WebSocketMessageType.Text, true, CancellationToken.None);
        await client.SendAsync(snapshot.Controls, WebSocketMessageType.Text, true, CancellationToken.None);
        await client.SendAsync(snapshot.UiSettings, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private void RemoveFailedClients(List<WebSocket>? failedClients)
    {
        if (failedClients is null) return;
        lock (_lock)
        {
            foreach (var client in failedClients)
                _clients.Remove(client);
        }
    }

    /// <summary>注册消息处理器</summary>
    public void On(string type, Action<JsonElement?> handler)
    {
        _handlers[type] = handler;
    }

    /// <summary>推送消息到所有已连接客户端</summary>
    public async Task SendAsync(object message)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, _jsonOptions);

        List<WebSocket> sendTargets;
        lock (_lock)
        {
            if (_disposed) return;
            _clients.RemoveAll(c => c.State != WebSocketState.Open);
            sendTargets = [.._clients];
        }

        if (sendTargets.Count == 0) return;

        List<WebSocket>? failedClients = null;
        await _writeLock.WaitAsync();
        try
        {
            lock (_lock)
            {
                if (_disposed) return;
            }

            foreach (var client in sendTargets)
            {
                try
                {
                    await client.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    DService.Instance().Log.Warning($"[WebUiBridge] SendAsync 失败: {ex.Message}");
                    (failedClients ??= []).Add(client);
                }
            }
        }
        finally
        {
            _writeLock.Release();
        }

        RemoveFailedClients(failedClients);
    }

    /// <summary>处理单个 WebSocket 连接</summary>
    public async Task HandleConnection(WebSocket ws, CancellationToken ct)
    {
        int clientCount;
        lock (_lock)
        {
            if (_disposed)
            {
                ws.Abort();
                return;
            }
            _clients.Add(ws);
            clientCount = _clients.Count;
        }
        DService.Instance().Log.Information($"[WS] 客户端已连接 (当前{clientCount}个)");

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
            lock (_lock)
            {
                _clients.Remove(ws);
                clientCount = _clients.Count;
            }
            DService.Instance().Log.Information($"[WS] 客户端已断开 (剩余{clientCount}个)");
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
            if (_disposed) return;
            _disposed = true;
            foreach (var client in _clients)
            {
                try { client.Abort(); } catch { }
            }
            _clients.Clear();
            _cachedAcrSnapshot = null;
        }
        _handlers.Clear();
    }

    private async Task PushInitialStatus(WebSocket ws)
    {
        try
        {
            AcrSnapshot? snapshot;
            lock (_lock)
            {
                if (_disposed) return;
                snapshot = _cachedAcrSnapshot;
            }
            snapshot ??= CreateFallbackSnapshot();

            await _writeLock.WaitAsync();
            try
            {
                lock (_lock)
                {
                    if (_disposed) return;
                    snapshot = SelectCurrentSnapshot(snapshot, _cachedAcrSnapshot);
                }
                DService.Instance().Log.Information($"[WS] PushInitialStatus: generation={snapshot.Generation}");
                await SendSnapshotAsync(ws, snapshot);
            }
            finally
            {
                _writeLock.Release();
            }
        }
        catch (Exception ex) { DService.Instance().Log.Error($"[WS] PushInitialStatus 失败: {ex.Message}"); }
    }

    private AcrSnapshot CreateFallbackSnapshot()
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

        return SerializeSnapshot(
            0,
            new
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
            },
            [],
            CreateDefaultUiSettings());
    }
}
