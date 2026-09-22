# -*- coding: utf-8 -*-
"""暗面批 · 生成器（2026-09-20）。

读 AO 下的 `_dark_A_20260920.json`，一次出全部档，双写现役＋镜像。

与 经济批生成器 的差别（两处）：
  ① **按档分域**（doc["domain"]/["subdomain"]）—— 暗面横跨 politics/culture/economy 三域。
  ② **支持三层表达**（rumor/summary/detail）—— 「信不信」这一轴要同一档给不同人不同说法。
     ⚠️ 层与层之间受众必须**互斥**：同一身份若被两层都挂上，层号低的那条永远送不到人耳
        （`SelectExpression` 每档只送一条、同分取先）。故 `townsfolk` 只在「无 summary 层」时才挂 rumor 层。

用法：
  python _gen_dark_20260920.py --check   # 只校验引文与结构，不写盘
  python _gen_dark_20260920.py           # 写盘
"""
import hashlib
import io
import json
import os
import sqlite3
import sys

import yaml

WS = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring"
AO = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out"
DB = r"file:C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db?mode=ro"
RULES = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/AnimusForge/PlayerExports/卡拉迪亚编年史/knowledge/rules"

GAME_TXT_F = "game-urban-dark.txt"
CHRON_TXT_F = "chronicle-animusforge-dark.txt"
GAME_SRC_ID = "source.calradia.game.urban-dark"
CHRON_SRC_ID = "source.calradia.chronicle.animusforge.dark"
GAME_VER = "bannerlord-1.3.15.110062"
CHRON_VER = "animusforge-20260913"
IMPORTED = "2026-09-20T00:00:00Z"

CHECK_ONLY = "--check" in sys.argv

db = sqlite3.connect(DB, uri=True)
cur = db.cursor()


def loc_cn(strid):
    r = cur.execute("SELECT text FROM localization_entries WHERE stringId=? AND language='CNs'",
                    (strid,)).fetchone()
    return r[0] if r else None


_rule_cache = {}


def rule_variant(fn, idx):
    if fn not in _rule_cache:
        _rule_cache[fn] = json.load(io.open(os.path.join(RULES, fn), encoding="utf-8-sig"))
    return (_rule_cache[fn].get("Variants") or [])[idx].get("Content", "")


# ---------- 1. 读 L2 ----------
docs = []
for f in ["_dark_A_20260920.json"]:
    d = json.load(io.open(os.path.join(AO, f), encoding="utf-8"))
    docs.extend(d["docs"])
print("L2 数据已载入，档数:", len(docs))

# ---------- 2. 引文校验 ----------
A_TEXTS, B_QUOTES, errs = {}, {}, []
for doc in docs:
    for a in doc["assertions"]:
        for e in a["expressions"]:
            for sid in e.get("A") or []:
                if sid not in A_TEXTS:
                    t = loc_cn(sid)
                    if not t:
                        errs.append("%s / %s: A 引文 %s 在 DB 里不存在" % (doc["slug"], e["id"], sid))
                    A_TEXTS[sid] = t
            for (fn, idx, q) in e.get("B") or []:
                k = (fn, idx, q)
                if k in B_QUOTES:
                    continue
                try:
                    c = rule_variant(fn, idx)
                except Exception as ex:
                    errs.append("%s / %s: 读不到 %s[%d] (%s)" % (doc["slug"], e["id"], fn, idx, ex))
                    continue
                if q not in c:
                    errs.append("%s / %s: B 引文不在 %s[%d]\n     want: %s" % (doc["slug"], e["id"], fn, idx, q))
                else:
                    B_QUOTES[k] = q
if errs:
    print("\n[引文校验失败]")
    for e in errs:
        print("  -", e)
    sys.exit(1)
print("引文校验通过：A %d 条 / B %d 条" % (len(A_TEXTS), len(B_QUOTES)))


def H(x):
    return hashlib.sha256(x.encode("utf-8")).hexdigest().upper()


def hlow(x):
    return hashlib.sha256(x.encode("utf-8")).hexdigest()


