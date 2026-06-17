using System.Numerics;
using Dalamud.Interface.Windowing;

namespace HiAuRo.Runtime;

/// <summary>
/// 插件窗口管理器 —— 扫描有窗口的插件, 注册到 WindowSystem, 提供 Toggle 命令
/// </summary>
public static class PluginWindowManager
{
    private static readonly Dictionary<string, PluginWindowWrapper> _windows = [];

    /// <summary>已注册的插件窗口名称列表</summary>
    public static IReadOnlyDictionary<string, PluginWindowWrapper> Windows => _windows;

    /// <summary>扫描插件并注册窗口到 WindowSystem</summary>
    public static void Init(WindowSystem windowSystem)
    {
        foreach (var (name, record) in PluginLoader.Plugins)
        {
            var window = record.Plugin.GetWindow();
            if (window == null) continue;

            var wrapper = new PluginWindowWrapper(window);
            windowSystem.AddWindow(wrapper);
            _windows[name] = wrapper;
            DService.Instance().Log.Information($"[PluginWindow] 已注册: {name} ({wrapper.WindowName})");
        }
    }

    /// <summary>切换指定插件窗口的显隐</summary>
    public static void Toggle(string pluginName)
    {
        if (_windows.TryGetValue(pluginName, out var w))
        {
            w.IsOpen = !w.IsOpen;
            DService.Instance().Log.Information($"[PluginWindow] {pluginName} -> {(w.IsOpen ? "显示" : "隐藏")}");
        }
    }

    /// <summary>显示指定插件窗口</summary>
    public static void Show(string pluginName)
    {
        if (_windows.TryGetValue(pluginName, out var w))
            w.IsOpen = true;
    }

    /// <summary>隐藏指定插件窗口</summary>
    public static void Hide(string pluginName)
    {
        if (_windows.TryGetValue(pluginName, out var w))
            w.IsOpen = false;
    }

    /// <summary>ImGui Window 包装 —— 委托 IPluginWindow.Draw()</summary>
    public sealed class PluginWindowWrapper : Window
    {
        readonly IPluginWindow _window;

        public PluginWindowWrapper(IPluginWindow window)
            : base($"{window.Title}##Plugin")
        {
            _window = window;
            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(200, 150),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };
            IsOpen = false;
        }

        public override void Draw() => _window.Draw();
    }
}
