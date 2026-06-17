using Dalamud.Game.ClientState.Keys;

namespace HiAuRo.ACR;

/// <summary>
/// 键位字符串解析器 —— 将 "Ctrl+F1" / "A" 等字符串转为 VirtualKey
/// </summary>
internal static class KeyBindingParser
{
    private static readonly Dictionary<string, VirtualKey> _nameToVk = new(StringComparer.OrdinalIgnoreCase)
    {
        // 字母
        ["A"] = VirtualKey.A, ["B"] = VirtualKey.B, ["C"] = VirtualKey.C,
        ["D"] = VirtualKey.D, ["E"] = VirtualKey.E, ["F"] = VirtualKey.F,
        ["G"] = VirtualKey.G, ["H"] = VirtualKey.H, ["I"] = VirtualKey.I,
        ["J"] = VirtualKey.J, ["K"] = VirtualKey.K, ["L"] = VirtualKey.L,
        ["M"] = VirtualKey.M, ["N"] = VirtualKey.N, ["O"] = VirtualKey.O,
        ["P"] = VirtualKey.P, ["Q"] = VirtualKey.Q, ["R"] = VirtualKey.R,
        ["S"] = VirtualKey.S, ["T"] = VirtualKey.T, ["U"] = VirtualKey.U,
        ["V"] = VirtualKey.V, ["W"] = VirtualKey.W, ["X"] = VirtualKey.X,
        ["Y"] = VirtualKey.Y, ["Z"] = VirtualKey.Z,
        // 功能键
        ["F1"] = VirtualKey.F1, ["F2"] = VirtualKey.F2, ["F3"] = VirtualKey.F3,
        ["F4"] = VirtualKey.F4, ["F5"] = VirtualKey.F5, ["F6"] = VirtualKey.F6,
        ["F7"] = VirtualKey.F7, ["F8"] = VirtualKey.F8, ["F9"] = VirtualKey.F9,
        ["F10"] = VirtualKey.F10, ["F11"] = VirtualKey.F11, ["F12"] = VirtualKey.F12,
        // 修饰键
        ["Ctrl"] = VirtualKey.CONTROL,
        ["Shift"] = VirtualKey.SHIFT,
        ["Alt"] = VirtualKey.MENU,
        // 其他常用键
        ["Space"] = VirtualKey.SPACE,
        ["Tab"] = VirtualKey.TAB,
        ["Enter"] = VirtualKey.RETURN,
        ["Esc"] = VirtualKey.ESCAPE,
        ["Backspace"] = VirtualKey.BACK,
        ["Delete"] = VirtualKey.DELETE,
        ["Insert"] = VirtualKey.INSERT,
        ["Home"] = VirtualKey.HOME,
        ["End"] = VirtualKey.END,
        ["PageUp"] = VirtualKey.PRIOR,
        ["PageDown"] = VirtualKey.NEXT,
        ["Up"] = VirtualKey.UP,
        ["Down"] = VirtualKey.DOWN,
        ["Left"] = VirtualKey.LEFT,
        ["Right"] = VirtualKey.RIGHT,
        // 符号键
        ["`"] = VirtualKey.OEM_3,
        ["-"] = VirtualKey.OEM_MINUS,
        ["="] = VirtualKey.OEM_PLUS,
        ["["] = VirtualKey.OEM_4,
        ["]"] = VirtualKey.OEM_6,
        ["\\"] = VirtualKey.OEM_5,
        [";"] = VirtualKey.OEM_1,
        ["'"] = VirtualKey.OEM_7,
        [","] = VirtualKey.OEM_COMMA,
        ["."] = VirtualKey.OEM_PERIOD,
        ["/"] = VirtualKey.OEM_2,
    };

    /// <summary>将 "Ctrl+Shift+F1" 解析为 (modifiers, primaryKey)</summary>
    public static (VirtualKey[] Modifiers, VirtualKey PrimaryKey)? Parse(string binding)
    {
        if (string.IsNullOrWhiteSpace(binding))
            return null;

        var parts = binding.Split('+', StringSplitOptions.TrimEntries);
        var modifiers = new List<VirtualKey>();
        VirtualKey? primary = null;

        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part))
                return null;

            if (_nameToVk.TryGetValue(part, out var vk))
            {
                if (IsModifier(vk))
                    modifiers.Add(vk);
                else
                {
                    if (primary != null) return null;
                    primary = vk;
                }
            }
            else if (part.Length == 1 && part[0] >= '0' && part[0] <= '9')
            {
                var vkN = (VirtualKey)(0x30 + (part[0] - '0'));
                if (primary != null) return null;
                primary = vkN;
            }
            else
            {
                return null;
            }
        }

        if (primary == null) return null;
        return (modifiers.ToArray(), primary.Value);
    }

    /// <summary>检查所有修饰键是否按住</summary>
    public static bool AreModifiersHeld(VirtualKey[] modifiers)
    {
        var ks = DService.Instance().KeyState;
        foreach (var mod in modifiers)
        {
            if (!ks[mod]) return false;
        }
        return true;
    }

    /// <summary>检查主键是否按住</summary>
    public static bool IsKeyDown(VirtualKey vk) =>
        DService.Instance().KeyState[vk];

    private static bool IsModifier(VirtualKey vk) =>
        vk == VirtualKey.CONTROL || vk == VirtualKey.SHIFT || vk == VirtualKey.MENU;
}
