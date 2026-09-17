# -*- coding: utf-8 -*-
"""地点批第二批（都城批）：萨涅俄帕 + 帕拉汶德 两档成品（拆解重写工序）。
依据：WORLDBOOK-IMPERIAL-CAPITALS-RESEARCH-20260912.md（Max 三裁 09-12 深夜）。
A=官方文本/游戏数据；B=编年史；D 裁定（萨涅俄帕定都说）登记于研究稿与台账，不进 sources。
"""
import io, os, json, hashlib, sqlite3, yaml, shutil

WS = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring"
AO = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out"
DB = r"file:C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db?mode=ro"
RULES = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/AnimusForge/PlayerExports/卡拉迪亚编年史/knowledge/rules"

db = sqlite3.connect(DB, uri=True)
cur = db.cursor()

def loc_cn(strid):
    r = cur.execute("SELECT text FROM localization_entries WHERE stringId=? AND language='CNs'", (strid,)).fetchone()
    return r[0] if r else None

def rule_variant(rulefile, idx):
    d = json.load(open(os.path.join(RULES, rulefile), encoding="utf-8-sig"))
    return (d.get("Variants") or [])[idx].get("Content", "")

# ---------------- A 级引文（程序化从 DB 拉，手写必失真） ----------------
A_FULL = {
    "saneopa_desc": ("EJZLzhJk", None),
    "paravenos_desc": ("Nx0qK2g6", None),
    "charas_desc": ("0cZmKO76", None),
    "penton_backstory": ("04xAg0yN", None),
}
for k, (sid, q) in A_FULL.items():
    if q is None:
        A_FULL[k] = (sid, loc_cn(sid))
# 游戏数据引文（settlements.xml 所有权）
A_FULL["saneopa_owner"] = (None, "Faction.clan_empire_north_3")

# ---------------- B 级引文（编年史变体切片） ----------------
B_QUOTES = {
    "saneopa_V0": ("rule_萨涅俄帕__萨涅俄帕.json", 0, "萨涅俄帕是帝国首都，听说现在迁都到吕卡隆了"),
    "saneopa_V1": ("rule_萨涅俄帕__萨涅俄帕.json", 1, "萨涅俄帕是我们光荣的帝都，虽然在涅雷采斯之后就迁到了吕卡隆，但这只是暂时的，等到帝国一统，我们就把迁回萨涅俄帕。应该吧？"),
    "saneopa_V2": ("rule_萨涅俄帕__萨涅俄帕.json", 2, "萨涅俄帕是传统帝都，我们从巴拉维诺斯迁都到这里已经三百多年了"),
    "saneopa_V2b": ("rule_萨涅俄帕__萨涅俄帕.json", 2, "如果帝国重归一统，我们依旧希望将帝都迁回萨涅俄帕"),
    "saneopa_V3": ("rule_萨涅俄帕__萨涅俄帕.json", 3, "我们从来不希望将都城迁回萨涅俄帕，因为迁回去，我们在新都培育的势力就全没了"),
    "neretzes_V1a": ("rule_涅雷采斯__涅雷采斯.json", 1, "现在他们家还在北边当大官，听说住在萨涅俄帕，那城可大了，城墙比我们村子的地界还长。"),
    "neretzes_V0a": ("rule_涅雷采斯__涅雷采斯.json", 0, "因为萨涅俄帕在他手里。那座城的城墙厚得连斯特吉亚人的攻城锤都砸不穿，商路从萨涅俄帕辐射出去，半个北帝国的粮食都要经过他们的地盘。"),
    "neretzes_V4a": ("rule_涅雷采斯__涅雷采斯.json", 4, "转而以萨涅俄帕为根基深耕元老院政治。"),
    "paravenos_V0a": ("rule_帕拉汶德__帕拉汶德.json", 0, "平民知晓这座城市曾是帝国首都，但如今它的市场、法庭与竞技场都由瓦兰迪亚贵族掌管。"),
    "paravenos_V0b": ("rule_帕拉汶德__帕拉汶德.json", 0, "铁匠与商贩更在意关税与行会规矩，而非百年前的投降协定。"),
    "paravenos_V1a": ("rule_帕拉汶德__帕拉汶德.json", 1, "戴·提哈家族虽非王座直系，却因持有此城而拥有不逊于公爵的威信。"),
    "paravenos_V1b": ("rule_帕拉汶德__帕拉汶德.json", 1, "谁掌控帕拉汶德，谁就掌控西部商路"),
    "paravenos_V2a": ("rule_帕拉汶德__帕拉汶德.json", 2, "他们记得帕拉汶德的真名是巴拉维诺斯，是卡拉狄乌斯亲手建起的荣耀之城。"),
    "paravenos_V3a": ("rule_帕拉汶德__帕拉汶德.json", 3, "绝大多数贵族们把帕拉汶德的沦陷视为帝国正逐渐走向衰落的又一个重要的历史节点。"),
    "paravenos_V4a": ("rule_帕拉汶德__帕拉汶德.json", 4, "巴旦尼亚山民对帕拉汶德的记忆混合着敌意与疏离。"),
}
for k, (sid, q) in A_FULL.items():
    if sid:
        full = loc_cn(sid)
        assert q in (full or ""), "A quote not in DB text: %s" % k
