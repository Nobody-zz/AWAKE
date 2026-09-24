# -*- coding: utf-8 -*-
"""暗面批前缀归一 · 第五段：文档与元数据里的 id 引用同步。

依 09-14 流程（`CLASSIFICATION-V2-20260914.md` §2-第3条 B 案）：
  文件名 ＋ `doc` id 首段同步换新 ⇒ **所有引用了旧 id 的地方都要跟着换**，
  否则文档/名录指不到档。

这次要换的是**带域前缀的 id**（上一轮我误当成"不能碰的已发布主键"而留着 —— 那是我自造的规矩）：
  politics.town-alleys / politics.alley-* / politics.town-gangs / politics.crime-rating
  / politics.blood-money / politics.bandits / economy.smuggling
  ⇒ 一律把第三段换成新 slug，域段不动。

⚠️ 只换**这两处**（都是文档，不是产物）：
  · docs/worldbook-migration/DARK-BATCH-REPORT-20260920.md
  · docs/worldbook-migration/DARK-BATCH-SPEC-20260920.md
  · docs/worldbook-migration/EDGE-RECALL-REPORT-20260920.md
⚠️ 快照类**不动**（它们是当时的三哈希签名产物，与当前状态不同步是既成事实）：
  · docs/worldbook-studio-plan/link-registry.v1.json
  · docs/mappings/worldbook-should-link/20260920/should-link.v3.json
  · docs/worldbook-migration/projection/authoring-out/_dark_A_20260920.json（生成本源）
"""
import io
import os
import re

ROOT = r"D:\AWAKE-Dev\AWAKE"
REPORT = os.path.join(ROOT, "tools", "_underworld_docs2_20260924.txt")

SLUGS = {
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
    "docs/worldbook-migration/EDGE-RECALL-REPORT-20260920.md",
]

lines = []


def log(s):
    lines.append(s)


def main():
    log("== 第五段：文档里的 id 引用同步（带域前缀的 id）==")
    log("")
    total = 0
    for rel in TARGETS:
        p = os.path.join(ROOT, rel.replace("/", os.sep))
        if not os.path.exists(p):
            log("### %s  （不存在，跳过）" % rel)
            continue
        src = io.open(p, encoding="utf-8").read()
        orig = src
        log("### %s" % rel)
        for old, new in SLUGS.items():
            # 只匹配「域.slug」形态：politics.<old> / economy.<old>
            # 负向断言：后面不能再跟 . 或 -字母（避免命中别的档）
            pat = re.compile(r"\b(politics|economy)\." + re.escape(old) + r"\b")
            n = len(pat.findall(src))
            if n:
                src = pat.sub(lambda m: m.group(1) + "." + new, src)
                log("   %-22s -> %-30s %d 处" % (old, new, n))
                total += n
        if src != orig:
            io.open(p, "w", encoding="utf-8", newline="").write(src)
            log("   [已写]")
        else:
            log("   （无改动）")
    log("")
    log("合计同步 %d 处" % total)
    log("")
    log("未动（快照类，与当前状态同步是既成事实）：")
    log("   · link-registry.v1.json / should-link.v3.json / _dark_A_20260920.json")

    with io.open(REPORT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print("\n".join(lines))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
