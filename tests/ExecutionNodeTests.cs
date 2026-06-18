#:project ../HiAuRo/HiAuRo.csproj
#:property PublishAot=false
#:property TargetFramework=net10.0-windows

using HiAuRo.Execution;

Test_EvalContext_CreateChild_复制变量和等待委托();
Console.WriteLine("=== 全部通过 ===");

void Test_EvalContext_CreateChild_复制变量和等待委托()
{
    var parent = new EvalContext
    {
        BattleTimeMs = 123,
        WaitCondFn = _ => Task.FromResult(true)
    };
    parent.SetVariable("phase", 7);

    var child = parent.CreateChild();

    if (ReferenceEquals(parent, child))
        throw new Exception("CreateChild 应返回新实例");
    if (child.BattleTimeMs != 123)
        throw new Exception("子上下文应复制 BattleTimeMs");
    if (child.WaitCondFn == null)
        throw new Exception("子上下文应复制 WaitCondFn");
    if (child.GetVariable("phase") != 7)
        throw new Exception("子上下文应复制变量值");

    child.SetVariable("phase", 9);
    if (parent.GetVariable("phase") != 7)
        throw new Exception("子上下文变量变更不应污染父上下文");
}
