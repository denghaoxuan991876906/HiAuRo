namespace HiAuRo.ACR;

/// <summary>
/// 实现此接口后，宿主自动托管 Settings 的加载与显式保存
/// ACR 作者在 IRotationEntry 实现类上同时实现此接口即可
/// </summary>
public interface ISettingsProvider<T> where T : class, new()
{
    /// <summary>宿主自动注入已加载的 settings 对象</summary>
    T Settings { get; set; }
}
