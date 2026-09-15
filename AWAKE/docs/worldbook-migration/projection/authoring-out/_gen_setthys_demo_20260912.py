# -*- coding: utf-8 -*-
"""塞堤斯河成品档草稿生成器：编年史+官方双源消化后输出（示范"揉碎重写"）"""
import io, os, hashlib, yaml

def H(s):
    return hashlib.sha256(s.encode("utf-8")).hexdigest().upper()

# ---------- 内核文本（A 级骨架 + 两说如实挂；中世纪口径，无现代分析腔） ----------
GEO = "塞堤斯河发源于密泽亚德高原，向南穿过吕卡里亚谷与上游平原，至下游分作三支注入珀拉斯海，人称三河河谷；两岸谷地是帝国南方的粮仓。"
TRADE = "河水全年可航，把上游平原的麦粮、河谷的陶土与河口的海盐连成一条水路；凭这条河，被围的城池不必忍饥挨饿。"
WAR = "河的东岸滩涂泥泞难行，重装人马难以立足；自东面进逼吕卡隆，须先渡河。"

# ---------- 表达层（完全自写，无编年史修辞残骸） ----------
E = [
    # 断言1 地理
    dict(
        eid="exp-geo-rumor",
        a=0, layer="rumor",
        text={"zh-CN": "这河打北边高原里流出来，一路往南入海。老辈人说，河口分成三股水岔子，一股水养活一片田。"},
        grants=[{"profile_id": "profile.villager", "scope": "local", "min_detail": "rumor"}],
        srcs=["A:settlements:LOC_Morenia", "A:settlements:LOC_Chanopsis", "B:chronicle:V0"],
    ),
    dict(
        eid="exp-geo-summary",
        a=0, layer="summary",
        text={"zh-CN": "塞堤斯河是南方的水脉：源头出自密泽亚德高原，穿吕卡里亚谷南下，末了分作三支归入珀拉斯海——官府文书里管那片水口叫三河河谷。"},
        grants=[{"profile_id": "profile.townsfolk", "scope": "regional", "min_detail": "summary"},
                {"profile_id": "profile.headman", "scope": "national", "min_detail": "summary"}],
        srcs=["A:settlements:LOC_Atphynia", "A:settlements:LOC_Gorcorys"],
    ),
    # 断言2 经济
    dict(
        eid="exp-trade-merchant",
        a=1, layer="detail",
        text={"zh-CN": "这条河一年到头行得了船。上游平原收的麦子装上平底驳船，顺水几日便到海口；河口晒盐、河谷烧陶，都是稳当的营生——做南方的买卖，绕不开这条河。"},
        grants=[{"profile_id": "profile.merchant", "scope": "faction", "min_detail": "detail"},
                {"profile_id": "profile.ransom_broker", "scope": "faction", "min_detail": "detail"}],
        srcs=["A:settlements:LOC_Atphynia_salt", "A:settlements:LOC_Saldannis_clay", "B:chronicle:V2", "B:chronicle:V5"],
    ),
    dict(
        eid="exp-trade-summary",
        a=1, layer="summary",
        text={"zh-CN": "吕卡隆的粮船一年四季不断——南方天暖，河面冬天也不封。"},
        grants=[{"profile_id": "profile.townsfolk", "scope": "regional", "min_detail": "summary"}],
        srcs=["B:chronicle:V3"],
    ),
    # 断言3 战争
    dict(
        eid="exp-war-soldier",
        a=2, layer="detail",
        text={"zh-CN": "从东面打吕卡隆，先得渡塞堤斯。河不深，可两岸滩地吃马蹄，重甲陷进去拔不出——守军只管在岸上等。多少年来，东边的兵多半没望见城墙就折在河滩上了。"},
        grants=[{"profile_id": "profile.soldier", "scope": "national", "min_detail": "detail"}],
        srcs=["B:chronicle:V0", "B:chronicle:V7_military"],
    ),
    dict(
        eid="exp-war-noble",
        a=2, layer="detail",
        text={"zh-CN": "北地来的将领先是轻慢这条河——他们惯于等河封冻踏冰而过。可南方的河从不封冻，围城围到军粮见底，河上的粮船照样进出。在南方打仗，先得算水。"},
        grants=[{"profile_id": "profile.noble", "scope": "elite", "min_detail": "detail"}],
        srcs=["B:chronicle:V3"],
    ),
]