for k, (rf, vi, q) in B_QUOTES.items():
    c = rule_variant(rf, vi)
    assert q in c, "B quote not in rule: %s\n  want: %s\n  have: %s" % (k, q, c[:120])

# ---------------- 源文件与登记 ----------------
def H(x):
    return hashlib.sha256(x.encode("utf-8")).hexdigest().upper()

def hlower(x):
    return hashlib.sha256(x.encode("utf-8")).hexdigest()

game_txt = "\n".join(
    "%s => %s" % (A_FULL[k][0] or ("settlements." + k.replace("_owner", "").replace("saneopa", "town_EN3") + ".owner"), A_FULL[k][1])
    for k in sorted(A_FULL)) + "\n"
# 手工拼更清晰的 locator 行
game_txt = "\n".join(
    ("%s => %s" % (A_FULL[k][0], A_FULL[k][1])) if A_FULL[k][0]
    else ("settlements.town_EN3.owner => %s" % A_FULL[k][1])
    for k in sorted(A_FULL)) + "\n"

chron_keys = sorted(B_QUOTES)
chron_txt = "\n\n".join("%s [V%d] %s" % (B_QUOTES[k][0], B_QUOTES[k][1], B_QUOTES[k][2]) for k in chron_keys) + "\n"

GAME_TXT_F = "game-settlements-capitals.txt"
CHRON_TXT_F = "chronicle-animusforge-capitals.txt"
io.open(os.path.join(WS, "sources", GAME_TXT_F), "w", encoding="utf-8", newline="\n").write(game_txt)
io.open(os.path.join(WS, "sources", CHRON_TXT_F), "w", encoding="utf-8", newline="\n").write(chron_txt)
game_hash = hlower(game_txt)
chron_hash = hlower(chron_txt)

GAME_SRC = dict(source_id="source.calradia.game.settlements.capitals", source_version="bannerlord-1.3.15.110062",
                source_nature="game_snapshot", universe="awake_current", era="current", locator_root=GAME_TXT_F,
                source_content_hash=game_hash, content_tier="base", license_status="permitted",
                use_status="active", valid_until=None, imported_at="2026-09-12T00:00:00Z",
                normalization_version="utf8-lf-no-bom-v1")
CHRON_SRC = dict(source_id="source.calradia.chronicle.animusforge.capitals", source_version="animusforge-20260912",
                 source_nature="chronicle", universe="awake_current", era="historical", locator_root=CHRON_TXT_F,
                 source_content_hash=chron_hash, content_tier="base", license_status="permitted",
                 use_status="active", valid_until=None, imported_at="2026-09-12T00:00:00Z",
                 normalization_version="utf8-lf-no-bom-v1")

class ND(yaml.SafeDumper):
    def ignore_aliases(self, data):
        return True

def ydump(obj):
    return yaml.dump(obj, Dumper=ND, allow_unicode=True, sort_keys=False, width=4096, default_flow_style=False)

