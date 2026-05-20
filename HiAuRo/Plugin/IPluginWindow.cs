namespace HiAuRo;

/// <summary>
/// 插件 ImGui 窗口接口 —— IPlugin.GetWindow() 返回此接口即可自动注册到 WindowSystem
/// </summary>
public interface IPluginWindow
{
    string Title { get; }
    void Draw();
}
