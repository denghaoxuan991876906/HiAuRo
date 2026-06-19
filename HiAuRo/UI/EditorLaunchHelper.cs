namespace HiAuRo.UI;

public static class EditorLaunchHelper
{
    public static System.Diagnostics.ProcessStartInfo Build(string url)
    {
        return new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        };
    }
}
