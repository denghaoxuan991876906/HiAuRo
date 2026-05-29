using System.Numerics;
using Dalamud.Interface.Textures;

namespace HiAuRo.UI;

/// <summary>
/// 职业图标工具 —— ClassJob RowId + 62100 偏移获取 Framed 职业图标
/// </summary>
public static class JobIconHelper
{
    private const uint IconOffset = 62100;

    /// <summary>图标纹理缓存（持有 ISharedImmediateTexture 防止 GC）</summary>
    private static readonly Dictionary<uint, ISharedImmediateTexture> _iconCache = [];

    /// <summary>获取职业图标的 ImGui 句柄</summary>
    public static ImTextureID GetJobIconHandle(uint classJobRowId)
    {
        if (_iconCache.TryGetValue(classJobRowId, out var sharedTex))
            return sharedTex.GetWrapOrDefault()?.Handle ?? default;

        var iconId = classJobRowId + IconOffset;
        sharedTex = DService.Instance().Texture.GetFromGameIcon(new GameIconLookup(iconId));
        _iconCache[classJobRowId] = sharedTex;
        return sharedTex.GetWrapOrDefault()?.Handle ?? default;
    }

    /// <summary>绘制职业图标（ImGui Image，size 默认 32x32）</summary>
    public static void DrawJobIcon(uint classJobRowId, float size = 32f)
    {
        var handle = GetJobIconHandle(classJobRowId);
        if (handle != (nint)0)
            ImGui.Image(handle, new Vector2(size, size));
    }
}
