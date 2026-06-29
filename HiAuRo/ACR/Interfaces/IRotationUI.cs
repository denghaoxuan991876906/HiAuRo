namespace HiAuRo.ACR;

/// <summary>
/// ACR 作者悬浮窗 UI 接口
/// </summary>
public interface IRotationUI
{
    /// <summary>注册 UI 控件，builder 提供 ref 重载可直接绑定 settings 字段</summary>
    void RegisterControls(IAcrUiBuilder builder);
}
