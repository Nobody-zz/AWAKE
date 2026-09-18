# -*- coding: utf-8 -*-
"""
身份覆盖面探针（2026-09-18，只读）

要回答的问题：修好职业名归一化之后，"你的身份决定了你听不到"这件事**有多常发生**？
如果它很少发生，那"让角色按身份说自己不知道"就是不值得做的功能；如果很多，它就是主线。

算的是：对每个身份，全部说法里有多少条授予他（grant 命中），多少条把他挡在门外。
不启动游戏、不改产品、纯统计上线包。

⚠️ 2026-09-18 修订（**这一版之前的读数是错的**）：旧版是按「这个身份 id 有没有直接出现在
grant 里」算的，**没有计算身份继承**。而包里身份是一棵树（见下），一个人物**同时拥有他的全部祖先身份**——
例如 `ransom_broker` 还带着 `merchant`→`townsfolk`→`commoner`；`noble` 带着 `notable`→`commoner`。
⇒ 旧版的"能听"一律**偏低**、"被挡"一律**偏高**（差距在小身份上最大）。
现在两列都出：`能听`＝含祖先，`[仅本层]`＝只算自己（旧口径，留作对照）。
"""

import json
import os
from collections import Counter

PACKAGE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..",
                       "ModuleData", "Worldbook", "packages", "calradia", "runtime.json")

# 运行时能真正落到这些身份上的职业（含修好驼峰后的 12 个）
ROLE_TO_IDENTITY = {
    "villager": ["villager", "farmer"],
    "commoner": ["commoner"],
    "townsfolk": ["townsfolk", "wanderer", "preacher"],
    "headman": ["headman", "village_headman", "town_headman"],
    "notable": ["rural_notable", "notable", "arena_master"],
    "merchant": ["merchant", "goods_trader", "horse_trader", "weaponsmith", "armorer",
                 "artisan", "blacksmith", "shop_worker", "ship_wright"],
    "tavernkeeper": ["tavernkeeper", "tavern_game_host", "tavern_wench", "musician"],
    "ransom_broker": ["ransom_broker"],
    "soldier": ["soldier", "guard", "prison_guard", "caravan_guard", "banner_bearer", "mercenary"],
    "noble": ["noble"],
}


def load():
    with open(PACKAGE, encoding="utf-8") as handle:
        return json.load(handle)


def main():
    package = load()
    expressions = []
    for entry in package.get("entries") or []:
        for expression in entry.get("expressions") or []:
            granted = {r.get("identity_id") for r in (expression.get("grants") or [])}
            expressions.append(granted)
    total = len(expressions)
    print("上线包：条目 %d ／ 说法 %d ／ 身份 %d ／ 指路 %d"
          % (len(package.get("entries") or []), total,
             len(package.get("identities") or []), len(package.get("referrals") or [])))
    print("包版本：%s  revision=%s" % (package.get("version"), package.get("revision")))
    print("⚠️ 内容侧正在重编 ⇒ 本文件的数字**随时会漂**，引用时必须带日期与包版本。")
    print()

    # 组合集中度（上一轮审计里那条"83% 压两种"的复核）
    shapes = Counter(frozenset(s) for s in expressions)
    print("=" * 76)
    print("身份组合集中度")
    print("=" * 76)
    print("  不同的身份组合数 = %d" % len(shapes))
    for shape, count in shapes.most_common(4):
        label = sorted(i.replace("awake:identity:", "") for i in shape)
        print("  %5d 条 (%5.1f%%)  %s%s"
              % (count, 100.0 * count / total, label[:6], " …" if len(label) > 6 else ""))
    top2 = sum(count for _, count in shapes.most_common(2))
    print("  ⇒ 最集中的两种合计 %d 条，占 %.1f%%" % (top2, 100.0 * top2 / total))
    print()

    denies = sum(1 for e in package.get("entries") or []
                 for x in e.get("expressions") or [] if x.get("denies"))
    print("  带 denies（硬拒绝）的说法 = %d / %d (%.1f%%)" % (denies, total, 100.0 * denies / total))
    print()

    # 身份树：一个人物同时拥有自己的全部祖先身份（代码侧 AddIdentity 沿 parents 往上加）
    tree = {}
    for item in package.get("identities") or []:
        if not isinstance(item, dict):
            continue
        ident = str(item.get("id") or "")
        if ident:
            tree[ident] = [str(p) for p in (item.get("parents") or [])]

    def ancestors(ident, seen=None):
        seen = seen if seen is not None else set()
        if ident in seen:
            return set()
        seen.add(ident)
        out = {ident}
        for parent in tree.get(ident, []):
            out |= ancestors(parent, seen)
        return out

    print("身份树（← 左边是子、右边是它继承的父身份）：")
    for ident in sorted(tree):
        parents = tree[ident]
        label = ident.replace("awake:identity:", "")
        if parents:
            print("   %-20s ← %s" % (label, " ← ".join(p.replace("awake:identity:", "") for p in parents)))
        else:
            print("   %-20s （根）" % label)
    print()

    # 表达按"能不能被某个身份听到"分组（含继承）
    by_identity = {}
    own_only = {}
    for name in ROLE_TO_IDENTITY:
        full = "awake:identity:" + name
        reach = ancestors(full)
        by_identity[name] = sum(1 for granted in expressions if granted & reach)
        own_only[name] = sum(1 for granted in expressions if full in granted)

    print("=" * 76)
    print("每个身份能听到多少条说法（分母 %d）" % total)
    print("  「能听」＝含继承；「仅本层」＝只算直接写他名字的（旧口径，对照用）")
    print("=" * 76)
    for name, heard in sorted(by_identity.items(), key=lambda kv: kv[1]):
        blocked = total - heard
        print("  %-14s 能听 %4d (%5.1f%%)  [仅本层 %4d]   被挡 %4d (%5.1f%%)"
              % (name, heard, 100.0 * heard / total, own_only[name], blocked, 100.0 * blocked / total))
    print()
    print("  （对照）anonymous：能听 0 (0.0%%)   被挡 %d (100.0%%)" % total)
    print()

    print("=" * 76)
    print("差距：拿最常见的两个身份比")
    print("=" * 76)
    high = max(by_identity, key=by_identity.get)
    low = min(by_identity, key=by_identity.get)
    print("  最能听的是 %s（%d 条），最不能听的是 %s（%d 条）" % (high, by_identity[high], low, by_identity[low]))
    print("  同一个问题，换个人问，答案有没有——差别 %d 条，占 %.1f%%"
          % (by_identity[high] - by_identity[low],
             100.0 * (by_identity[high] - by_identity[low]) / total))
    print()
    print("读法：'被挡'这一列就是身份真正在管事的量。")
    print("  如果它接近 0 ⇒ 身份几乎不产生'他不知道'，那就不值得为它做功能；")
    print("  如果它很大 ⇒ 身份是主闸，'按身份说自己不知道'就是主线，得让角色说得出口。")


if __name__ == "__main__":
    main()
