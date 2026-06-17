using Dalamud.Game.ClientState.Keys;

namespace HiAuRo.ACR;

/// <summary>
/// 键盘热键轮询器 —— 每帧检测已绑定的热键是否按下，触发对应动作
/// 使用 DService.KeyState 轮询（level-triggered）
/// </summary>
public static class HotkeyPoller
{
    /// <summary>上次轮询时按住的键（用于避免抖动）</summary>
    private static readonly HashSet<string> _heldKeys = [];

    /// <summary>每帧由 RuntimeCore.OnTick 调用</summary>
    public static void Update()
    {
        var bindings = HotkeyHelper.GetAllBindings();
        if (bindings.Count == 0) return;

        foreach (var (id, binding) in bindings)
        {
            if (string.IsNullOrEmpty(binding)) continue;

            var parsed = KeyBindingParser.Parse(binding);
            if (parsed == null) continue;

            var (modifiers, primary) = parsed.Value;

            var isDown = KeyBindingParser.AreModifiersHeld(modifiers) &&
                         KeyBindingParser.IsKeyDown(primary);

            // 边缘触发：只在刚按下时触发一次
            if (isDown && !_heldKeys.Contains(binding))
            {
                _heldKeys.Add(binding);
                HotkeyHelper.HandleKeyPress(binding);
            }
            else if (!isDown)
            {
                _heldKeys.Remove(binding);
            }
        }

        // 清理已解绑的键
        _heldKeys.RemoveWhere(k => !bindings.ContainsValue(k));
    }

    /// <summary>清空按住状态（插件卸载时调用，避免下次加载残留）</summary>
    public static void Clear() => _heldKeys.Clear();
}