for nm, src in [("source-game-settlements-capitals.yaml", GAME_SRC), ("source-chronicle-animusforge-capitals.yaml", CHRON_SRC)]:
    io.open(os.path.join(WS, "sources", nm), "w", encoding="utf-8", newline="\n").write(ydump(src))

RB = {"profile_registry_version": "1.0.0",
      "profile_registry_hash": "309D10583E7B4AF05F28DE2C2E67A2528CB3D6C45752369C0F7D160FABC3F3A5",
      "referral_registry_version": "1.0.0",
      "referral_registry_hash": "6E17075F1F967BE28C0437121DB50E5C30809C4EFC3D2A5F22AD9BB0B3B926CD"}

def aref(key):
    sid, q = A_FULL[key]
    loc = ("bannerlord.db#localization." + sid) if sid else "bannerlord.db#settlements.town_EN3.owner"
    return {"source_id": GAME_SRC["source_id"], "source_version": GAME_SRC["source_version"],
            "source_content_hash": game_hash, "locator": loc, "quote_hash": H(q), "quote": q}

def bref(key):
    rf, vi, q = B_QUOTES[key]
    return {"source_id": CHRON_SRC["source_id"], "source_version": CHRON_SRC["source_version"],
            "source_content_hash": chron_hash, "locator": "rules/%s#/Variants/%d/Content" % (rf, vi),
            "quote_hash": H(q), "quote": q}

def G(pid, scope, detail, **cond):
    g = {"profile_id": "profile." + pid, "scope": scope, "min_detail": detail}
    for k, v in cond.items():
        if k.endswith("_ids"):
            g[k] = ["entity.%s.%s" % (k[:-4], x) for x in v]
        else:
            g[k] = v
    return g

def E(docslug, eid, layer, text, grants, srcs):
    return {"id": "expr.%s-%s" % (docslug, eid), "revision": 1, "layer": layer,
            "text": {"zh-CN": text}, "sources": srcs, "grants": grants, "denies": []}

def A(docslug, aid, text, kind, exprs):
    allsrcs, seen = [], set()
    for e in exprs:
        for s in e["sources"]:
            k = (s["source_id"], s["quote_hash"])
            if k not in seen:
                seen.add(k)
                allsrcs.append(s)
    return {"id": "assertion.%s-%s" % (docslug, aid), "revision": 1, "kind": kind,
            "text": {"zh-CN": text}, "sources": allsrcs, "expressions": exprs}

def doc(did, zh, en, domain, sub, aliases, summary, assertions):
    allsrcs, seen = [], set()
    for a in assertions:
        for src in a["sources"]:
            k = (src["source_id"], src["quote_hash"])
            if k not in seen:
                seen.add(k)
                allsrcs.append(src)
    return {"schema_version": "awake.worldbook.authoring.v1", "revision": 1, "id": did,
            "title": {"zh-CN": zh, "en": en}, "status": "needs_review",
            "domain": domain, "subdomain": sub, "universe": "awake_current",
            "era": {"key": "current", "certainty": "bounded"}, "content_tier": "base",
            "aliases": {"zh-CN": aliases[0], "en": aliases[1]},
            "summary": {"zh-CN": summary},
            "registry_bindings": RB,
            "authority": {"owner": "awake_canon", "conflict_policy": "canon_wins"},
            "sources": allsrcs,
            "assertions": assertions}

