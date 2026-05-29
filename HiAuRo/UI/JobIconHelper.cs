using System.Numerics;
using Dalamud.Interface.Textures;

namespace HiAuRo.UI;

/// <summary>
/// 职业图标工具 —— 通过 DataManager 查 ClassJob sheet 获取图标
/// </summary>
public static class JobIconHelper
{
    /// <summary>图标纹理缓存（持有 ISharedImmediateTexture 防止 GC）</summary>
    private static readonly Dictionary<uint, ISharedImmediateTexture> _iconCache = [];

    /// <summary>获取职业图标的 ImGui 句柄</summary>
    public static nint GetJobIconHandle(uint classJobRowId)
    {
        if (_iconCache.TryGetValue(classJobRowId, out var sharedTex))
            return sharedTex.GetWrapOrDefault()?.Handle ?? 0;

        var sheet = DService.Instance().Data.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>();
        var row = sheet?.GetRow(classJobRowId);
        if (row == null) return 0;

        sharedTex = DService.Instance().Texture.GetFromGameIcon(
            new GameIconLookup(row.Value.Icon));
        _iconCache[classJobRowId] = sharedTex;
        return sharedTex.GetWrapOrDefault()?.Handle ?? 0;
    }

    /// <summary>绘制职业图标（ImGui Image，size 默认 32x32）</summary>
    public static void DrawJobIcon(uint classJobRowId, float size = 32f)
    {
        var handle = GetJobIconHandle(classJobRowId);
        if (handle == 0) return;
        ImGui.Image(handle, new Vector2(size, size));
    }
}
