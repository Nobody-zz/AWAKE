# -*- coding: utf-8 -*-
"""修4a：扩来源快照 + 三处 hash 同步（2026-09-13）。
编译器机制：quote 必须能在 authoring/sources/<locator_root> 快照中定位；
source_content_hash 三处一致：快照文件 sha256 = 登记 yaml = 档内 sources 条目。
"""
import os, re, json, hashlib, glob
import yaml

WS = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring"
OUT = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out"
SRC = os.path.join(WS, "sources")

def sha(b):
    return hashlib.sha256(b).hexdigest()

# 1) geography2 快照追加 8 行
g2 = os.path.join(SRC, "game-settlements-geography2.txt")
add_g2 = [
    "Settlements.Settlement.text.castle_village_EW1_1 => 位于沙拉斯湾和珀拉斯内海间的海角以加隆托为名。沿海草甸茂盛，这些较高的草对海水盐质的耐受性不错，可用来饲养优良的马匹。",
    "Settlements.Settlement.text.village_K6_2 => 努丘克位于一片露头岩山脉之下，该山的达西语名为柯希·罗希尼，即黎明山脉。当地村民多是新近定居的游牧民，他们像祖先一样饲养绵羊。",
    "Settlements.Settlement.text.village_K6_1 => 喀拉罕位于柯希·罗希尼——黎明山脉山麓的丘陵地带。当地村民向迁徙的部落购买马匹，自己也饲养一些，库赛特军队的许多马匹都来源于此。",
    "Settlements.Settlement.text.castle_village_V6_2 => 德里亚特位于瓦尔切格湾与埃博半岛的山脊之间。当地村民会在山上捕获河狸与水貂，有时会在海湾水域捉海豹。",
    "Settlements.Settlement.text.castle_village_S1_1 => 乌斯托科坐落于瓦尔切格湾与比亚里海之间当风的卡恰尔半岛。当地人饲养的耐寒北方牛会在石缝间找小草吃，困难时期则补充一些海藻。",
    "T2E4xKVn => 雷维尔是怪石嶙峋的卡恰尔半岛上的一座重要城镇，同时也是巴尔加德的传统对手。根据传说，巴尔加德公主阿基娜的丈夫被雷维尔人杀死，但她接受了敌国国王的提议，与其结为连理以解决这一争端。然而，在新婚之夜，她堵住了厅堂的大门，将国王和他的一百名族人与勇士活活烧死在里面。这场争端后来通过一系列的条约和联姻解决了，也迎来了其乐融融的时代。",
    "0cZmKO76 => 这座如今被称为沙拉斯的城市，其历史最早可以追溯到第一批卡拉德殖民者登上这片大陆的海岸之时。在帝国如日中天的时候，其首都迁到了北方的巴拉维诺斯，而沙拉斯则成为了一座海务和贸易的重要港口，帝国贵族们在周边海湾附近的和煦海滩上纷纷建起自己的纳凉别墅，时不时地划着小艇在小岛的周边游弋。瓦兰迪亚人到来后，沙拉斯落入了无情的戴·科尔坦家族手中，其无穷的财富助长了后者的野心。",
    "Settlements.Settlement.text.castle_village_EN2_1 => 罗卡那位于拉科尼斯湖的东岸。拉科尼斯湖缓和了北方冬季的寒冷，为种植桑树、产蚕丝提供了条件。",
]
txt = open(g2, encoding="utf-8").read()
tail = add_g2 if txt.endswith("\n") else [""] + add_g2
new_g2 = txt + "\n".join(tail) + "\n"
open(g2, "w", encoding="utf-8", newline="\n").write(new_g2)
g2_hash = sha(new_g2.encode("utf-8"))
print("geography2 快照 +", len(add_g2), "行, 新 hash", g2_hash[:16])

# 2) war1 快照追加 2 行
w1 = os.path.join(SRC, "game-lore-war1.txt")
add_w1 = ["DHbF9JvO => 皇家侍卫", "k1Xr4rKn => 亲卫骑兵"]
txt = open(w1, encoding="utf-8").read()
new_w1 = txt + ("\n" if txt.endswith("\n") else "") + "\n".join(add_w1) + "\n"
open(w1, "w", encoding="utf-8", newline="\n").write(new_w1)
w1_hash = sha(new_w1.encode("utf-8"))
print("war1 快照 +", len(add_w1), "行, 新 hash", w1_hash[:16])

# 3) 更新两个登记 yaml 的 hash
for reg, nh in [("source-game-settlements-geography2.yaml", g2_hash),
                ("source-game-lore-war1.yaml", w1_hash)]:
    p = os.path.join(SRC, reg)
    t = open(p, encoding="utf-8").read()
    t = re.sub(r"(?m)^source_content_hash: [0-9a-f]+$", "source_content_hash: " + nh, t)
    yaml.safe_load(t)
    open(p, "w", encoding="utf-8", newline="\n").write(t)
    print("[OK] 登记", reg, "->", nh[:16])

# 4) 更新 authoring-out 12 档的 source_content_hash
G2_OLD, G2_NEW = "a6d2b6c3fbfeb687e6395c1114440b93ee2519674bdb1cc5a336d4f6df4947e2", g2_hash
W1_OLD, W1_NEW = "41792a7110317feb4b4f8dc8c5ae861fe070bb13adb64771e9455166d81001bb", w1_hash
targets = {
    "charas-bay": G2_NEW, "charas-origin-tales": G2_NEW, "dawn-mtn": G2_NEW, "dawn-stew": G2_NEW,
    "der-furs": G2_NEW, "der-vill": G2_NEW, "kach-land": G2_NEW, "kach-own": G2_NEW,
    "kach-tales": G2_NEW, "lac-lake": G2_NEW, "royal-guard": W1_NEW, "sturgia-military": W1_NEW,
}
for slug, nh in targets.items():
    fp = os.path.join(OUT, slug + ".yaml")
    t = open(fp, encoding="utf-8").read()
    old = G2_OLD if nh == G2_NEW else W1_OLD
    # 只替换新挂条目里的旧 hash（整档 replace 安全：旧 hash 只出现在我们新加的行）
    t2 = t.replace(old, nh)
    assert t2 != t or nh in t, slug + ": hash 替换失败"
    yaml.safe_load(t2)
    open(fp, "w", encoding="utf-8", newline="\n").write(t2)
print("[OK] 12 档 source_content_hash 同步")

# 5) 同步到 workspace authoring
import shutil
for slug in targets:
    shutil.copy(os.path.join(OUT, slug + ".yaml"), os.path.join(WS, slug + ".yaml"))
for src, reg in [(g2, "game-settlements-geography2.txt"), (w1, "game-lore-war1.txt"),
                 (os.path.join(SRC, "source-game-settlements-geography2.yaml"), "source-game-settlements-geography2.yaml"),
                 (os.path.join(SRC, "source-game-lore-war1.yaml"), "source-game-lore-war1.yaml")]:
    pass  # 快照与登记 yaml 本就写在 WS 侧
print("[OK] 同步 12 档到 workspace authoring")