game_txt = "\n".join("%s => %s" % (sid, A_TEXTS[sid]) for sid in sorted(A_TEXTS)) + "\n"
chron_txt = "\n\n".join("%s [V%d] %s" % (k[0], k[1], k[2]) for k in sorted(B_QUOTES)) + "\n"
SH = {"game": hlow(game_txt), "chron": hlow(chron_txt)}


def aref(sid):
    q = A_TEXTS[sid]
    return {"source_id": GAME_SRC_ID, "source_version": GAME_VER, "source_content_hash": SH["game"],
            "locator": "bannerlord.db#localization." + sid, "quote_hash": H(q), "quote": q}


def bref(fn, idx, q):
    return {"source_id": CHRON_SRC_ID, "source_version": CHRON_VER, "source_content_hash": SH["chron"],
            "locator": "rules/%s#/Variants/%d/Content" % (fn, idx), "quote_hash": H(q), "quote": q}


# ---------- 3. grant 表（严格不超身份能力上限） ----------
# 一手能力表（src/WorldbookIdentityCapabilityRules.cs）：
#   villager/commoner=(local,rumor)｜townsfolk=(regional,summary)｜headman/soldier=(national,detail)
#   notable=(regional,detail)｜merchant/tavernkeeper/ransom_broker=(faction,detail)｜noble=(elite,detail)
# 序关系（WorldbookIdentityEvaluator.cs）：scope local<regional<national<faction<elite<private；
#                                          detail rumor<summary<detail<secret
# ⚠️ 同档内受众必须互斥 —— 同一身份挂两层，层号低的那条永远不达。
LOW = [("commoner", "local", "rumor"), ("villager", "local", "rumor")]
MID = [("townsfolk", "regional", "summary")]
HIGH = [("notable", "regional", "detail"), ("merchant", "faction", "detail"),
        ("tavernkeeper", "faction", "detail"), ("ransom_broker", "faction", "detail"),
        ("headman", "national", "detail"), ("soldier", "national", "detail"),
        ("noble", "elite", "detail")]


def grants_for(layers):
    """按本档实际用到的层，给出互斥的受众分配。"""
    g = {}
    if "rumor" in layers:
        # 有 summary 层时，townsfolk 让给 summary 层，免得自己压自己
        g["rumor"] = LOW if "summary" in layers else LOW + MID
    if "summary" in layers:
        g["summary"] = MID
    if "detail" in layers:
        g["detail"] = HIGH
    return g


def G(t):
    return {"profile_id": "profile." + t[0], "scope": t[1], "min_detail": t[2]}


class ND(yaml.SafeDumper):
    def ignore_aliases(self, data):
        return True


def ydump(obj):
    return yaml.dump(obj, Dumper=ND, allow_unicode=True, sort_keys=False, width=4096,
                     default_flow_style=False)


RB = {"profile_registry_version": "1.0.0",
      "profile_registry_hash": "309D10583E7B4AF05F28DE2C2E67A2528CB3D6C45752369C0F7D160FABC3F3A5",
      "referral_registry_version": "1.0.0",
      "referral_registry_hash": "6E17075F1F967BE28C0437121DB50E5C30809C4EFC3D2A5F22AD9BB0B3B926CD"}

if not CHECK_ONLY:
    game_src = dict(source_id=GAME_SRC_ID, source_version=GAME_VER, source_nature="game_snapshot",
                    universe="awake_current", era="current", locator_root=GAME_TXT_F,
                    source_content_hash=SH["game"], content_tier="base", license_status="permitted",
                    use_status="active", valid_until=None, imported_at=IMPORTED,
                    normalization_version="utf8-lf-no-bom-v1")
    chron_src = dict(source_id=CHRON_SRC_ID, source_version=CHRON_VER, source_nature="chronicle",
                     universe="awake_current", era="historical", locator_root=CHRON_TXT_F,
                     source_content_hash=SH["chron"], content_tier="base", license_status="permitted",
                     use_status="active", valid_until=None, imported_at=IMPORTED,
                     normalization_version="utf8-lf-no-bom-v1")
    io.open(os.path.join(WS, "sources", GAME_TXT_F), "w", encoding="utf-8", newline="\n").write(game_txt)
    io.open(os.path.join(WS, "sources", CHRON_TXT_F), "w", encoding="utf-8", newline="\n").write(chron_txt)
    io.open(os.path.join(WS, "sources", "source-game-urban-dark.yaml"), "w",
            encoding="utf-8", newline="\n").write(ydump(game_src))
    io.open(os.path.join(WS, "sources", "source-chronicle-animusforge-dark.yaml"), "w",
            encoding="utf-8", newline="\n").write(ydump(chron_src))

