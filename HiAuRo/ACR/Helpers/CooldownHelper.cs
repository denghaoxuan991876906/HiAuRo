namespace HiAuRo.ACR;

/// <summary>
/// [已废弃] 请使用 SpellHelper 替代。保留此类以兼容旧版 ACR 程序集。
/// </summary>
public static class CooldownHelper
{
    public static bool IsOnCooldown(uint spellId)
        => !SpellHelper.CanUseSpell(spellId);

    public static float GetCooldownRemaining(uint spellId)
        => SpellHelper.GetCooldownRemaining(spellId);

    public static int GetMaxCharges(uint spellId)
        => SpellHelper.GetMaxCharges(spellId);

    public static int GetCharges(uint spellId)
        => SpellHelper.GetCharges(spellId);

    public static float GetChargeCooldown(uint spellId)
        => SpellHelper.GetChargeCooldown(spellId);
}
