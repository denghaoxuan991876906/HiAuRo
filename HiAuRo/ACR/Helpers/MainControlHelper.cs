namespace HiAuRo.ACR;

/// <summary>
/// 主控面板后端
/// 提供 ACR 启停、停手、保存设置 功能
/// </summary>
public static class MainControlHelper
{
    /// <summary>ACR 运行时暂停（不停止引擎，只暂停动作输出）</summary>
    public static bool IsPaused
    {
        get => _isPaused;
        private set => _isPaused = value;
    }

    private static volatile bool _isPaused;

    /// <summary>保存回调（由 ACR 作者注册）</summary>
    public static event Action? OnSave;

    /// <summary>切换暂停状态</summary>
    public static void TogglePause()
    {
        IsPaused = !IsPaused;
    }

    /// <summary>暂停 ACR 动作输出。</summary>
    public static void Pause()
    {
        IsPaused = true;
    }

    /// <summary>取消暂停</summary>
    public static void Unpause()
    {
        IsPaused = false;
    }

    /// <summary>触发保存</summary>
    public static void Save()
    {
        OnSave?.Invoke();
    }

    /// <summary>重置状态（ACR 卸载时调用）</summary>
    public static void Reset()
    {
        IsPaused = false;
        OnSave = null;
    }
}