# ================= 档1 萨涅俄帕 =================
SL = "saneopa"
saneopa = doc("doc.politics." + SL, "萨涅俄帕", "Saneopa", "politics", "throne",
    (["萨涅俄帕", "萨涅俄帕城"], ["Saneopa"]),
    "萨涅俄帕：隘口之上的内陆商埠与涅雷采斯家的京城；旧都岁月与迁都后的迁回之争。",
    [
        A(SL, "1", "萨涅俄帕坐落在涅维斯谷通往俄佛堤斯湖的低矮隘口上；帕拉人视湖泊为神圣之地，而城本身人声嘈杂、百业兴旺，是卡拉迪亚内陆重要的贸易中心。", "fact",
          [
              E(SL, "geo-rumor", "rumor",
                "城卡在山口上，一边是谷子一边是大湖。湖是圣湖，老辈人讲湖底下通着另一个世界——这话咱不懂，只知道城里的市集一年到头热闹。",
                [G("villager", "local", "rumor")],
                [aref("saneopa_desc")]),
              E(SL, "geo-summary", "summary",
                "萨涅俄帕扼守涅维斯谷通俄佛堤斯湖的低隘口，是内陆数一数二的商埠；湖区是帕拉人的圣地，买卖与香火在这城里各走各的门。",
                [G("townsfolk", "regional", "summary")],
                [aref("saneopa_desc")]),
          ]),
        A(SL, "2", "萨涅俄帕曾为帝都：帝国自巴拉维诺斯东迁后在此定都三百余年；阿雷尼科斯皇帝即位后把首都迁去了吕卡隆，但把帝都迁回萨涅俄帕的呼声至今未息。", "fact",
          [
              E(SL, "capital-summary", "summary",
                "老人们说这儿当过帝都，后来朝廷搬去了吕卡隆。街上还有人念叨，等帝国重归一统，就该把都城迁回来。",
                [G("townsfolk", "regional", "summary")],
                [bref("saneopa_V0"), bref("saneopa_V1")]),
              E(SL, "capital-detail", "detail",
                "帝都东迁至此已三百余年，涅雷采斯朝便坐镇此城；阿雷尼科斯登基后把首都迁去了吕卡隆，帝国也自此滑向分裂。城中故老仍以旧都自居，念念不忘迁回。",
                [G("soldier", "national", "detail")],
                [bref("saneopa_V2"), bref("saneopa_V2b")]),
              E(SL, "capital-noble", "detail",
                "迁回旧都的呼声听着响，真到了元老院里却推不动——各家在新都经营的势力盘根错节，谁也不肯把本钱挪回萨涅俄帕去。",
                [G("noble", "elite", "detail")],
                [bref("saneopa_V3")]),
          ]),
        A(SL, "3", "萨涅俄帕是涅雷采斯家的根据地：彭同·涅雷采斯在此深耕元老院政治、侍奉卢孔而不逾矩；城墙坚厚，商路由此辐射半个北帝国的粮道。", "fact",
          [
              E(SL, "neretzes-rumor", "rumor",
                "涅雷采斯老爷家就住在这座城，城墙比咱们村的地界还长。听说他家老王爷在潘德拉克把军队折光了，如今是小王爷当家居多。",
                [G("villager", "local", "rumor")],
                [bref("neretzes_V1a")]),
              E(SL, "neretzes-detail", "detail",
                "涅雷采斯家的根基就在萨涅俄帕：城墙厚得砸不穿，商路从城辐射开去，半个北帝国的粮食都要过他们的地盘。",
                [G("merchant", "faction", "detail")],
                [aref("saneopa_owner"), bref("neretzes_V0a")]),
              E(SL, "neretzes-headman", "detail",
                "彭同·涅雷采斯是德洛修斯皇帝的独子，潘德拉克之后没争位、没发疯，靠着萨涅俄帕这门根本在元老院里深耕细作，给卢孔当臣子当得安稳——涅雷采斯家的分量，全压在这座城上。",
                [G("headman", "national", "detail")],
                [aref("penton_backstory"), bref("neretzes_V4a")]),
          ]),
    ])

