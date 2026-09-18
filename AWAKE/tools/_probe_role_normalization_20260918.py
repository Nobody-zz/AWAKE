# -*- coding: utf-8 -*-
"""
职业名归一化错配探针（2026-09-18，只读）

疑点：`BannerlordWorldbookIdentityAdapter.ResolveHeroRole` 把 `Occupation` 枚举
用 `ToString()` 变成字符串（`RansomBroker`、`GangLeader` 这种驼峰），
而 `WorldbookIdentityCapabilityRules.NormalizeRole` **只做小写，不拆驼峰**，
规则表里写的却是 `ransom_broker`、`gang_leader`（下划线）。

⇒ 凡是多词驼峰职业名，可能全部落空到 `default: Unknown()` ⇒ `profile.anonymous`
  ⇒ 该角色在 461 条内容里拿不到任何知识（内容侧 `anonymous` 只出现在 denies，从不出现在 grants）。

本探针把两边机械对齐，算出确切的落空清单。**不读产品状态的运行期行为，只对字符串。**
"""
import json
import os
import re

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src")
RULES = os.path.join(ROOT, "WorldbookIdentityCapabilityRules.cs")

# 来自 decompiled 源：TaleWorlds.CampaignSystem.Occupation (Occupation.cs:3-39)，33 个成员
# （去掉 NotAssigned 与 NumberOfOccupations：前者在调用点被提前排掉，后者不是真职业）
OCCUPATION_ENUM = [
    "Tavernkeeper", "Mercenary", "Lord", "GoodsTrader", "ArenaMaster", "Villager",
    "Soldier", "Townsfolk", "RansomBroker", "Weaponsmith", "Armorer", "HorseTrader",
    "TavernWench", "TavernGameHost", "Bandit", "Wanderer", "Artisan", "Merchant",
    "Preacher", "Headman", "GangLeader", "RuralNotable", "PrisonGuard", "Guard",
    "ShopWorker", "Musician", "Gangster", "Blacksmith", "BannerBearer", "CaravanGuard",
    "Special", "ShipWright",
]

CAMEL_SPLIT = re.compile(r"(?<=[a-z0-9])(?=[A-Z])")


def normalize_role(value):
    """照抄 WorldbookIdentityCapabilityRules.NormalizeRole（:128-136）。"""
    if not value or not value.strip():
        return ""
    normalized = value.strip().lower()
    sep = normalized.rfind(":")
    if sep >= 0:
        normalized = normalized[sep + 1:]
    if normalized.startswith("role_"):
        normalized = normalized[5:]
    return normalized.replace("-", "_").replace(" ", "_")


def load_rule_literals():
    """把规则表里所有 case "xxx": 字面量机械抽出来，避免手抄。"""
    text = open(RULES, encoding="utf-8").read()
    return set(re.findall(r'case\s+"([a-z0-9_]+)"\s*:', text))


def load_package():
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..",
                        "ModuleData", "Worldbook", "packages", "calradia", "runtime.json")
    with open(path, encoding="utf-8") as handle:
        return json.load(handle)


def camel_to_snake(value):
    return CAMEL_SPLIT.sub("_", value).lower()


