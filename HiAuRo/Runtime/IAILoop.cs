using HiAuRo.ACR;

namespace HiAuRo.Runtime;

public interface IAILoop
{
    bool Check();
    void Update(AIRunner runner);
    void Clear(AIRunner runner);
}
