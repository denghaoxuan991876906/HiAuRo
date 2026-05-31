using FFXIVClientStructs.FFXIV.Client.Game;

namespace HiAuRo.ACR;

/// <summary>
/// 道具/爆发药使用辅助
/// </summary>
public static class ItemHelper
{
    public static uint GetCurrJobPotionItemId() => Data.Me.ClassJob switch
    {
        19 or 21 or 32 or 37 => 39727, // Tank: 刚力
        20 or 22 or 30 or 34 or 39 or 41 => 39728, // Melee: 刚力
        23 or 31 or 38 => 39727, // Ranged: 刚力
        25 or 27 or 35 or 42 => 39730, // Caster
        24 or 28 or 33 or 40 => 39731, // Healer
        _ => 39727
    };

    public static uint GetCurrJobPotionActionId(bool isHq = false)
    {
        var itemId = GetCurrJobPotionItemId();
        return isHq ? itemId + 1000000U : itemId;
    }

    /// <summary>使用物品并等待冷却好后再重试一次</summary>
    public static async void ForceUsePotion(uint itemId, bool isHq = false)
    {
        if (!await TryUseItem(itemId, isHq))
            return;

        // 等待冷却后重试（爆发药通常有 CD）
        await CoroutineHelper.Wait(500);
        _ = TryUseItem(itemId, isHq);
    }

    /// <summary>尝试使用物品，返回是否成功</summary>
    private static Task<bool> TryUseItem(uint itemId, bool isHq)
    {
        var tcs = new TaskCompletionSource<bool>();
        var realId = isHq ? itemId + 1000000U : itemId;
        var selfId = Data.Me.Object?.EntityID ?? 0;

        unsafe
        {
            // 直接使用 ActionType.Item
            var result = ActionManager.Instance()->UseAction(
                ActionType.Item, realId, selfId, ushort.MaxValue, 0, 0, null);
            tcs.SetResult(result);
        }
        return tcs.Task;
    }

    /// <summary>检查当前职业爆发药是否可用</summary>
    public static unsafe bool CheckCurrJobPotion(bool isHq = false)
    {
        if (!HasItem(GetCurrJobPotionItemId(), isHq))
            return false;

        var actionId = GetCurrJobPotionActionId(isHq);
        return ActionManager.Instance()->GetRecastTimeElapsed(ActionType.Item, actionId) == 0;
    }

    public static unsafe bool HasItem(uint itemId, bool isHq = false)
    {
        var inv = InventoryManager.Instance();
        return inv != null && inv->GetInventoryItemCount(itemId, isHq, true, true, 0) > 0;
    }

    /// <summary>背包空位数量</summary>
    public static unsafe int GetEmptyInventorySlotCount()
    {
        var inv = InventoryManager.Instance();
        if (inv == null) return 0;
        int count = 0;
        for (int i = 0; i < 140; i++)
        {
            if (inv->GetInventorySlot(FFXIVClientStructs.FFXIV.Client.Game.InventoryType.Inventory1, i) == null
                || inv->GetInventorySlot(FFXIVClientStructs.FFXIV.Client.Game.InventoryType.Inventory1, i)->ItemId == 0)
                count++;
        }
        return count;
    }
}

/// <summary>
/// 简单的协程延迟辅助（供 ItemHelper 使用）
/// </summary>
internal static class CoroutineHelper
{
    public static async Task Wait(int ms)
    {
        await Task.Delay(ms);
    }
}
