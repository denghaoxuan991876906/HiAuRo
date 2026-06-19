#:project ../HiAuRo/HiAuRo.csproj
#:property PublishAot=false
#:property TargetFramework=net10.0-windows

using HiAuRo.UI;

Test_EditorLaunch_应使用ShellExecute打开本地Web阅读器();
Console.WriteLine("=== 全部通过 ===");

void Test_EditorLaunch_应使用ShellExecute打开本地Web阅读器()
{
    var psi = EditorLaunchHelper.Build("http://localhost:5678/combat-recorder.html");

    if (!psi.UseShellExecute)
        throw new Exception("阅读器 URL 应通过 UseShellExecute=true 交给系统默认浏览器打开");
    if (psi.FileName != "http://localhost:5678/combat-recorder.html")
        throw new Exception($"阅读器 URL 构造错误: {psi.FileName}");
}