QUOTES = {
    "A:settlements:LOC_Morenia": "摩雷尼亚俯瞰着塞堤斯河，位于通往吕卡里亚谷的低矮入口处。",
    "A:settlements:LOC_Chanopsis": "卡诺普西斯依着密泽亚德高原上的塞堤斯河源头而建。",
    "A:settlements:LOC_Atphynia": "阿特费尼亚村靠近塞堤斯河口，再向下游河流随即分为三支，形成三河河谷。",
    "A:settlements:LOC_Atphynia_salt": "在塞堤斯河流入珀拉斯海的河口附近，村民们开出了盐田。",
    "A:settlements:LOC_Gorcorys": "戈耳科律斯坐落于歌里亚河畔，那是塞堤斯河流经三河河谷最南端的支流。",
    "A:settlements:LOC_Saldannis_clay": "山上的细土被水流冲刷下来形成黏土，这是能让帝国南方的陶工们两眼放光的东西。",
    "B:chronicle:V0": "塞堤斯河是吕卡隆的东墙……重甲步兵踩进去能陷到膝盖。",
    "B:chronicle:V2": "收粮的时候，平底驳船从吕卡里亚平原装满了麦子和马料，顺着河往南漂。",
    "B:chronicle:V3": "我们在北方的冰天雪地里练出来的围城本事，到了南方全用不上。",
    "B:chronicle:V5": "我们阿塞莱的商船从撒纳拉出发，穿过珀拉斯海，进了塞堤斯河口就能一路漂到吕卡隆城下。",
    "B:chronicle:V7_military": "塞堤斯河及其两岸的湿地构成了城市东侧的自然屏障。",
}

def src_entry(ref):
    kind, sid = ref.split(":", 1)
    q = QUOTES[ref]
    if kind == "A":
        return {"source_id": "source.game.bannerlord-settlements", "quote_hash": H(q), "note": sid}
    return {"source_id": "source.chronicle.animusforge", "quote_hash": H(q), "note": "rule_塞堤斯河"}

assertions = []
for aidx, atext in enumerate([GEO, TRADE, WAR]):
    exprs = []
    for e in E:
        if e["a"] != aidx:
            continue
        exprs.append({
            "id": e["eid"], "layer": e["layer"], "text": e["text"],
            "grants": e["grants"], "sources": [src_entry(s) for s in e["srcs"]],
        })
    assertions.append({"id": "a%d" % (aidx + 1), "text": {"zh-CN": atext}, "expressions": exprs})

doc = {
    "id": "doc.geography.sethys-river",
    "title": {"zh-CN": "塞堤斯河", "en": "Sethys"},
    "domain": "geography", "subdomain": "rivers",
    "kind": "fact", "revision": 1,
    "aliases": [{"zh-CN": ["塞堤斯河", "塞堤斯谷", "三河河谷"], "en": ["Sethys", "Tripotamia"]}],
    # 河流无游戏 settlement 代码 → 概念条目不写 entity_ids（09-12 定案）
    "assertions": assertions,
    "sources": [
        {"source_id": "source.game.bannerlord-settlements",
         "source_content_hash": "（登记表既有值）", "use": "A级：走向/源头/三河河谷/盐田/陶土"},
        {"source_id": "source.chronicle.animusforge",
         "source_content_hash": "（登记表既有值）", "use": "B级：不冻/粮道/东滩屏障/北人观感"},
    ],
}

out = os.path.join(os.path.dirname(__file__), "draft-sethys-river.yaml")
with io.open(out, "w", encoding="utf-8", newline="\n") as f:
    f.write(yaml.dump(doc, allow_unicode=True, sort_keys=False, width=4096, default_flow_style=False))
print("written:", out)
print("assertions:", len(assertions), "expressions:", len(E))