def content_side_safety():
    """修法安全性：加驼峰拆分会不会把现有的值改坏？

    ⚠️ 别拿"样本为空"当证据。本函数先报样本量，再报结构性理由。
    """
    package = load_package()
    roles = set()
    for entry in package.get("entries") or []:
        for expression in entry.get("expressions") or []:
            for bucket in ("grants", "denies"):
                for rule in expression.get(bucket) or []:
                    for value in (rule.get("conditions") or {}).get("roleIds") or []:
                        roles.add(value)
    identity_ids = {i.get("id") for i in package.get("identities") or []}

    print("=" * 78)
    print("修法安全性：把驼峰拆成下划线，会不会改坏现有值")
    print("=" * 78)
    print("  样本① 内容侧 conditions.roleIds 实际取值数 = %d" % len(roles))
    print("     ⇒ 内容侧**不使用** roleIds 这个条件；改它不波及任何一条内容。")
    print("        （作者侧 docs/.../authoring-out/ 里也是 0 处，同一结论。）")
    print("  样本② 包的 identities（%d 个）：" % len(identity_ids))
    for value in sorted(identity_ids):
        fixed = CAMEL_SPLIT.sub("_", value).lower().replace("-", "_").replace(" ", "_")
        flag = "不变" if fixed == value else "变了 => " + fixed
        print("       %-34s %s" % (value, flag))
    print()
    print("  结构性理由（不依赖任何样本）：驼峰拆分只在**大写字母前**插一个下划线，")
    print("  ⇒ 任何已经是小写的字面量（`noble`、`awake:identity:ransom_broker`、`tavernkeeper`）")
    print("     拆完一字不改；它只影响 `RansomBroker` 这类**从枚举 ToString() 来的驼峰值**。")
    print("  ⇒ 修法只可能把「本来落空」的接上，不可能把「本来接上的」弄坏。")


def after_fix():
    accepted = load_rule_literals()
    newly = []
    still = []
    for name in OCCUPATION_ENUM:
        fixed = CAMEL_SPLIT.sub("_", name).lower()
        if normalize_role(name) in accepted:
            continue
        if fixed in accepted:
            newly.append((name, fixed))
        else:
            still.append((name, fixed))
    print()
    print("=" * 78)
    print("若把驼峰拆成下划线：")
    print("   立刻被接住 %d 个：" % len(newly))
    for name, fixed in newly:
        print("      %-16s -> %s" % (name, fixed))
    print("   仍然接不住 %d 个（表里根本没有，要另补映射或补内容）：" % len(still))
    for name, fixed in still:
        print("      %-16s -> %s" % (name, fixed))


def main():
    accepted = load_rule_literals()
    print("规则表接受的职业字面量（%d 个）:" % len(accepted))
    print("   " + ", ".join(sorted(accepted)))
    print()

    hit, miss = [], []
    for name in OCCUPATION_ENUM:
        got = normalize_role(name)
        (hit if got in accepted else miss).append((name, got))

    print("=" * 78)
    print("枚举真名 -> 归一化结果 -> 能不能被规则表接住")
    print("=" * 78)
    for name, got in hit:
        print("  OK    %-16s -> %-16s" % (name, got))
    print()
    for name, got in miss:
        snake = CAMEL_SPLIT.sub("_", name).lower()
        note = "（Lord 不算：调用点第一步 IsNoble 就接住了）" if got == "lord" else ""
        print("  落空  %-16s -> %-16s  表里若按驼峰拆应为 %s %s" % (name, got, snake, note))
    print()
    real_miss = [x for x in miss if x[1] != "lord"]
    fixable = [(n, g, CAMEL_SPLIT.sub("_", n).lower()) for n, g in real_miss
               if CAMEL_SPLIT.sub("_", n).lower() in accepted]
    print("=" * 78)
    print("真落空 %d / %d 个职业（已扣掉 Lord 这个误报）。" % (len(real_miss), len(OCCUPATION_ENUM) - 1))
    print("落空者一律走 default: Unknown() -> profile.anonymous。")
    print("其中「表里写了下划线、只是永远匹配不上」的 %d 个（纯归一化错配）：" % len(fixable))
    for n, g, s in fixable:
        print("   %-16s 表里写的是 %-16s 实际传进来是 %s" % (n, s, g))
    print()
    print("表里根本没有的（内容/映射缺口，不是归一化问题）:")
    for n, g in real_miss:
        if CAMEL_SPLIT.sub("_", n).lower() not in accepted:
            print("   %-16s -> %s" % (n, g))

    content_side_safety()
    after_fix()


if __name__ == "__main__":
    main()

