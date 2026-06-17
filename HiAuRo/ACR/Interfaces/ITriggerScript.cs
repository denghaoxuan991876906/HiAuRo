namespace HiAuRo.ACR;

/// <summary>
/// 触发脚本接口
/// </summary>
public interface ITriggerScript : ITriggerBase
{
    /// <summary>检查/执行脚本。OnlyCheck=true 时仅做条件判断。</summary>
    bool Check(ITriggerCondParams? condParams);
}
