namespace HiAuRo.UI;

/// <summary>图标服务器——从 CAFEMAKER 获取游戏图标 URL</summary>
public static class IconServer
{
    private const string IconBaseUrl = "https://cafemaker.wakingsands.com/i";

    /// <summary>获取图标 URL</summary>
    public static string? GetIconUrl(uint iconId)
    {
        if (iconId == 0) return null;
        var baseDir = iconId / 1000 * 1000;
        return $"{IconBaseUrl}/{baseDir:D6}/{iconId:D6}_hr1.png";
    }
}
