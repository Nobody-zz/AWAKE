# -*- coding: utf-8 -*-
"""前缀归一 · 第三段：把「档名」这一层的旧名换成新名，**不动「条目 id」那一层**。

★ 这两层必须分开，否则会把已发布的东西改坏：
   · 档名层（本次要改）：`docs/worldbook-migration/*.md` 报告/规格里的 `` `alley-struggle` `` 这类。
     它指的是「工作区里的哪个 .yaml 文件」，改名后必须跟着走，否则文档指不到档。
   · 条目 id 层（本次绝不能改）：`politics.town-gangs` / `awake:entry:politics.town-gangs`
     这类。它是**已发布的运行时主键**，`link-registry.v1.json` / `should-link.v3.json`
     里 1286 / 1394 条边都按它索引。改名会打断全部边表。

判据（只改这些，别的一律不碰）：
  1) 文本里出现在 **反引号 `` ` `` 或代码块里**、且等于旧档名的裸 slug
  2) 且该 slug 确实是这 8 档之一
  3) 且**不是**带域前缀的 id（即前面不紧邻 "." 且后面不带 "." + 更多词）
"""
import io
import os
import re

ROOT = r"D:\AWAKE-Dev\AWAKE"
REPORT = os.path.join(ROOT, "tools", "_underworld_docs_20260924.txt")

MAP = {
    "town-alleys":        "underworld-alleys",
    "alley-gang-leaders": "underworld-gang-leaders",
    "alley-struggle":     "underworld-struggle",
    "town-gangs":         "underworld-gangs",
    "crime-rating":       "underworld-crime-rating",
    "blood-money":        "underworld-blood-money",
    "bandits":            "underworld-bandits",
    "smuggling":          "underworld-smuggling",
}

TARGETS = [
    "docs/worldbook-migration/DARK-BATCH-REPORT-20260920.md",
    "docs/worldbook-migration/DARK-BATCH-SPEC-20260920.md",
]

lines = []


def log(s):
    lines.append(s)


def main():
    log("== 前缀归一 · 第三段：文档里的「档名」层 ==")
    log("")
    log("改：反引号里的裸档名          留：带域前缀的条目 id（已发布主键）")
    log("")

    total = 0
    for rel in TARGETS:
        p = os.path.join(ROOT, rel.replace("/", os.sep))
        src = io.open(p, encoding="utf-8").read()
        orig = src
        log("### %s" % rel)

        for old, new in MAP.items():
            # 只匹配反引号包裹的裸档名：`alley-struggle`
            # 负向断言排除 `.old` / `old.` / `old-xxx-name`（后面接更多词那种是别的档）
            pat = re.compile(r"`" + re.escape(old) + r"`")
            hits = pat.findall(src)
            if hits:
                src = pat.sub("`" + new + "`", src)
                log("   %-24s -> %-30s  %d 处" % (old, new, len(hits)))
                total += len(hits)

        if src != orig:
            io.open(p, "w", encoding="utf-8", newline="").write(src)
            log("   [已写]")
        else:
            log("   （无改动）")
        log("")

    log("合计改写 %d 处" % total)
    log("")
    log("★ 以下一律**未动**（属已发布主键，改了会打断边表）：")
    log("   · politics.town-gangs / politics.town-alleys …等带域前缀的条目 id")
    log("   · docs/worldbook-studio-plan/link-registry.v1.json（1286 条边）")
    log("   · docs/mappings/.../should-link.v3.json（1394 条边）")
    log("   · 两档 yaml 内的 id / assertion id / expression id")
    log("   · _dark_A_20260920.json（生成本源，其 slug 是 id 的来源）")

    with io.open(REPORT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print("\n".join(lines))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
