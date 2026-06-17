#!/usr/bin/env python3
"""
HiAuRo PR AI 审核脚本（增强版）
- 结构化 JSON 输出：严重性 / 文件 / 行号 / 描述 / 建议
- 行级评论：通过 GitHub PR Review API 精确标注
- 超长上下文：DeepSeek 1M 上下文，读取更多文件更深内容
- 融入项目规范：AGENTS.md / PROJECT.md / STACK.md
"""

import json
import os
import re
import subprocess
import sys
import time

from openai import OpenAI

# ─── Shell helpers ───────────────────────────────────────────────────────


def run(cmd: str) -> str:
    result = subprocess.run(cmd, shell=True, capture_output=True, text=True)
    if result.returncode != 0 and result.stderr.strip():
        print(f"WARNING: {cmd}\n{result.stderr}", file=sys.stderr)
    return result.stdout.strip()


def pr_number() -> str:
    return os.environ["PR_NUMBER"]


def repo() -> str:
    return os.environ["REPO"]


def head_sha() -> str:
    return os.environ["HEAD_SHA"]


# ─── Data collection ─────────────────────────────────────────────────────


def get_pr_diff() -> str:
    return run(f"gh pr diff {pr_number()} --repo {repo()}")


def get_changed_files() -> list[str]:
    out = run(f"gh pr view {pr_number()} --repo {repo()} --json files --jq '.files[].path'")
    if not out:
        return []
    return [f.strip() for f in out.split("\n") if f.strip()]


def read_file_with_lines(path: str, max_lines: int = 1000) -> str:
    try:
        with open(path, "r", encoding="utf-8", errors="ignore") as f:
            lines = f.readlines()
        total = len(lines)
        selected = lines[:max_lines]
        numbered = []
        for i, line in enumerate(selected, 1):
            numbered.append(f"{i:5d}|{line.rstrip()}")
        result = "\n".join(numbered)
        if total > max_lines:
            result += f"\n     ... | ... ({total - max_lines} more lines)"
        return result
    except Exception:
        return f"[无法读取: {path}]"


def build_context(diff: str, changed_files: list[str]) -> str:
    parts = []
    parts.append("## 项目代码审查请求")
    parts.append("")
    parts.append("### PR Diff (diff 格式，`-` 删除行，`+` 新增行)")
    parts.append("```diff")
    parts.append(diff)
    parts.append("```")

    code_exts = {".cs", ".csproj", ".xaml", ".json", ".xml", ".py", ".yml", ".yaml", ".sh", ".cmd"}
    code_files = [f for f in changed_files if os.path.splitext(f)[1] in code_exts]
    code_files = code_files[:30]

    for f in code_files:
        ext = os.path.splitext(f)[1]
        lang = "csharp" if ext in {".cs", ".csproj"} else ""
        content = read_file_with_lines(f)
        parts.append(f"\n### 文件: {f} (行号已标注)")
        parts.append(f"```{lang}")
        parts.append(content)
        parts.append("```")

    return "\n".join(parts)


# ─── System prompt ───────────────────────────────────────────────────────

