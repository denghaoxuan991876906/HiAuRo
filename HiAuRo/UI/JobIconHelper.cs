using System.Numerics;
using Dalamud.Interface.Textures;

namespace HiAuRo.UI;

/// <summary>
/// 职业图标工具 —— 通过游戏图标ID映射获取职业图标
/// </summary>
public static class JobIconHelper
{
    /// <summary>图标纹理缓存（持有 ISharedImmediateTexture 防止 GC）</summary>
    private static readonly Dictionary<uint, ISharedImmediateTexture> _iconCache = [];

    /// <summary>ClassJob RowId → 高分辨率职业图标 ID（DT 7.x）</summary>
    private static readonly Dictionary<uint, uint> _jobIconMap = new()
    {
        {19, 62101}, {21, 62102}, {32, 62103}, {37, 62104},
        {24, 62105}, {28, 62106}, {33, 62107}, {40, 62108},
        {20, 62109}, {22, 62110}, {30, 62111}, {34, 62112}, {39, 62113}, {41, 62114},
        {23, 62115}, {31, 62116}, {38, 62117},
        {25, 62118}, {27, 62119}, {35, 62120}, {42, 62121},
    };

    /// <summary>获取职业图标的 ImGui 句柄</summary>
    public static ImTextureID GetJobIconHandle(uint classJobRowId)
    {
        if (_iconCache.TryGetValue(classJobRowId, out var sharedTex))
            return sharedTex.GetWrapOrDefault()?.Handle ?? default;

        if (!_jobIconMap.TryGetValue(classJobRowId, out var iconId))
            return default;

        sharedTex = DService.Instance().Texture.GetFromGameIcon(new GameIconLookup(iconId));
        _iconCache[classJobRowId] = sharedTex;
        return sharedTex.GetWrapOrDefault()?.Handle ?? default;
    }

    /// <summary>绘制职业图标（ImGui Image，size 默认 32x32）</summary>
    public static void DrawJobIcon(uint classJobRowId, float size = 32f)
    {
        var handle = GetJobIconHandle(classJobRowId);
        if ((nint)handle == 0) return;
        ImGui.Image(handle, new Vector2(size, size));
    }
}
