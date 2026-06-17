namespace HiAuRo.ACR;

/// <summary>
/// 触发条件接口
/// </summary>
public interface ITriggerCond : ITriggerBase
{
    /// <summary>检查触发条件是否满足</summary>
    bool Handle(ITriggerCondParams? condParams = null);

    /// <summary>用户备注</summary>
    string Remark { get; set; }

    /// <summary>编辑器控件。调用 builder 方法声明 UI 形状</summary>
    void Draw(IUiBuilder builder);
}
