# -*- coding: utf-8 -*-
"""军事批 · 样板档：具装骑兵（war/troops）。

工序照 war1 定式：A=游戏本体（bannerlord.db 按 token 拉 CN 全文，程序校验）；
B=编年史变体（Variants[n].Content，逐字 assert）；各自落一份源快照 .txt 并取 sha256。
写盘双写：现役 workspace/full-geo1/authoring/ + 镜像 projection/authoring-out/。
"""
import hashlib
import io
import json
import os
import sqlite3

import yaml

WS = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring"
AO = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out"
DB = r"file:C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db?mode=ro"
RULES = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/AnimusForge/PlayerExports/卡拉迪亚编年史/knowledge/rules"

GAME_TXT_F = "game-lore-war2.txt"
CHRON_TXT_F = "chronicle-animusforge-war2.txt"
GAME_SRC_ID = "source.calradia.game.lore.war2"
CHRON_SRC_ID = "source.calradia.chronicle.animusforge.war2"
GAME_VER = "bannerlord-1.3.15.110062"
CHRON_VER = "animusforge-20260913"

db = sqlite3.connect(DB, uri=True)
cur = db.cursor()


def loc_cn(strid):
    r = cur.execute("SELECT text FROM localization_entries WHERE stringId=? AND language='CNs'", (strid,)).fetchone()
    return r[0] if r else None


def rule_variant(rulefile, idx):
    d = json.load(io.open(os.path.join(RULES, rulefile), encoding="utf-8-sig"))
    return (d.get("Variants") or [])[idx].get("Content", "")


# ---------------- A 级引文（游戏本体） ----------------
A_FULL = {
    "cat_elite":  "0dnD9cR4",   # 具装骑兵＝帝国精英、全身钢铁、领皇饷、操练剑斧骑枪复合弓
    "cat_core":   "Mpu7rab1",   # 帝国军队核心＝职业步兵＋具装骑兵
    "cat_hist":   "X0kKBzsW",   # 卡拉德军制转变：公民步兵 → 富人扈从具装骑兵队
    "cat_vet":    "ammEPFoq",   # 退役具装骑兵：负担不起战马盔甲，给商人当雇佣兵
    "cat_foe":    "BleybhsD",   # 敌方亲历者：无视我们的射击，撕开我军部队
    "cat_raider": "aT2qskIk",   # 轻骑兵的速度对具装骑兵而言太快了
    "cat_dense":  "7H5RG3fS",   # 近战骑兵硬撞密集阵就可能被截停、打倒
}
A = {k: loc_cn(sid) for k, sid in A_FULL.items()}
for k, v in A.items():
    assert v, "A text missing in DB: %s" % k

# ---------------- B 级引文（编年史变体） ----------------
# ⚠️ rule_具装骑兵 的 V1（「矛又长又重…无法架枪，也无法举盾，遇到远程部队就只能被射成刺猬」）
#    **与游戏实际机制相反**（09-20 甲方指出；装备数据核实：imperial_cataphract 装备
#    Item0=empire_lance_2_t4、Item1=heavy_horsemans_kite_shield，有盾；方旗骑士同样有盾），
#    故**不采用**——B 级料讲"游戏机制"时必须先与游戏数据对表。
B_QUOTES = {
    "V0": ("rule_具装骑兵__具装骑兵.json", 0, "具装骑兵是帝国的大杀器，他们人马具装呀，战无不胜"),
    "V2": ("rule_具装骑兵__具装骑兵.json", 2, "我们帝国军队里的具装骑兵就是战无不胜的，他们人马具装，无视一切攻击，胯下一匹帕尔马廷马赋予了他们极大的机动能力，没有什么可以阻止具装骑兵们的冲锋，除了他们自己"),
}
for k, (rf, vi, q) in B_QUOTES.items():
    c = rule_variant(rf, vi)
    assert q in c, "B quote not in rule %s[%d]:\n  want: %s\n  have: %s" % (rf, vi, q, c[:160])


def H(x):
    return hashlib.sha256(x.encode("utf-8")).hexdigest().upper()


def hlow(x):
    return hashlib.sha256(x.encode("utf-8")).hexdigest()


# ---------------- 源快照 ----------------
game_txt = "\n".join("%s => %s" % (A_FULL[k], A[k]) for k in sorted(A_FULL)) + "\n"
chron_txt = "\n\n".join("%s [V%d] %s" % (B_QUOTES[k][0], B_QUOTES[k][1], B_QUOTES[k][2])
                        for k in sorted(B_QUOTES)) + "\n"
SH = {"game": hlow(game_txt), "chron": hlow(chron_txt)}

io.open(os.path.join(WS, "sources", GAME_TXT_F), "w", encoding="utf-8", newline="\n").write(game_txt)
io.open(os.path.join(WS, "sources", CHRON_TXT_F), "w", encoding="utf-8", newline="\n").write(chron_txt)


class ND(yaml.SafeDumper):
    def ignore_aliases(self, data):
        return True