SYSTEM_PROMPT = """你是 HiAuRo 项目的代码审查专家。

## 项目概要
HiAuRo 是 FFXIV Dalamud 战斗辅助框架（.NET 10 + Dalamud.NET.Sdk 15.0 + OmenTools）。
三轴架构：执行轴（类AE时间轴）和事实轴（Boss时间表）互斥，辅助轴（安全坐标计算）始终运行。
架构风格：平直直接，中文注释，不过度抽象。新能力 additive 叠加，不做推翻式重构。

## 三轴约束
- 执行轴和事实轴互斥（同一时间只用一个）
- 辅助轴独立于模式切换始终运行，不要把辅助轴代码放到执行轴或事实轴里
- Qt 开关（爆发/爆发药/停手/AOE/TTK）是全职业统一控制机制
- OnGameEvent 是 ACR 作者副本精调的主要入口

## 架构约束（必须检查）
1. Svc.ClientState.LocalPlayer 已废弃 → 用 IObjectTable.LocalPlayer
2. OmenTools 已提供的能力禁止重复封装（见下方速查表）
3. 敌人判断不用单一 IsEnemy()，需结合 ObjectKind / OwnerId / BuddyList / IsTargetable
4. 队伍/友方用 ICharacter.StatusFlags，敌人用 ICharacter.BattalionFlags (Enemy=4)
5. 不改动 OmenTools/ 和 Browsingway/ 子模块代码
6. 不引入额外 IoC/ServiceLocator/WinForms/WPF 依赖

## OmenTools 即用即取速查（看到自己封装的就要指出来）
| 需求 | 直接用 | 不要做 |
|------|--------|--------|
| 对象表 | DService.Instance().ObjectTable（零分配） | 自己封装 ObjectTable |
| 队伍判断 | ICharacter.StatusFlags | ObjectTable.SearchByID() 查 OwnerID |
| 敌人判断 | ICharacter.BattalionFlags (Enemy=4) | 多层 if 组合推断 |
| 玩家状态 | LocalPlayerState.* | 自己读 ClientState |
| 战斗状态 | GameState.* + DService.Condition.* | 自己组合 ICondition |
| 目标链 | TargetManager.Target（可读写） | 原生 ITargetManager |
| 技能释放 | UseActionManager.UseAction() | 封装 ActionManager |
| 帧调度 | FrameworkManager.Reg(method, throttleMS) | 写 Update 循环 |
| 距离计算 | LocalPlayerState.DistanceToObject2D/3D（含 hitbox） | Vector3.Distance |
| Buff 查询 | IBattleChara.StatusList.HasStatus/TryGetStatus | 遍历 StatusList |
| 对象分类 | IObjectTable.CharactersRange (..200) | 遍历 729 槽 |
| 伙伴查询 | BuddyList 预缓存到 HashSet<uint> | 嵌套遍历 BuddyList |
| 对象引用 | member.GameObject as IPlayerCharacter | CreateObjectReference() |

## 常见陷阱
- 不迭代 IPartyMember.GameObject 多次，每扫描周期只解析一次
- 文件命名用中文描述（如 BRD_GCD_强力射击.cs），不用缩写
- HiAuRo.Data 是薄转发层，不是仓库层

## 审查重点
1. Bug / 逻辑错误 / 空引用
2. 性能问题（重复遍历、不必要的分配、装箱）
3. 违反上述架构约束
4. 安全（密钥泄露、注入）

## 输出要求
你必须输出一个严格合法的 JSON 对象，字段如下：
```json
{
  "summary": "一段中文总体评价，2-5 句",
  "findings": [
    {
      "severity": "error 或 warning 或 suggestion",
      "file": "相对于仓库根目录的文件路径，如 HiAuRo/Data/PartyData.cs",
      "line": 42,
      "description": "问题描述",
      "suggestion": "修复建议"
    }
  ]
}
```
- 如果没有发现问题，findings 为空数组 []
- line 字段使用文件内容中标注的行号（5 位数字前缀）
- severity: error=必须修复, warning=建议修复, suggestion=可选优化
- 最多返回 10 条 findings，按严重性排序（error 在前）
- 不要在 JSON 外输出任何其他内容"""


# ─── DeepSeek call ───────────────────────────────────────────────────────


def call_deepseek(prompt: str, api_key: str) -> dict | None:
    client = OpenAI(api_key=api_key, base_url="https://api.deepseek.com")

    for attempt in range(3):
        try:
            response = client.chat.completions.create(
                model="deepseek-v4-flash",
                messages=[
                    {"role": "system", "content": SYSTEM_PROMPT},
                    {"role": "user", "content": f"请审查以下 PR，输出 JSON：\n\n{prompt}"},
                ],
                temperature=0.1,
                max_tokens=4096,
                response_format={"type": "json_object"},
                stream=False,
            )
            text = response.choices[0].message.content
            print(f"Raw response ({len(text)} chars):\n{text[:500]}")
            return extract_json(text)
        except Exception as e:
            print(f"Attempt {attempt + 1} failed: {e}", file=sys.stderr)
            if attempt < 2:
                time.sleep(3)

    return None


