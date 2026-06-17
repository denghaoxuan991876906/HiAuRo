using FFXIVClientStructs.FFXIV.Client.Game;

namespace HiAuRo.ACR;

/// <summary>
/// 连击辅助 —— 直接读取 ActionManager.Instance()->Combo (游戏原生连击状态)
/// </summary>
public static class ComboHelper
{
    /// <summary>当前连击技能 ID（游戏原生）</summary>
    public static unsafe uint LastComboSpellId
    {
        get
        {
            var am = ActionManager.Instance();
            if (am == null) return 0;
            return am->Combo.Action;
        }
    }

    /// <summary>连击剩余时间 (秒)</summary>
    public static unsafe float ComboTimer
    {
        get
        {
            var am = ActionManager.Instance();
            if (am == null) return 0;
            return am->Combo.Timer;
        }
    }

    /// <summary>最近一次成功使用的技能 ID（仍走 EventSystem 手动记录）</summary>
    public static uint LastSpellId
        => Runtime.EventSystem.LastCompletedActionId;

    /// <summary>上一个技能是指定 ID 吗？（读取游戏原生 Combo）</summary>
    public static bool WasLastCombo(uint spellId)
        => LastComboSpellId == spellId;

    /// <summary>连击是否在窗口内（游戏原生计时器）</summary>
    public static bool ComboInWindow(uint spellId, int windowMs = 15000)
        => LastComboSpellId == spellId && ComboTimer > 0;

    /// <summary>连击是否即将过期</summary>
    public static bool ComboAboutToExpire(uint spellId, int withinMs = 500)
        => LastComboSpellId == spellId && ComboTimer <= withinMs / 1000f;
}
