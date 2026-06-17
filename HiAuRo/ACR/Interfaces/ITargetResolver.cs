using OmenTools.Dalamud.Services.ObjectTable.Abstractions.ObjectKinds;

namespace HiAuRo.ACR;

/// <summary>
/// 目标选择器接口 —— 在进战前/进战后主动选择目标
/// Rotation 可通过 AddTargetResolver() 注册多个选择器，按顺序调用
/// </summary>
public interface ITargetResolver
{
    /// <summary>尝试选择目标。返回 true 表示成功选择目标</summary>
    bool ResolveTarget(out IBattleChara agent);
}
