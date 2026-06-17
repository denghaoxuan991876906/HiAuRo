namespace HiAuRo.Infrastructure;

/// <summary>日志辅助工具 — 实体/技能/状态 ID 名字解析</summary>
public static class LogHelper
{
    /// <summary>解析实体 ID 到 "id(Name)" 格式，找不到名字时只返回数字</summary>
    public static string Entity(uint id)
    {
        if (id == 0 || id == 0xE0000000) return id.ToString();
        try
        {
            var obj = DService.Instance().ObjectTable?.SearchByID(id);
            if (obj != null)
                return $"{id}(\"{obj.Name}\")";
        }
        catch { }
        return id.ToString();
    }

    /// <summary>解析技能 ID 到 "id(Name)" 格式</summary>
    public static string Action(uint id)
    {
        if (id == 0) return "0";
        try
        {
            var sheet = DService.Instance().Data.GetExcelSheet<Lumina.Excel.Sheets.Action>();
            var row = sheet?.GetRow(id);
            if (row.HasValue && !string.IsNullOrEmpty(row.Value.Name.ToString()))
                return $"{id}(\"{row.Value.Name}\")";
        }
        catch { }
        return id.ToString();
    }

    /// <summary>解析状态 ID 到 "id(Name)" 格式</summary>
    public static string Status(uint id)
    {
        if (id == 0) return "0";
        try
        {
            var sheet = DService.Instance().Data.GetExcelSheet<Lumina.Excel.Sheets.Status>();
            var row = sheet?.GetRow(id);
            if (row.HasValue && !string.IsNullOrEmpty(row.Value.Name.ToString()))
                return $"{id}(\"{row.Value.Name}\")";
        }
        catch { }
        return id.ToString();
    }
}
