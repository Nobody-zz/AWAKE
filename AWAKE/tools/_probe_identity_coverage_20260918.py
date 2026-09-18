# -*- coding: utf-8 -*-
"""
身份覆盖面探针（2026-09-18，只读）

要回答的问题：修好职业名归一化之后，"你的身份决定了你听不到"这件事**有多常发生**？
如果它很少发生，那"让角色按身份说自己不知道"就是不值得做的功能；如果很多，它就是主线。

算的是：对每个身份，1050 条说法里有多少条授予他（grant 命中），多少条把他挡在门外。
不启动游戏、不改产品、纯统计上线包。
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

    # 表达按"能不能被某个身份听到"分组
    by_identity = {}
    for name in ROLE_TO_IDENTITY:
        full = "awake:identity:" + name
        heard = sum(1 for granted in expressions if full in granted)
        by_identity[name] = heard

    print("=" * 76)
    print("每个身份能听到多少条说法（分母 %d）" % total)
    print("=" * 76)
    for name, heard in sorted(by_identity.items(), key=lambda kv: kv[1]):
        blocked = total - heard
        print("  %-14s 能听 %4d (%5.1f%%)   被挡 %4d (%5.1f%%)"
              % (name, heard, 100.0 * heard / total, blocked, 100.0 * blocked / total))
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
