using System.Net;
using System.Net.WebSockets;

namespace HiAuRo.UI;

/// <summary>
/// HTTP + WebSocket 服务器 —— 仅为 ACR 悬浮窗提供服务
/// </summary>
public sealed class WebUiServer
{
    private HttpListener? _listener;
    private readonly string _webRoot;
    private readonly WebUiBridge _bridge;
    private CancellationTokenSource? _cts;

    /// <summary>实际监听的端口（5678 或 5679）</summary>
    public int Port { get; private set; } = 5678;

    /// <summary>Initializes a new instance of the <see cref="WebUiServer"/> class</summary>
    public WebUiServer(string webRoot, WebUiBridge bridge)
    {
        _webRoot = webRoot;
        _bridge = bridge;
    }

    /// <summary>启动服务器</summary>
    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add("http://localhost:5678/");
        Port = 5678;

        try
        {
            _listener.Start();
            DService.Instance().Log.Information("[WebServer] 启动成功 (http://localhost:5678/)");
        }
        catch (HttpListenerException)
        {
            _listener.Prefixes.Clear();
            _listener.Prefixes.Add("http://localhost:5679/");
            Port = 5679;
            _listener.Start();
            DService.Instance().Log.Warning("[WebServer] 5678被占用, 改用 http://localhost:5679/");
        }

        _ = Task.Run(() => ListenLoop(_cts.Token));
    }

    /// <summary>停止服务器</summary>
    public void Stop()
    {
        _cts?.Cancel();
        try
        {
            _listener?.Stop();
        }
        catch (ObjectDisposedException) { }
        try
        {
            _listener?.Close();
        }
        catch (ObjectDisposedException) { }
        _cts?.Dispose();
        _cts = null;
        _listener = null;
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener is { IsListening: true })
        {
            try
            {
                var ctx = await _listener.GetContextAsync().WaitAsync(ct);
                _ = Task.Run(() => HandleRequest(ctx, ct), ct);
            }
            catch (OperationCanceledException) { break; }
            catch (HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception) { }
        }
    }

    private async Task HandleRequest(HttpListenerContext ctx, CancellationToken ct)
    {
        try
        {
            var path = ctx.Request.Url?.AbsolutePath ?? "/";

            // WebSocket
            if (path == "/ws" && ctx.Request.IsWebSocketRequest)
            {
                DService.Instance().Log.Information($"[WebServer] WebSocket 升级 /ws");
                var wsCtx = await ctx.AcceptWebSocketAsync(null);
                await _bridge.HandleConnection(wsCtx.WebSocket, ct);
                return;
            }

            var filePath = path == "/" ? "/main.html" : path;
            if (!Path.HasExtension(filePath))
                filePath += ".html";
            var fullPath = Path.GetFullPath(Path.Combine(_webRoot, filePath.TrimStart('/')));

            if (!fullPath.StartsWith(_webRoot, StringComparison.OrdinalIgnoreCase))
            {
                ctx.Response.StatusCode = 403;
                return;
            }

            if (File.Exists(fullPath))
            {
                var data = await File.ReadAllBytesAsync(fullPath, ct);
                ctx.Response.ContentType = GetContentType(fullPath);
                ctx.Response.ContentLength64 = data.Length;
                await ctx.Response.OutputStream.WriteAsync(data, ct);
            }
            else
            {
                ctx.Response.StatusCode = 404;
                DService.Instance().Log.Warning($"[WebServer] 404 {path} (fullPath={fullPath})");
            }
        }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            DService.Instance().Log.Warning($"[WebServer] 请求异常: {ex.Message}");
        }
        finally
        {
            try { ctx.Response.OutputStream.Close(); } catch { }
        }
    }

    private static string GetContentType(string path) =>
        Path.GetExtension(path).ToLower() switch
        {
            ".html" => "text/html; charset=utf-8",
            ".js" => "application/javascript; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            _ => "application/octet-stream"
        };
}
