#if DEBUG
using System.Diagnostics;

namespace HiAuRo.Infrastructure;

/// <summary>
/// 模块级性能监控 —— 仅在 DEBUG 编译时启用的零分配内联计时器
/// </summary>
public static class PerfMonitor
{
    private static readonly Dictionary<string, double> _lastFrame = [];
    private static readonly Dictionary<string, double> _max = [];
    private static readonly HashSet<string> _modules = [];

    /// <summary>上一帧各模块耗时(μs)，用于 UI 展示（含未执行模块，值为 0）</summary>
    public static IReadOnlyDictionary<string, double> LastFrame => _lastFrame;

    /// <summary>历史最大值(μs)</summary>
    public static IReadOnlyDictionary<string, double> Max => _max;

    /// <summary>注册模块名（在启动时调用一次），确保未执行模块也显示在面板中</summary>
    public static void Register(params string[] names)
    {
        foreach (var n in names) _modules.Add(n);
    }

    /// <summary>每帧开始前调用，将所有已注册模块重置为 0</summary>
    public static void BeginFrame()
    {
        foreach (var m in _modules)
            _lastFrame[m] = 0;
    }

    /// <summary>记录模块耗时。传入模块开始时 Stopwatch.GetTimestamp() 的值</summary>
    public static double Record(string name, long startTick)
    {
        var us = (Stopwatch.GetTimestamp() - startTick) * 1_000_000.0 / Stopwatch.Frequency;
        _lastFrame[name] = us;
        if (!_max.TryGetValue(name, out var m) || us > m)
            _max[name] = us;
        return us;
    }

    /// <summary>上一帧总耗时(μs)</summary>
    public static double TotalUs
    {
        get
        {
            double sum = 0;
            foreach (var v in _lastFrame.Values) sum += v;
            return sum;
        }
    }

    /// <summary>重置所有历史最大值</summary>
    public static void ResetMax()
    {
        foreach (var key in _max.Keys.ToList())
            _max[key] = 0;
    }
}
#endif