# ================= 档2 帕拉汶德（巴拉维诺斯） =================
SL = "paravenos"
paravenos = doc("doc.politics." + SL, "帕拉汶德", "Pravend", "politics", "throne",
    (["帕拉汶德", "巴拉维诺斯", "旧都"], ["Pravend", "Paravenos"]),
    "帕拉汶德（旧名巴拉维诺斯，Paravenos）：卡拉狄乌斯大帝所建第二殖民地、旧帝都；铁壁奥斯里克献城后归戴·提尔家，今为瓦兰迪亚西部重镇。帝国遗民与巴旦尼亚人的观感分层。",
    [
        A(SL, "1", "巴拉维诺斯（Paravenos）是卡拉狄乌斯大帝亲手建立的第二座重要殖民地，曾取代沙拉斯成为卡拉德人的首都；后来帝国统治重心东移，此城仍是西部经济重镇。", "fact",
          [
              E(SL, "capital-summary", "summary",
                "这座城老辈叫巴拉维诺斯，是卡拉狄乌斯大帝亲手筑的第二座大城，还当过卡拉德人的都城；后来朝廷的重心挪去了东边，它依旧是西部最肥的地界。",
                [G("townsfolk", "regional", "summary")],
                [aref("paravenos_desc")]),
              E(SL, "capital-noble", "detail",
                "沙拉斯的老户提起它都认账：帝国如日中天的时候，首都从沙拉斯北迁到了巴拉维诺斯——那一笔写进了帝国的鼎盛年代。",
                [G("noble", "elite", "detail")],
                [aref("charas_desc")]),
          ]),
        A(SL, "2", "瓦兰迪亚人「铁壁」奥斯里克入侵时，认定此城作为权力宝座贵于作为掠获财源，与城中元老协商献城；城归奥斯里克旁支戴·提尔家，改称帕拉汶德至今。", "fact",
          [
              E(SL, "fall-summary", "summary",
                "当年「铁臂」奥斯里克打过来，没屠城——他跟城里元老谈妥了献城，把整座城囫囵吞了下来。城从那时起换了名号，叫帕拉汶德，落在奥斯里克旁支戴·提尔家手里。",
                [G("soldier", "national", "summary")],
                [aref("paravenos_desc")]),
              E(SL, "fall-noble", "detail",
                "戴·提尔家虽不是王座直系，可攥着帕拉汶德，威信不逊于公爵；瓦兰迪亚贵族议会里争影响力，讲的就是那句「谁掌控帕拉汶德，谁就掌控西部商路」。",
                [G("noble", "elite", "detail")],
                [bref("paravenos_V1a"), bref("paravenos_V1b")]),
          ]),
        A(SL, "3", "如今的帕拉汶德是多重记忆叠在一座城里：瓦兰迪亚人当它是自己的西部王冠；帝国遗民记得它的旧名与沦陷；巴旦尼亚山民只当它是砍圣林、铺石路的异族据点。", "fact",
          [
              E(SL, "vlandia-rumor", "rumor",
                "帕拉汶德是咱瓦兰迪亚自己的城，铁臂奥斯里克当年拿下的——那可是把战利品变成本事的祖宗。城里的好营生，如今都归自家人。",
                [G("villager", "local", "rumor", culture_ids=["vlandia"])],
                [bref("paravenos_V0a")]),
              E(SL, "battania-rumor", "rumor",
                "那座城？帝国人在时下山征粮，瓦兰迪亚人来了圈猎场——换谁坐城里，林子都得遭殃。不去，也不稀罕去。",
                [G("villager", "local", "rumor", culture_ids=["battania"])],
                [bref("paravenos_V4a")]),
              E(SL, "market-detail", "detail",
                "城里市场、法庭、竞技场都归瓦兰迪亚贵族掌管；做买卖的只认关税和行会规矩，百年前那纸投降协定，没人在秤上计较。",
                [G("merchant", "faction", "detail")],
                [bref("paravenos_V0a"), bref("paravenos_V0b")]),
              E(SL, "imperial-noble", "detail",
                "帝国贵族私下提起帕拉汶德，说的还是旧名巴拉维诺斯——卡拉狄乌斯亲手建起的荣耀之城如今易帜，多数人把它当作帝国走向衰落的又一块界碑。",
                [G("noble", "elite", "detail")],
                [bref("paravenos_V2a"), bref("paravenos_V3a")]),
          ]),
    ])

# ---------------- 落盘（authoring-out + workspace 双写） ----------------
for d in [saneopa, paravenos]:
    fname = d["id"].split(".")[-1] + ".yaml"
    body = ydump(d)
    io.open(os.path.join(AO, fname), "w", encoding="utf-8", newline="\n").write(body)
    io.open(os.path.join(WS, fname), "w", encoding="utf-8", newline="\n").write(body)
    print("written:", fname, "assertions:", len(d["assertions"]),
          "expressions:", sum(len(a["expressions"]) for a in d["assertions"]))
db.close()
print("OK")
