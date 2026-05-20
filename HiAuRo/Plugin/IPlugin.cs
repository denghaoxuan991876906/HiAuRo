namespace HiAuRo;

/// <summary>
/// 通用插件入口 —— 实现此接口的 DLL 将被 PluginLoader 自动发现并加载
/// 扫描路径: Plugins/*.dll
/// </summary>
public interface IPlugin : IDisposable
{
    string Name { get; }
    string Version { get; }
    void Initialize();
    void Update();
}
