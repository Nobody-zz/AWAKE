# -*- coding: utf-8 -*-
"""
《小阵营》L2 补料（2026-09-24）

按甲方裁定「七变体不拆散，整条进《小阵营》」，把《火焰余烬》六变体
按「说话人层级」并成三条表达（rumor/summary/detail），并补 B 类引文。
**只改 L2**（`_dark_A_20260920.json`）—— 生成器与双写 yaml 由 `_gen_dark_20260920.py` 负责。
"""
import io
import json
import os

L2 = r"D:\AWAKE-Dev\AWAKE\docs\worldbook-migration\projection\authoring-out\_dark_A_20260920.json"

# ── 新三条表达的文本 ─────────────────────────────────────────────────
# 说明：**这是"把原句的口吻落成对白"，不是照抄原句**（照抄进正文违反"正文不得现当代视角"的既定纪律，
#      且原句是第三人称叙述）。引文（B 栏）才是逐字原句，供校验器查连续子串。
RUMOR_TEXT = (
    "有的地方，过路是要交钱的——商家按月交，农户按季交，交了就没人来掀你的摊子，"
    "不交就隔三差五有醉汉上门闹事，报官也没用。他们到底是谁，说不清：白天在街上碰见，"
    "也就是隔壁那条巷子里的街坊，有人还拄着拐一瘸一拐的；可收了摊往巷子里一蹲，"
    "腿就不瘸了。有人讲他们拜一个死了好多年的皇帝，就等着他回来——死了皇帝不埋，"
    "这么些年还能说出个名堂来，怎么听怎么像喝多了编的。反正离得远，谁知道呢。"
)

SUMMARY_TEXT = (
    "他们自己不叫自己匪帮，说自己是「被遗忘者的工会」：你摆摊没人管，他们管；"
    "你死了儿子没人收尸，他们替你收。他们讲自己拜的不是账本，是一个死了一百年的皇帝，"
    "等的是下一代人的好日子——说是这么说，会里的人也承认自己七岁跟着爹进会，"
    "如今鬓角白了，天还是没亮，念一次经，手里那把刀就沉一分。"
    "同行看他们又是另一套说法：同业，但不是同行——一个为了利，一个一半为了利、一半为了等谁回来；"
    "念着经把刀子捅了，捅完还跪地上求原谅。"
    "但同行也认一件事：他们接活讲规矩，交了钱的不抢、交了粮的不烧、灾年还给欠账的宽限，"
    "官府来围剿，他们不躲山头，就躲在隔壁老瘸子家后院的地窖里——官府想不到。"
    "他们的根基不是钱，是街坊。"
)

DETAIL_TEXT = (
    "在朝廷眼里，他们多半是癣疥之疾，不是心腹之患：元老院每隔几年议一次要不要彻底清剿，"
    "每次议而不决——不是剿不了，是剿完留下的灰色地带更麻烦。城镇里的高利贷、销赃、"
    "调解纠纷都捏在这么一批人手里，凑成一套法外的秩序，没了它，下城的治安撑不了三天；"
    "他们也接朝廷不方便出面的活。至于那套信仰，不过是脸上贴的一层金箔，撕了底下还是匪帮——"
    "但这层金箔到底让他们和纯黑帮分开了：他们有经可念、有主可等，用起来有底线。"
    "外来做生意的看得更实在：大陆上再没有第二个这样的组织，一边敲诈商户一边念经，"
    "劫掠和末世论搅在一块儿；阵地战里只能当消耗品，可巷战里的那股劲头不能小看。"
    "那句话概括得最准——那层千年教派的自我定位，是他们在这个灰产行当里唯一的信用担保。"
)

# B 类引文（[文件名, 变体号, 原句]），逐字取自源文件，不得改写
B_RUMOR = [
    ["rule_火焰余烬__火焰余烬.json", 0, "北区卖陶罐的老瘸子就是他们的人"],
    ["rule_火焰余烬__火焰余烬.json", 4, "死了皇帝不埋，等着他回来"],
]
B_SUMMARY = [
    ["rule_火焰余烬__火焰余烬.json", 2, "我们不是匪帮，我们是被遗忘者的工会"],
    ["rule_火焰余烬__火焰余烬.json", 2, "我七岁跟着爹进会，到现在鬓角白了，天亮还是没来"],
    ["rule_火焰余烬__火焰余烬.json", 5, "同业，但不是同行"],
]
B_DETAIL = [
    ["rule_火焰余烬__火焰余烬.json", 1, "火焰余烬是帝国的癣疥之疾，不是心腹之患"],
    ["rule_火焰余烬__火焰余烬.json", 3, "那层千年教派的自我定位，是他们在这个灰产行当里唯一的信用担保"],
]

# A 类引文（沿用现有，不外扩）
A_RUMOR = ["uce9Opgg", "RWOYui01", "7JMaPpkN"]
A_DETAIL = ["nQOf27Rx", "dRaYhQva", "B8c2mNKj"]

d = json.load(io.open(L2, encoding="utf-8"))
target = None
for doc in d["docs"]:
    if doc["slug"] == "small-factions":
        target = doc
        break

if target is None:
    raise SystemExit("找不到 small-factions 档")

old = json.dumps(target["assertions"], ensure_ascii=False)

target["assertions"] = [{
    "id": "small-factions-1",
    "kind": "fact",
    "text": "小阵营是卡拉迪亚上不属于任何王国的一批群体：可能是罪恶的兄弟会、雇佣兵组织、宗教运动组织或游牧民。他们通常靠给某个王国卖命，或向路过其领地的商队和旅行者收取保护费来维持生计。",
    "expressions": [
        {"id": "small-factions-rumor", "layer": "rumor", "text": RUMOR_TEXT,
         "A": A_RUMOR, "B": B_RUMOR},
        {"id": "small-factions-summary", "layer": "summary", "text": SUMMARY_TEXT,
         "A": A_RUMOR, "B": B_SUMMARY},
        {"id": "small-factions-detail", "layer": "detail", "text": DETAIL_TEXT,
         "A": A_DETAIL, "B": B_DETAIL},
    ],
}]

with io.open(L2, "w", encoding="utf-8", newline="\n") as h:
    json.dump(d, h, ensure_ascii=False, indent=2)
    h.write("\n")

print("《小阵营》L2 已更新。")
print("旧 assertions 字节：%d → 新：%d" % (len(old), len(json.dumps(target["assertions"], ensure_ascii=False))))
for e in target["assertions"][0]["expressions"]:
    print("  %s / %s / A=%d B=%d / %d 字"
          % (e["id"], e["layer"], len(e["A"]), len(e["B"]), len(e["text"])))
