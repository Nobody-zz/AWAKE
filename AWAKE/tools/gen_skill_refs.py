#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""把「发现提取」产出的 JSON 渲染成 skill 的 references/*.md。

用法:
  py -3 gen_skill_refs.py <findings.json> [<findings.json> ...] \
      --out <skill目录>/references --title "<skill 名>" [--min-reverify-only]

输入 JSON 结构（见提取流程）:
  { source_skills: [...],
    findings: [ {id, claim, kind, evidence, valid_for, needs_reverify, source} ],
    procedure_keep: [...], procedure_drop: [...] }

产出:
  <out>/INDEX.md             总览 + kind 分布 + 头部规则
  <out>/<kind>.md            按 kind 分组的发现表
  <out>/reverify-queue.md    所有 needs_reverify=true 的条目（按 kind 分组）
  <out>/procedure.md         keep / drop 两张表

纪律：本脚本只读 JSON、只写 out 目录，不碰任何其它文件。
"""
import argparse
import collections
import io
import json
import os
import re
import sys

KIND_ZH = {
    "engine-behavior": "引擎行为",
    "byte-layout": "字节布局",
    "db-schema": "数据库结构",
    "measured-number": "实测数值",
    "silent-failure": "静默失败",
    "api-quirk": "API 怪癖",
    "negative-claim": "否定式断言",
    "version-fact": "版本事实",
    "path-fact": "路径事实",
}
KIND_ORDER = list(KIND_ZH.keys())


def esc(s):
    """表格单元格里不能有裸换行或竖线。"""
    return (s or "").replace("|", "\\|").replace("\n", " ").replace("\r", " ").strip()


def clean_source(s):
    """出处只保留到「旧 skill 名 # 小节名」一级。

    旧 skill 在各 harness 的私有目录里（~/.workbuddy/skills 等），**不在本仓库**，
    所以其中的行号从仓库侧无法验证 —— 留着是「假的可点性」，不如去掉。
    """
    s = s or ""
    # 一个 L 引用：L123 或 L123-L456（也容忍全角波浪号）
    one = r"L\d+(?:\s*[-–~]\s*L?\d+)?"
    # 括号里可以是逗号/顿号分隔的**多个**引用，例如「（L378, L420-L421）」
    lst = one + r"(?:\s*[,，、]\s*" + one + r")*"
    s = re.sub(r"（\s*" + lst + r"\s*[。.]?\s*）", "", s)
    s = re.sub(r"\(\s*" + lst + r"\s*[.]?\s*\)", "", s)
    s = re.sub(r"\s*[:：]?\s*" + one + r"\s*[-–~]\s*L?\d+", "", s)
    s = re.sub(r"\s*[:：]\s*" + one + r"\s*$", "", s)
    return s.strip().rstrip("，,;；")


def load(paths):
    findings, keeps, drops, sources = [], [], [], []
    for p in paths:
        with io.open(p, encoding="utf-8") as fh:
            j = json.load(fh)
        sources.extend(j.get("source_skills") or [])
        for x in (j.get("findings") or []):
            x.setdefault("kind", "api-quirk")
            x.setdefault("needs_reverify", False)
            # 记住来源文件：id 只在单份文件内唯一，跨文件会重号（如多份都有 f01）。
            # 只按 id 去重会**静默丢掉**跨文件重号的条目 —— 2026-10-01 实测丢过 128/384 条。
            x["_src"] = os.path.basename(p)
            findings.append(x)
        keeps.extend(j.get("procedure_keep") or [])
        drops.extend(j.get("procedure_drop") or [])
    # 去重键 = (来源文件, id)。同文件内同 id 才是真重复。
    seen, out = set(), []
    for x in findings:
        k = (x.get("_src"), x.get("id") or x.get("claim"))
        if k in seen:
            continue
        seen.add(k)
        out.append(x)
    return out, keeps, drops, sources


def write(path, text):
    with io.open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)
    print("  wrote %s (%d bytes)" % (path, len(text.encode("utf-8"))))


def render_group(items, title):
    lines = ["# %s" % title, ""]
    lines.append("> 共 %d 条。`valid_for` 是这批事实**被测量时**的版本——引用前按当前版本复核。" % len(items))
    lines.append("")
    lines.append("| id | 事实 | 测量版本 | 待重验 | 出处 |")
    lines.append("|---|---|---|---|---|")
    for x in items:
        rv = "是" if x.get("needs_reverify") else ""
        lines.append("| %s | %s | %s | %s | %s |" % (
            esc(x.get("id")), esc(x.get("claim")), esc(x.get("valid_for")), rv, clean_source(x.get("source"))))
    lines.append("")
    lines.append("## 证据")
    lines.append("")
    for x in items:
        lines.append("- **%s** %s" % (esc(x.get("id")), esc(x.get("evidence"))))
    lines.append("")
    return "\n".join(lines)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("json", nargs="+")
    ap.add_argument("--out", default="")
    ap.add_argument("--single", default="", help="输出成单个 .md（仓库共享文档用）")
    ap.add_argument("--title", required=True)
    ap.add_argument("--reverify-only", action="store_true")
    args = ap.parse_args()
    if not args.out and not args.single:
        ap.error("either --out or --single is required")

    findings, keeps, drops, sources = load(args.json)

    by_kind = collections.defaultdict(list)
    for x in findings:
        by_kind[x["kind"]].append(x)

    total = len(findings)
    rv = [x for x in findings if x.get("needs_reverify")]
    print("装载 %d 条发现（待重验 %d），来源 skill %d 个" % (total, len(rv), len(set(sources))))

    HEADER = [
        "> 由 `AWAKE/tools/gen_skill_refs.py` 从既有 skill 的发现提取生成。**不要手改**——改了会被下次重跑覆盖。",
        ">",
        "> **这是跨 agent 的共享知识**（见 `AWAKE/AGENTS.md`〈跨 agent 共享知识〉）。任何 harness 的 agent 都应读这里，不要各自维护私有副本。",
        ">",
        "> 共 **%d** 条发现，其中 **%d** 条标了 `needs_reverify`（测量于旧版本，引用前复核）。" % (total, len(rv)),
        "",
        "## 头部规则",
        "",
        "1. **`valid_for` 是测量版本，不是当前版本。** 本机游戏已从 v1.3.15 升到 **v1.4.8**；凡 `valid_for=v1.3.15` 的，引用前先复核。",
        "2. **`未标版本` 的条目 = 提取时未记录版本**，请**按 v1.3.15 对待**（即同样需要复核）。",
        "3. **否定式断言（`negative-claim`）风险最高**——最容易因升级变成假话。先看文末〈待重验队列〉。",
        "4. **`出处` 只到「旧 skill 名 # 小节名」一级。** 旧 skill 在各 harness 的私有目录里（`~/.workbuddy/skills` 等），**不在本仓库**，故不保留行号——留着是假的可点性。",
        "5. 本文件是**世界里的事实**，不是程序。怎么做任务看对应 skill 或 `AWAKE/AGENTS.md`。",
        "",
    ]

    proc = ["## 程序要点（从既有 skill 提取）", "",
            "**仍可执行**", ""] + ["- " + esc(k) for k in keeps] + ["", "**已失效**（因 harness 变更）", ""] + \
           ["- " + esc(d) for d in drops] + [""]

    # ---- 只出待重验队列 ----
    if args.reverify_only:
        dest = args.single or os.path.join(args.out, "reverify-queue.md")
        parent = os.path.dirname(dest)
        if parent and not os.path.isdir(parent):
            os.makedirs(parent)
        write(dest, "\n".join(["# 待重验队列 · %s" % args.title, ""] + HEADER + [render_group(rv, "待重验队列")]))
        return 0

    # ---- 单文件模式（仓库共享文档）----
    if args.single:
        parts = ["# %s · 发现全集" % args.title, ""] + HEADER
        parts.append("## 按 kind 索引")
        parts.append("")
        parts.append("| kind | 条数 | 待重验 |")
        parts.append("|---|---|---|")
        for kind in KIND_ORDER:
            items = by_kind.get(kind)
            if not items:
                continue
            n_rv = len([x for x in items if x.get("needs_reverify")])
            parts.append("| `%s` | %d | %d |" % (kind, len(items), n_rv))
        parts.append("| **合计** | **%d** | **%d** |" % (total, len(rv)))
        parts.append("")
        for kind in KIND_ORDER:
            items = by_kind.get(kind)
            if items:
                parts.append("---")
                parts.append("")
                parts.append(render_group(items, "%s（%s）" % (KIND_ZH[kind], kind)))
        parts.append("---")
        parts.append("")
        parts.append("> **待重验**：本文件中标 `待重验=是` 的共 **%d** 条。" % len(rv))
        parts.append("> 完整队列（跨全部领域）见 `AWAKE/docs/reference/reverify-queue.md` —— 本文件不重复内联，避免同一事实出现两次。")
        parts.append("")
        parts.append("---")
        parts.append("")
        parts.append("\n".join(proc))
        write(args.single, "\n".join(parts))
        return 0

    out = args.out
    if not os.path.isdir(out):
        os.makedirs(out)

    if args.reverify_only:
        write(os.path.join(out, "reverify-queue.md"), render_group(rv, "待重验队列（%s）" % args.title))
        return 0

    # 每个 kind 一份
    for kind in KIND_ORDER:
        items = by_kind.get(kind)
        if not items:
            continue
        write(os.path.join(out, kind + ".md"),
              render_group(items, "%s · %s（%s）" % (args.title, KIND_ZH[kind], kind)))

    write(os.path.join(out, "reverify-queue.md"), render_group(rv, "待重验队列（%s）" % args.title))

    plines = ["# 程序要点（%s）" % args.title, "",
              "> 由既有 skill 提取。**keep** 是仍可执行的；**drop** 是因 harness 变更而失效的。", "",
              "## 仍可执行", ""]
    for k in keeps:
        plines.append("- " + esc(k))
    plines += ["", "## 已失效", ""]
    for d in drops:
        plines.append("- " + esc(d))
    plines.append("")
    write(os.path.join(out, "procedure.md"), "\n".join(plines))

    idx = ["# %s · 发现索引" % args.title, "",
           "> 由 `gen_skill_refs.py` 从既有 skill 的发现提取生成。**不要手改**——改了会被下次重跑覆盖。", "",
           "共 **%d** 条发现，其中 **%d** 条标了 `needs_reverify`（测量于旧版本，引用前复核）。" % (total, len(rv)),
           "", "## 头部规则", "",
           "1. **`valid_for` 是测量版本，不是当前版本。** 凡 `valid_for=v1.3.15` 而当前是 v1.4.8 的，引用前先复核。",
           "2. **否定式断言（`negative-claim`）风险最高**——它们最容易因版本升级而变成假话。先看 `reverify-queue.md`。",
           "3. 本目录**不是程序**。怎么做任务看 `../SKILL.md`；这里只是「世界里的事实」。", "",
           "## 按 kind 索引", "",
           "| kind | 条数 | 待重验 | 文件 |", "|---|---|---|---|"]
    for kind in KIND_ORDER:
        items = by_kind.get(kind)
        if not items:
            continue
        n_rv = len([x for x in items if x.get("needs_reverify")])
        idx.append("| %s | %d | %d | [%s.md](%s.md) |" % (kind, len(items), n_rv, kind, kind))
    idx += ["| **合计** | **%d** | **%d** | |" % (total, len(rv)), "",
            "另有 [procedure.md](procedure.md)（程序要点）与 [reverify-queue.md](reverify-queue.md)（全部待重验）。", ""]
    write(os.path.join(out, "INDEX.md"), "\n".join(idx))
    return 0


if __name__ == "__main__":
    sys.exit(main())
