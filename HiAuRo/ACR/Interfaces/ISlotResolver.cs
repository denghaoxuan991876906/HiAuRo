namespace HiAuRo.ACR;

/// <summary>
/// 技能槽位解析器 —— Check + Build 双方法
/// </summary>
public interface ISlotResolver
{
    /// <summary>检查优先级/可用性。&gt;=0 可用（越大优先级越高），&lt;0 禁止</summary>
    int Check();

    /// <summary>构建 Slot。Check() &gt;= 0 时由 AI 引擎调用</summary>
    void Build(Slot slot);
}
