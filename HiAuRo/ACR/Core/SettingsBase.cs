using HiAuRo.Setting;

namespace HiAuRo.ACR;

/// <summary>
/// 设置基类 —— 所有 HiAuRo 设置（主插件/副插件/ACR）统一继承
/// Save() 自动选择存储路径：ACR 实例走 ACR 路径，其他走主插件路径
/// </summary>
public abstract class SettingsBase
{
    public void Save()
    {
        SettingMgr.Save(this);
    }
}