# ---------- 4. 出档 ----------
stat = []
for doc in docs:
    dom = doc["domain"]
    layers = {e["layer"] for a in doc["assertions"] for e in a["expressions"]}
    GG = grants_for(layers)
    asserts = []
    for a in doc["assertions"]:
        exprs, allsrc, seen = [], [], set()
        for e in a["expressions"]:
            srcs = [aref(s) for s in (e.get("A") or [])] + \
                   [bref(fn, i, q) for (fn, i, q) in (e.get("B") or [])]
            for s in srcs:
                k = (s["source_id"], s["quote_hash"])
                if k not in seen:
                    seen.add(k)
                    allsrc.append(s)
            grants = GG[e["layer"]]
            exprs.append({"id": "expr." + e["id"], "revision": 1, "layer": e["layer"],
                          "text": {"zh-CN": e["text"]}, "sources": srcs,
                          "grants": [G(g) for g in grants], "denies": []})
        asserts.append({"id": "assertion." + a["id"], "revision": 1, "kind": a["kind"],
                        "text": {"zh-CN": a["text"]}, "sources": allsrc, "expressions": exprs})

    topsrc, seen = [], set()
    for a in asserts:
        for s in a["sources"]:
            k = (s["source_id"], s["quote_hash"])
            if k not in seen:
                seen.add(k)
                topsrc.append(s)

    doc_obj = {
        "schema_version": "awake.worldbook.authoring.v1",
        "revision": 1,
        "id": "doc.%s.%s" % (dom, doc["slug"]),
        "title": {"zh-CN": doc["title_zh"], "en": doc["title_en"]},
        "status": "needs_review",
        "domain": dom,
        "subdomain": doc["subdomain"],
        "universe": "awake_current",
        "era": {"key": "current", "certainty": "bounded"},
        "content_tier": "base",
        "aliases": {"zh-CN": doc["aliases_zh"], "en": doc["aliases_en"]},
        "summary": {"zh-CN": doc["summary"]},
        "registry_bindings": RB,
        "sources": topsrc,
        "authority": {"owner": "awake_canon", "conflict_policy": "canon_wins"},
        "assertions": asserts,
    }
    body = ydump(doc_obj)
    chk = yaml.safe_load(body)
    assert chk["id"] == "doc.%s.%s" % (dom, doc["slug"])
    assert len(chk["assertions"]) == len(doc["assertions"])
    for lang, tv in (("zh-CN", doc["title_zh"]), ("en", doc["title_en"])):
        for al in chk["aliases"][lang]:
            assert al != tv and (al.lower() not in tv.lower()) and (tv.lower() not in al.lower()), \
                "%s: 别名「%s」与 title「%s」构成死条" % (doc["slug"], al, tv)
    fn = doc["slug"] + ".yaml"
    if not CHECK_ONLY:
        for d in (WS, AO):
            io.open(os.path.join(d, fn), "w", encoding="utf-8", newline="\n").write(body)
    stat.append((doc["slug"], dom + "/" + doc["subdomain"], len(asserts),
                 sum(len(a["expressions"]) for a in asserts), len(topsrc)))

print()
print("%-30s %-22s %4s %4s %4s" % ("档", "域/子域", "断言", "表达", "源"))
for s, dm, na, ne, ns in stat:
    print("%-30s %-22s %4d %4d %4d" % (s, dm, na, ne, ns))
print("\n合计: %d 档 / %d 断言 / %d 表达" % (len(stat), sum(x[2] for x in stat), sum(x[3] for x in stat)))
print("game  hash:", SH["game"])
print("chron hash:", SH["chron"])
print("模式:", "CHECK（未写盘）" if CHECK_ONLY else "WRITE（已双写）")