def extract_json(text: str) -> dict | None:
    text = text.strip()
    match = re.search(r"\{.*\}", text, re.DOTALL)
    if not match:
        return None
    try:
        return json.loads(match.group())
    except json.JSONDecodeError:
        return None


# ─── Post review ─────────────────────────────────────────────────────────


def post_review(result: dict, commit_id: str):
    summary = result.get("summary", "（无总结）")
    findings = result.get("findings", [])

    valid_findings = []
    for f in findings:
        path = f.get("file", "")
        line = f.get("line", 0)
        if path and isinstance(line, int) and line > 0:
            valid_findings.append({
                "path": path,
                "line": line,
                "body": f"**{f.get('severity', 'suggestion').upper()}**: {f.get('description', '')}\n\n💡 {f.get('suggestion', '')}",
            })

    if not valid_findings:
        body = f"##  🤖 AI 代码审查\n\n### ✅ 通过\n\n{summary}\n\n> *AI 审核仅供参考，合并请自行判断*"
        tmp = "/tmp/pr_review_comment.txt"
        with open(tmp, "w", encoding="utf-8") as f:
            f.write(body)
        run(f"gh pr comment {pr_number()} --repo {repo()} --body-file {tmp}")
        return

    body = (
        f"##  🤖 AI 代码审查\n\n"
        f"###  摘要\n{summary}\n\n"
        f"###  发现问题 ({len(valid_findings)} 条)\n\n"
        f"> *AI 审核仅供参考，合并请自行判断*"
    )

    payload = json.dumps({
        "commit_id": commit_id,
        "body": body,
        "event": "COMMENT",
        "comments": valid_findings,
    }, ensure_ascii=False)

    tmp = "/tmp/pr_review_payload.json"
    with open(tmp, "w", encoding="utf-8") as f:
        f.write(payload)

    run(f"gh api repos/{repo()}/pulls/{pr_number()}/reviews --input {tmp} --method POST")
    print(f"Posted review with {len(valid_findings)} line comments")


def post_fallback_comment(text: str):
    comment = f"##  🤖 AI 代码审查\n\n⚠️ 结构化解析失败，以下为原始输出：\n\n{text}\n\n> *AI 审核仅供参考，合并请自行判断*"
    tmp = "/tmp/pr_review_comment.txt"
    with open(tmp, "w", encoding="utf-8") as f:
        f.write(comment)
    run(f"gh pr comment {pr_number()} --repo {repo()} --body-file {tmp}")


# ─── Main ────────────────────────────────────────────────────────────────


def main():
    api_key = os.environ.get("DEEPSEEK_API_KEY", "")
    if not api_key:
        print("DEEPSEEK_API_KEY 未设置，跳过审核")
        return

    print("获取 PR diff...")
    diff = get_pr_diff()
    if not diff:
        print("无 diff，跳过审核")
        return

    print("获取变更文件列表...")
    changed_files = get_changed_files()
    print(f"变更文件 ({len(changed_files)}): {changed_files}")

    print("构建上下文...")
    context = build_context(diff, changed_files)

    MAX_CTX = 800000
    if len(context) > MAX_CTX:
        context = context[:MAX_CTX] + "\n\n[上下文过长，已截断]"
    print(f"上下文长度: {len(context)} chars")

    print("调用 DeepSeek API...")
    result = call_deepseek(context, api_key)

    if result:
        print("发布 PR review...")
        post_review(result, head_sha())
    else:
        print("JSON 解析失败，降级为普通 comment")
        post_fallback_comment("未能从 AI 响应中提取结构化结果，请手动审查。")

    print("✅ 审核完成")


if __name__ == "__main__":
    main()
