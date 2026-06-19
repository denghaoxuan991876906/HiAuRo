#:project ../HiAuRo/HiAuRo.csproj
#:property PublishAot=false
#:property TargetFramework=net10.0-windows

using HiAuRo.ACR;

Test_默认查询技能9的RecastGroup();
Test_可查询指定技能的RecastGroup();
Console.WriteLine("=== 全部通过 ===");

void Test_默认查询技能9的RecastGroup()
{
    var actionId = 0u;
    var recastGroup = GCDHelper.ResolveRecastGroupForDuration(id =>
    {
        actionId = id;
        return 58;
    });

    if (actionId != 9)
        throw new Exception($"默认应查询技能 9，实际 {actionId}");
    if (recastGroup != 58)
        throw new Exception($"应返回技能 9 的 recast group，实际 {recastGroup}");
}

void Test_可查询指定技能的RecastGroup()
{
    var actionId = 0u;
    var recastGroup = GCDHelper.ResolveRecastGroupForDuration(id =>
    {
        actionId = id;
        return id == 3576 ? 58 : 0;
    }, 3576);

    if (actionId != 3576)
        throw new Exception($"应查询指定技能 3576，实际 {actionId}");
    if (recastGroup != 58)
        throw new Exception($"应返回指定技能的 recast group，实际 {recastGroup}");
}
