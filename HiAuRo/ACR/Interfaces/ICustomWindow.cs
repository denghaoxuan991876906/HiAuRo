using System.Numerics;

namespace HiAuRo.ACR;

/// <summary>
/// ACR 作者自定义 ImGui 窗口接口
/// </summary>
public interface ICustomWindow
{
    /// <summary>窗口唯一标识、标题</summary>
    string Name { get; }

    /// <summary>null = 自动大小</summary>
    Vector2? DefaultSize { get; }

    /// <summary>ACR 加载时是否自动打开</summary>
    bool IsOpenByDefault { get; }

    /// <summary>ACR 作者自由写 ImGui</summary>
    void Draw();
}
