using HiAuRo.ImGuiLib;

namespace HiAuRo.UI.Tabs;

public abstract class TabPageBase
{
    public string Name { get; }
    public string RouteKey { get; }
    public string Icon { get; }
    public bool IsVisible { get; set; } = true;

    protected TabPageBase(string name, string routeKey, string icon)
    {
        Name = name;
        RouteKey = routeKey;
        Icon = icon;
    }

    public virtual void OnOpen() { }
    public virtual void OnClose() { }
    public abstract void DrawContent();
}
