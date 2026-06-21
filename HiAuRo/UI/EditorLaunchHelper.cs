namespace HiAuRo.UI;

public static class EditorLaunchHelper
{
    public const string HostedToolsUrl = "https://denghaoxuan991876906.github.io/HiAuRo.WebTools/";

    public static string BuildWebToolsUrl(string? _fileName = null, string? _query = null)
    {
        // ponytail: UI 入口统一跳到托管 WebTools；如果站点以后恢复多页面或 query 路由，再在这里展开参数映射。
        return HostedToolsUrl;
    }

    public static System.Diagnostics.ProcessStartInfo Build(string url)
    {
        return new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        };
    }
}