def ydump(obj):
    return yaml.dump(obj, Dumper=ND, allow_unicode=True, sort_keys=False, width=4096,
                     default_flow_style=False)


GAME_SRC = dict(source_id=GAME_SRC_ID, source_version=GAME_VER, source_nature="game_snapshot",
                universe="awake_current", era="current", locator_root=GAME_TXT_F,
                source_content_hash=SH["game"], content_tier="base", license_status="permitted",
                use_status="active", valid_until=None, imported_at="2026-09-19T00:00:00Z",
                normalization_version="utf8-lf-no-bom-v1")
CHRON_SRC = dict(source_id=CHRON_SRC_ID, source_version=CHRON_VER, source_nature="chronicle",
                 universe="awake_current", era="historical", locator_root=CHRON_TXT_F,
                 source_content_hash=SH["chron"], content_tier="base", license_status="permitted",
                 use_status="active", valid_until=None, imported_at="2026-09-19T00:00:00Z",
                 normalization_version="utf8-lf-no-bom-v1")
io.open(os.path.join(WS, "sources", "source-game-lore-war2.yaml"), "w",
        encoding="utf-8", newline="\n").write(ydump(GAME_SRC))
io.open(os.path.join(WS, "sources", "source-chronicle-animusforge-war2.yaml"), "w",
        encoding="utf-8", newline="\n").write(ydump(CHRON_SRC))


def aref(k):
    q = A[k]
    return {"source_id": GAME_SRC_ID, "source_version": GAME_VER, "source_content_hash": SH["game"],
            "locator": "bannerlord.db#localization." + A_FULL[k], "quote_hash": H(q), "quote": q}


def bref(k):
    rf, vi, q = B_QUOTES[k]
    return {"source_id": CHRON_SRC_ID, "source_version": CHRON_VER, "source_content_hash": SH["chron"],
            "locator": "rules/%s#/Variants/%d/Content" % (rf, vi), "quote_hash": H(q), "quote": q}


def G(pid, scope, detail, **cond):
    g = {"profile_id": "profile." + pid, "scope": scope, "min_detail": detail}
    for k, v in cond.items():
        if k.endswith("_ids"):
            g[k] = ["entity.%s.%s" % (k[:-4], x) for x in v]
        else:
            g[k] = v
    return g


def E(eid, layer, text, grants, srcs):
    return {"id": "expr." + eid, "revision": 1, "layer": layer, "text": {"zh-CN": text},
            "sources": srcs, "grants": grants, "denies": []}


def AS(aid, text, kind, exprs):
    allsrcs, seen = [], set()
    for e in exprs:
        for s in e["sources"]:
            k = (s["source_id"], s["quote_hash"])
            if k not in seen:
                seen.add(k)
                allsrcs.append(s)
    return {"id": "assertion." + aid, "revision": 1, "kind": kind, "text": {"zh-CN": text},
            "sources": allsrcs, "expressions": exprs}


SL = "troop-cataphract"

a1 = AS(SL + "-1",
        "具装骑兵是帝国最精锐的职业重装骑兵：人马俱披甲，全身上下包覆帝国锻造厂所能锻造出的最精良钢铁；作为领取皇家军饷的全职士兵，他们别无他业，终日操练剑、斧、骑枪与复合弓，与职业步兵并列为帝国军队的两大核心。其地位有一部转变史——卡拉德军队的中坚早先由公民步兵充当，后变为富人扈从组成的具装骑兵队，与帝国由民粹共和转向大地主寡头同步。不过这份荣耀并不总是养得住人：退役的具装骑兵未必负担得起自己的战马与盔甲，有人只得去给商人当雇佣兵。",
        "fact",
        [
            E(SL + "-overview-summary", "summary",
              "具装骑兵是帝国最拿得出手的兵：人和马都裹着铁，从头包到脚。他们是领皇家军饷的职业兵，不干别的，"
              "天天操练剑、斧、骑枪和复合弓——帝国军队的两根柱子，一根是职业步兵，另一根就是他们。",
              [G("townsfolk", "regional", "summary"), G("merchant", "faction", "summary")],
              [aref("cat_elite"), aref("cat_core")]),
            E(SL + "-elite-detail", "detail",
              "帝国的家底是从公民步兵换成具装骑兵的：早先卡拉德人靠自耕农拿枪站阵，后来站在阵前的成了大户人家的扈从，"
              "人马具甲。这一步换下来，帝国也就从一个推举执政官的共和，变成了大地主说了算的朝廷。名头归名头——"
              "退役下来的具装骑兵，付不起自己那副甲和那匹马的大有人在，最后只能去给商人牵马押货。",
              [G("noble", "elite", "detail")],
              [aref("cat_hist"), aref("cat_vet")]),
        ])

