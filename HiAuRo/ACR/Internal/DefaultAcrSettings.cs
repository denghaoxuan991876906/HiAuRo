namespace HiAuRo.ACR.Internal;

/// <summary>
/// 当 ACR 作者未实现 ISettingsProvider{T} 时使用的默认设置实例
/// 仅包含 AcrSettings 基类的 QT/Hotkey/UI 布局属性
/// </summary>
internal sealed class DefaultAcrSettings : AcrSettings { }
