namespace HiAuRo.ACR;

/// <summary>
/// 触发动作接口
/// </summary>
public interface ITriggerAction : ITriggerBase
{
    /// <summary>执行触发动作，返回 true 表示已处理</summary>
    bool Handle();

    /// <summary>用户备注</summary>
    string Remark { get; set; }

    /// <summary>编辑器控件。调用 builder 方法声明 UI 形状</summary>
    void Draw(IUiBuilder builder);
}