a2 = AS(SL + "-2",
        "「战无不胜」是具装骑兵在卡拉迪亚最通行的名声：民间直呼其为帝国的大杀器，敌方亲历者也作此印象——"
        "「人马具甲，铁铠从头包到脚。他们完全无视我们的射击，直接撕开了我军部队」。",
        "rumor",
        [
            E(SL + "-levy-rumor", "rumor",
              "帝国那些铁人又来了——人和马都包着铁，箭射上去叮当响，跟没事一样。老话讲他们是帝国的大杀器，战无不胜。"
              "真撞上他们的队，能躲就躲。",
              [G("villager", "local", "rumor")],
              [bref("V0"), aref("cat_foe")]),
        ])

a3 = AS(SL + "-3",
        "行家点出具装骑兵的战场短板：机动性不及轻骑——「轻骑兵的速度对于具装骑兵而言，实在是太快了」，追击乏力；"
        "硬撞结好阵的密集步兵则可能被截停、打倒；此外这兵种养护极贵，帝国一旦手头拮据，"
        "只能征召规模可观的志愿辅助兵来凑数。",
        "interpretation",
        [
            E(SL + "-drill-detail", "detail",
              "见识过的人才知道具装骑兵不是没法对付。他们追不上轻骑——轻骑跑得快，他们只能在后头吃土；"
              "硬撞结好阵、站得密的步兵，照样能被截停、打倒。再就是贵：帝国手头一紧，就只能拿征召来的辅助兵凑数。"
              "这兵种，养得起才算本事。",
              [G("soldier", "national", "detail")],
              [aref("cat_raider"), aref("cat_dense"), aref("cat_core")]),
        ])

a4 = AS(SL + "-4",
        "帝国军人对具装骑兵的另一套说法则全然是自家荣耀：宣称他们人马具装、无视一切攻击，胯下帕尔马廷马赋予极大机动，"
        "「没有什么可以阻止具装骑兵们的冲锋，除了他们自己」。",
        "interpretation",
        [
            E(SL + "-honor-detail", "detail",
              "帝国官面上一提精兵就提具装骑兵。他们自己说：人马具装，什么攻击都挨得住，胯下那匹帕尔马廷马跑起来还灵活得不像话——"
              "「没有什么可以阻止具装骑兵们的冲锋，除了他们自己」。这话你信几成，看你站哪儿听。",
              [G("headman", "national", "detail")],
              [bref("V2")]),
        ])

asserts = [a1, a2, a3, a4]

# 顶层 sources 汇总
allsrc, seen = [], set()
for a in asserts:
    for s in a["sources"]:
        k = (s["source_id"], s["quote_hash"])
        if k not in seen:
            seen.add(k)
            allsrc.append(s)

RB = {"profile_registry_version": "1.0.0",
      "profile_registry_hash": "309D10583E7B4AF05F28DE2C2E67A2528CB3D6C45752369C0F7D160FABC3F3A5",
      "referral_registry_version": "1.0.0",
      "referral_registry_hash": "6E17075F1F967BE28C0437121DB50E5C30809C4EFC3D2A5F22AD9BB0B3B926CD"}

doc_obj = {
    "schema_version": "awake.worldbook.authoring.v1",
    "revision": 1,
    "id": "doc.war.troops-cataphract",
    "title": {"zh-CN": "具装骑兵", "en": "Cataphract"},
    "status": "needs_review",
    "domain": "war",
    "subdomain": "troops",
    "universe": "awake_current",
    "era": {"key": "current", "certainty": "bounded"},
    "content_tier": "base",
    "aliases": {"zh-CN": ["具装骑兵", "帝国具装骑兵", "铁甲骑兵", "皇家具装骑兵"],
                "en": ["Cataphract", "Imperial Cataphract", "Elite Cataphract"]},
    "summary": {"zh-CN": "具装骑兵：帝国最精锐的重装骑兵，人马俱披铁甲，领取皇家军饷的职业兵，与职业步兵并列为帝国军队两大核心；"
                         "「战无不胜」之名遍传卡拉迪亚，敌方亲历者称其无视箭雨撕开阵列；行家则指出其机动不及轻骑、硬撞密集阵会被截停，且养护极贵。"},
    "registry_bindings": RB,
    "sources": allsrc,
    "authority": {"owner": "awake_canon", "conflict_policy": "canon_wins"},
    "assertions": asserts,
}

body = ydump(doc_obj)
assert "&id" not in body and "id0" not in body
chk = yaml.safe_load(body)
assert chk["id"] == "doc.war.troops-cataphract" and len(chk["assertions"]) == 4

for d in (WS, AO):
    io.open(os.path.join(d, "troops-cataphract.yaml"), "w", encoding="utf-8", newline="\n").write(body)

print("game hash :", SH["game"])
print("chron hash:", SH["chron"])
print("断言 %d 条 / 表达 %d 条 / 源 %d 条" % (len(asserts), sum(len(a["expressions"]) for a in asserts), len(allsrc)))
print("受众:", [g["profile_id"] for a in asserts for e in a["expressions"] for g in e["grants"]])
print("已写: troops-cataphract.yaml （现役 + 镜像）")
