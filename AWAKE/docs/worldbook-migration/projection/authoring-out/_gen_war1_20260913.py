# -*- coding: utf-8 -*-
"""战争批第一批（war 6 档）：斯特吉亚军制/瓦兰迪亚军事力量/库赛特军事力量/皇家侍卫/马穆鲁克/弩。
工序：拆→核→重写。A=官方文本（程序化拉取）；B=编年史原声（变体切片带校验）。
裁定：军制条目＝战争背景知识（Max 09-13 域定义裁定）；无锚点概念条不写 entity_ids，检索靠 aliases；
文化门为主角；可汗亲卫（B 级草判名）正文一律写官方名"可汗卫士"。
"""
import io, os, json, hashlib, sqlite3, yaml

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

# ---------------- A 级引文（stringId, None=程序拉全文） ----------------
A_FULL = {
    # 瓦兰迪亚军事力量
    "vl_origin":  ("1Ww5lg6I", None),   # 瓦兰迪亚人起源：雇佣兵与冒险家
    "vl_levy":    ("3In09YKu", None),   # 村庄征召兵义务
    # 库赛特军事力量
    "kh_horsearcher": ("r88TwVdE", None),  # 弓骑兵主攻力量
    "kh_rabble":      ("iDzjO25n", None),  # 杂牌军
    "kh_nomad":       ("5FanO0Lb", None),  # 草原部落为报酬而战
    "kh_horse":       ("Settlements.Settlement.text.village_K6_1", None),  # 喀拉罕军马来源
    # 马穆鲁克
    "mamluk_def": ("Q4zplpf7", None),   # 官方释义全文
    # 弩
    "xb_karad":   ("5WMHiC9P", None),   # 瓦兰迪亚弩列装/卡拉德弩砲手持版
    "xb_south":   ("qzsjGcEz", None),   # 南瓦兰迪亚劲弩手传统
    "xb_heavy":   ("5FPhltaZ", None),   # 劲弩：卡拉迪亚最强弩
}
for k, (sid, q) in A_FULL.items():
    if q is None:
        A_FULL[k] = (sid, loc_cn(sid))
        assert A_FULL[k][1], "A text missing in DB: %s" % k

# ---------------- B 级引文（编年史变体切片） ----------------
B_QUOTES = {
    "st_V0":  ("rule_斯特吉亚的军事制度__斯特吉亚的军事制度.json", 0, "斯特吉亚的军事制度建立在波耶私兵与平民动员相结合的基础上。每一位波耶都维持着自己的亲卫队，这些亲卫是脱产的职业战士，装备精良、训练有素。"),
    "st_V1":  ("rule_斯特吉亚的军事制度__斯特吉亚的军事制度.json", 1, "我们的瓦连格——那些贵族子弟组成的精锐步兵——站在盾墙最前排，飞斧开路，斧头收割，简单直接。"),
    "st_V2":  ("rule_斯特吉亚的军事制度__斯特吉亚的军事制度.json", 2, "他们之中最优秀的能升成资深瓦连格，到了那个级别就有资格骑马了——在北方能骑马作战是身份和实力的象征。"),
    "st_V3":  ("rule_斯特吉亚的军事制度__斯特吉亚的军事制度.json", 3, "筝形盾、单手斧、飞斧，每一样都是自己掏钱打的。我们在盾墙最前排，大公或波耶一声令下，先掷飞斧打乱敌阵，然后拔出斧头撞上去。"),
    "st_V4":  ("rule_斯特吉亚的军事制度__斯特吉亚的军事制度.json", 0, "他们的军队不像帝国那样有严格的军团编制，也不像瓦兰迪亚那样以重骑兵为主，而是以坚固的步兵阵线为根本"),
    "vl_V0":  ("rule_瓦兰迪亚军事力量__瓦兰迪亚军事力量.json", 0, "最优秀的骑士将被授予“方旗骑士”的称号"),
    "vl_V1":  ("rule_瓦兰迪亚军事力量__瓦兰迪亚军事力量.json", 1, "你作为一个瓦兰迪亚人，还是能感觉出来自己国家的军事力量强大"),
    "vl_V3":  ("rule_瓦兰迪亚军事力量__瓦兰迪亚军事力量.json", 3, "恐怖的瓦兰迪亚像达摩克里斯之剑一样悬在西方，你清楚的知道他们的所向披靡，所以你害怕他们"),
    "vl_V4":  ("rule_瓦兰迪亚军事力量__瓦兰迪亚军事力量.json", 4, "也对瓦兰迪亚在杰屈朗的屠杀有所耳闻。不过，你完全不知道他们为何这么强大"),
    "vl_V10": ("rule_瓦兰迪亚军事力量__瓦兰迪亚军事力量.json", 10, "你知道大陆西端有个瓦兰迪亚，军队很强，似乎骑兵强？还装备了一种叫弩的东西"),
    "kh_V0":  ("rule_库赛特军事力量__库赛特军事力量.json", 0, "男人是骑射手，他的马就是他的命，弓是他爷爷传下来的"),
    "kh_V1":  ("rule_库赛特军事力量__库赛特军事力量.json", 1, "这头是怯薛和可汗亲卫，那头是牧民征召兵，中间是达尔罕和枪骑兵在平衡"),
    "kh_V2":  ("rule_库赛特军事力量__库赛特军事力量.json", 2, "他们的骑兵从不靠近，绕着你的方阵射一整天，你追击他们就散，你停下他们就回来"),
    "kh_V5":  ("rule_库赛特军事力量__库赛特军事力量.json", 5, "打仗不是什么荣耀，是义务。那颜征召，我就带上我的马和弓去，不去不行。"),
    "kh_V9":  ("rule_库赛特军事力量__库赛特军事力量.json", 9, "怯薛为核心的精锐骑射手是大陆上最优秀的弓骑兵，这一点毋庸置疑"),
    "kh_V10": ("rule_库赛特军事力量__库赛特军事力量.json", 10, "听说他们打仗跟苍蝇一样，光射箭不接战，耗到你没力气了他们再冲"),
    "rg_V0":  ("rule_诺德皇家侍卫__诺德皇家侍卫.json", 0, "诺德皇家侍卫是诺德王国最精锐的步兵，在整个卡拉迪亚都威名赫赫"),
    "rg_V1":  ("rule_诺德皇家侍卫__诺德皇家侍卫.json", 1, "他们的大圆盾能挡下箭雨和骑枪，手中的侍卫矛让骑兵有来无回，拔出长剑近战时更是无人能挡"),
    "rg_V7":  ("rule_诺德皇家侍卫__诺德皇家侍卫.json", 7, "诺德皇家侍卫是典型的重装剑盾步兵，代表了诺德军事体系的最高成就"),
    "rg_V6":  ("rule_诺德皇家侍卫__诺德皇家侍卫.json", 6, "他们的盾墙极难突破，冷不丁飞过来的飞斧总能造成混乱"),
    "rg_V8":  ("rule_诺德皇家侍卫__诺德皇家侍卫.json", 8, "不过在平原上，他们缺乏机动性，终究是方旗骑士的猎物"),
    "rg_V9":  ("rule_诺德皇家侍卫__诺德皇家侍卫.json", 9, "但他们有一点比我们更极致，那就是海战。斯特吉亚的战士也懂海，可诺德人简直就是从海里长出来的"),
    "mm_V1":  ("rule_马穆鲁克__马穆鲁克.json", 1, "“花金子养大的，就得拿命还。”埃米尔和谢赫们算这笔账时毫不含糊"),
    "mm_V1b": ("rule_马穆鲁克__马穆鲁克.json", 1, "马穆鲁克只管杀人，剩下的不用他们操心"),
    "xb_V1":  ("rule_弩__弩.json", 1, "别的国家养弓箭手得从小练，我们养弩手几周就够。这就是为什么瓦兰迪亚的弩手比谁家都多——我们不是靠武艺，是靠工艺"),
    "xb_V2":  ("rule_弩__弩.json", 2, "我家里就有一把，轻弩，挂在灶台边上的墙上，下雨天拿出来擦擦。村里发下来的，说是战时征召用的"),
    "xb_V4":  ("rule_弩__弩.json", 4, "一个奴隶拿弩能射死一个贵族，这种事在帝国有先例，元老院的老爷爷们不喜欢"),
    "xb_V11": ("rule_弩__弩.json", 11, "长老拎起来翻了个面，说这东西没有灵魂，弓有弓灵，弩没有"),
    "xb_V12": ("rule_弩__弩.json", 12, "但库赛特战士不拿弩。弩装填太慢"),
    "xb_V0":  ("rule_弩__弩.json", 0, "什么是弩？不知道"),
}
for k, (rf, vi, q) in B_QUOTES.items():
    c = rule_variant(rf, vi)
    assert q in c, "B quote not in rule: %s\n  want: %s\n  have: %s" % (k, q, c[:120])

# ---------------- 源文件与登记 ----------------
def H(x):
    return hashlib.sha256(x.encode("utf-8")).hexdigest().upper()

def hlower(x):
    return hashlib.sha256(x.encode("utf-8")).hexdigest()

game_txt = "\n".join("%s => %s" % (A_FULL[k][0], A_FULL[k][1]) for k in sorted(A_FULL)) + "\n"
chron_txt = "\n\n".join("%s [V%d] %s" % (B_QUOTES[k][0], B_QUOTES[k][1], B_QUOTES[k][2]) for k in sorted(B_QUOTES)) + "\n"

GAME_TXT_F = "game-lore-war1.txt"
CHRON_TXT_F = "chronicle-animusforge-war1.txt"
io.open(os.path.join(WS, "sources", GAME_TXT_F), "w", encoding="utf-8", newline="\n").write(game_txt)
io.open(os.path.join(WS, "sources", CHRON_TXT_F), "w", encoding="utf-8", newline="\n").write(chron_txt)
game_hash = hlower(game_txt)
chron_hash = hlower(chron_txt)

GAME_SRC = dict(source_id="source.calradia.game.lore.war1", source_version="bannerlord-1.3.15.110062",
                source_nature="game_snapshot", universe="awake_current", era="current", locator_root=GAME_TXT_F,
                source_content_hash=game_hash, content_tier="base", license_status="permitted",
                use_status="active", valid_until=None, imported_at="2026-09-13T00:00:00Z",
                normalization_version="utf8-lf-no-bom-v1")
CHRON_SRC = dict(source_id="source.calradia.chronicle.animusforge.war1", source_version="animusforge-20260913",
                 source_nature="chronicle", universe="awake_current", era="historical", locator_root=CHRON_TXT_F,
                 source_content_hash=chron_hash, content_tier="base", license_status="permitted",
                 use_status="active", valid_until=None, imported_at="2026-09-13T00:00:00Z",
                 normalization_version="utf8-lf-no-bom-v1")

class ND(yaml.SafeDumper):
    def ignore_aliases(self, data):
        return True

def ydump(obj):
    return yaml.dump(obj, Dumper=ND, allow_unicode=True, sort_keys=False, width=4096, default_flow_style=False)

for nm, src in [("source-game-lore-war1.yaml", GAME_SRC), ("source-chronicle-animusforge-war1.yaml", CHRON_SRC)]:
    io.open(os.path.join(WS, "sources", nm), "w", encoding="utf-8", newline="\n").write(ydump(src))

RB = {"profile_registry_version": "1.0.0",
      "profile_registry_hash": "309D10583E7B4AF05F28DE2C2E67A2528CB3D6C45752369C0F7D160FABC3F3A5",
      "referral_registry_version": "1.0.0",
      "referral_registry_hash": "6E17075F1F967BE28C0437121DB50E5C30809C4EFC3D2A5F22AD9BB0B3B926CD"}

def aref(key):
    sid, q = A_FULL[key]
    return {"source_id": GAME_SRC["source_id"], "source_version": GAME_SRC["source_version"],
            "source_content_hash": game_hash, "locator": "bannerlord.db#localization." + sid,
            "quote_hash": H(q), "quote": q}

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

docs = []

# ================= 档1 斯特吉亚的军事制度 =================
SL = "sturgia-military"
docs.append(doc("doc.war." + SL, "斯特吉亚的军事制度", "Sturgian Military System", "war", "military_system",
    (["斯特吉亚的军事制度", "斯特吉亚军制", "波耶亲卫队", "瓦连格"], ["Sturgian Military System", "Druzhinnik"]),
    "斯特吉亚军制：波耶职业亲卫队与自由民动员相结合，核心为重步兵盾墙，辅以轻骑袭扰与飞斧投射；瓦连格为贵族子弟精锐步兵，资深者有资格骑马。",
    [
        A(SL, "1", "斯特吉亚军制建立在波耶私兵与平民动员相结合的基础上：每位波耶维持脱产的职业亲卫队，装备精良、训练有素；战时大公向各波耶发征召令，波耶率亲卫并武装自由民应召；军队核心是重步兵盾墙，辅以轻骑兵袭扰和飞斧投射——既无帝国式军团编制，也不以重骑兵为主，而是以坚固步兵阵线为根本。", "fact",
          [
              E(SL, "sturgia-rumor", "rumor",
                "大公一吹号，波耶老爷带着亲兵出来，我们这些拿得起斧头的跟在后面。盾墙一列，飞斧开路——北边这地方，扛得住风雪和刀剑的步兵才靠得住。",
                [G("villager", "local", "rumor", culture_ids=["sturgia"])],
                [bref("st_V1")]),
              E(SL, "overview-summary", "summary",
                "斯特吉亚的军队靠两样：波耶的亲卫队和武装起来的自由民。核心是重步兵盾墙，轻骑兵袭扰，飞斧开路——步兵阵线是根本。",
                [G("townsfolk", "regional", "summary"), G("soldier", "national", "summary")],
                [bref("st_V0"), bref("st_V4")]),
              E(SL, "boyar-detail", "detail",
                "波耶的亲卫队是脱产的职业战士，从小在领地上养大练出来的。战时大公发征召令，波耶带亲卫打头阵，自由民跟在后面；打完仗各回各处。这套制度养不出帝国那样的十几万军团，但胜在省钱，而且每个战士都是为熟人而战——士气不是军饷换来的。",
                [G("noble", "elite", "detail")],
                [bref("st_V0"), bref("st_V2")]),
          ]),
        A(SL, "2", "瓦连格（瓦良格）是斯特吉亚军中的精锐步兵阶层：贵族子弟组成，站在盾墙最前排，自备筝形盾、单手斧与飞斧；战术为先掷飞斧打乱敌阵、再拔斧冲撞；其中最优秀者升为资深瓦连格，获得骑马作战的资格——在北方，骑马作战是身份与实力的象征。", "fact",
          [
              E(SL, "varyag-detail", "detail",
                "瓦良格的装备全是自家掏钱置办的：筝形盾、单手斧、飞斧。令下先掷飞斧搅乱敌阵，再拔斧撞上去。资深瓦良格能骑马，那就是亲卫骑兵——机动和冲击都更强。",
                [G("soldier", "national", "detail")],
                [bref("st_V3")]),
              E(SL, "rank-detail", "detail",
                "北方的军阶看马：普通战士站着打，最优秀的升资深瓦连格，到了那个级别才有资格骑马——在斯特吉亚，马背是身份和实力的界碑。",
                [G("headman", "national", "detail")],
                [bref("st_V2")]),
          ]),
    ]))

# ================= 档2 瓦兰迪亚军事力量 =================
SL = "vlandia-military"
docs.append(doc("doc.war." + SL, "瓦兰迪亚军事力量", "Vlandian Military Power", "war", "military_system",
    (["瓦兰迪亚军事力量", "瓦兰迪亚军力", "方旗骑士"], ["Vlandian Military Power", "Banner Knight"]),
    "瓦兰迪亚军力：贵族子弟自备战马从军组成骑士部队，最优秀者授方旗骑士；平民骑兵配瓦朗比骏马；海量征召弩手取之不尽。外族对其忌惮各异。",
    [
        A(SL, "1", "瓦兰迪亚人源出海外的雇佣兵与冒险家，最初受帝国雇佣保卫边疆；今其军力以骑兵与弩手立国：贵族子弟自备武器战马组成骑士部队，最优秀者授「方旗骑士」，骑竞技马冲阵，唯一克星是帝国具装骑兵；平民骑兵配罗瓦尔出产的瓦朗比骏马，与贵族骑兵相互配合。", "fact",
          [
              E(SL, "knight-detail", "detail",
                "瓦兰迪亚的老爷们打仗靠的是自家的马和甲：贵族子弟自备武器战马从军，最出色的授「方旗骑士」名号——骑竞技马，速度慢些但下盘稳，冲起阵来无坚不摧，全大陆只有帝国的具装骑兵是他们的对手。",
                [G("noble", "elite", "detail"), G("merchant", "faction", "detail")],
                [bref("vl_V0"), aref("vl_origin")]),
              E(SL, "levy-detail", "detail",
                "每座瓦兰迪亚村庄都有为领主提供征召兵的义务，定额不满，领主会亲自进村强行带人。贵族老爷很少花钱给这些庄稼汉置办装备——所以后来才有了弩：训练几天就能上阵的武器。",
                [G("noble", "elite", "detail")],
                [aref("vl_levy")]),
          ]),
        A(SL, "2", "外族对瓦兰迪亚军力的观感分野鲜明：瓦兰迪亚平民只觉本国强大；巴旦尼亚人视之为悬在西方的达摩克里斯之剑；帝国人闻杰屈朗之屠而不知其强在哪、仍自信帝国更强；远东士兵只模糊知道西端有个骑兵强、装备弩的国度。", "fact",
          [
              E(SL, "proud-rumor", "rumor",
                "咱瓦兰迪亚的兵强得很——老爷们骑大马，庄稼汉扛起弩也顶个人头。旁的不敢说，这份底气是有的。",
                [G("villager", "local", "rumor", culture_ids=["vlandia"])],
                [bref("vl_V1")]),
              E(SL, "battania-rumor", "rumor",
                "瓦兰迪亚就是悬在咱们头顶的那把剑。他们的骑士所向披靡——西边的人，少惹。",
                [G("villager", "local", "rumor", culture_ids=["battania"])],
                [bref("vl_V3")]),
              E(SL, "empire-rumor", "rumor",
                "瓦兰迪亚人厉害，厉害在哪说不清。杰屈朗那档子事倒是听说过。可要说帝国比不过他们——那不能。",
                [G("villager", "local", "rumor", culture_ids=["empire"])],
                [bref("vl_V4")]),
              E(SL, "far-soldier-summary", "summary",
                "大陆西端有个瓦兰迪亚，兵强，骑兵厉害，还使一种叫弩的东西——离得远，跟咱们没多大相干。",
                [G("soldier", "national", "summary")],
                [bref("vl_V10")]),
          ]),
    ]))

# ================= 档3 库赛特军事力量 =================
SL = "khuzait-military"
docs.append(doc("doc.war." + SL, "库赛特军事力量", "Khuzait Military Power", "war", "military_system",
    (["库赛特军事力量", "库赛特军制", "怯薛", "可汗卫士"], ["Khuzait Military Power", "Khan's Guard"]),
    "库赛特军力：以弓骑兵为攻击主力的游牧战争机器——怯薛精锐骑射手、枪骑兵撕口、达尔罕重步兵；牧民征召兵与「杂牌军」为数量底座；帝国视之为最危险的敌人。",
    [
        A(SL, "1", "弓骑兵是库赛特部落的攻击主力：可汗发动战争时部落联盟各部召集最好的士兵，以速度、耐心和狡猾作战——骑射手轮番袭扰削弱敌军，再由枪骑兵发起致命一击；怯薛为核心的精锐骑射手是大陆上最优秀的弓骑兵；但帝国评估其弱点在于缺乏强力步弓手、马匹繁育不成体系。", "fact",
          [
              E(SL, "horse-archer-rumor", "rumor",
                "草原上男人都是骑射手——马是命，弓是祖上传的。打仗跟那颜走，女人孩子赶着羊群往后撤。赢了带东西回来，输了他回不回得来，看长生天的心情。",
                [G("villager", "local", "rumor", culture_ids=["khuzait"])],
                [bref("kh_V0")]),
              E(SL, "system-detail", "detail",
                "库赛特的战争机器：怯薛精锐骑射手是大陆最好的弓骑兵，偃月刀是混战利器；枪骑兵便宜量大，是撕开缺口的刀；达尔罕是少数扛得住硬仗的重步兵。弱点也有两样——缺强力步弓手，马多但没有规范的繁育，质量参差。开阔地形上，这些短板被速度和兵力盖住了。",
                [G("noble", "elite", "detail")],
                [bref("kh_V9"), aref("kh_horsearcher")]),
          ]),
        A(SL, "2", "库赛特的征召体系一杆秤：一头是怯薛与可汗卫士，一头是牧民征召兵；未正式归属的城镇村庄被要求提供兵员，称「杂牌军」——装备简陋、缺乏训练，只能指望汗国精锐骑兵赶来解救；草原上不受汗权节制的部落仍为可汗作战，但只为报酬。", "fact",
          [
              E(SL, "levy-detail", "detail",
                "可汗的军队是一杆秤：这头是怯薛和可汗卫士，那头是牧民征召兵，中间靠达尔罕和枪骑兵找平衡。养一个帝国具装骑兵的钱够养十个骑射手——但牧民的马有好有坏，冲锋一拉开队形就散。",
                [G("noble", "elite", "detail"), G("merchant", "faction", "detail")],
                [bref("kh_V1")]),
              E(SL, "rabble-detail", "detail",
                "「杂牌军」是没正式归附的城镇村庄被摊派来的兵员——装备简陋没训练，唯一的指望是汗国精锐骑兵赶紧来解围，之前先保住自己的命。",
                [G("soldier", "national", "detail")],
                [aref("kh_rabble")]),
              E(SL, "duty-rumor", "rumor",
                "打仗不是什么荣耀，是义务。那颜一征召，备上马带上弓就得去。咱这样的人战场上最多，围在外圈射——可汗靠的不是怯薛，是成千上万个咱们这样的牧民。",
                [G("villager", "local", "rumor", culture_ids=["khuzait"])],
                [bref("kh_V5")]),
          ]),
        A(SL, "3", "帝国视角：库赛特人自兀儿浑时代起就是最危险的敌人——骑射手从不近战，绕阵射一整天，追则散、停则回；军团步兵重甲厚盾耗箭也耗士气，阵线松动时枪骑兵从侧翼杀入；草原远征每次都赔上半支军团。远方族群对库赛特仅有模糊传闻。", "fact",
          [
              E(SL, "empire-detail", "detail",
                "库赛特人是最危险的敌人，从兀儿浑时代起就不必讨论。方阵和具装骑兵的优势全被距离吞掉：追，他们散；停，他们回来围着放箭。等阵线一松，枪骑兵就从侧翼进来了。每次远征草原，都得赔上半支军团。",
                [G("noble", "elite", "detail", culture_ids=["empire"])],
                [bref("kh_V2")]),
              E(SL, "far-rumor", "rumor",
                "东边那些骑马的？听说打仗跟苍蝇似的，光射箭不接战，耗到你没力气再冲。马多，小孩都会骑射——离得远，搭不上。",
                [G("villager", "local", "rumor")],
                [bref("kh_V10")]),
          ]),
    ]))

# ================= 档4 皇家侍卫（Huscarl） =================
SL = "royal-guard"
docs.append(doc("doc.war." + SL, "皇家侍卫", "Huscarl", "war", "troops",
    (["皇家侍卫", "诺德皇家侍卫", "诺德侍卫", "侍卫矛兵"], ["Huscarl", "Royal Guard"]),
    "皇家侍卫（Huscarl）：北方最精锐的重装剑盾步兵——重甲、大圆盾、长剑侍卫矛、投掷飞斧；纪律严明，盾墙坚不可摧；自幼与海浪为伴，海战跳帮尤称无敌。各文化对其评价迥异。",
    [
        A(SL, "1", "皇家侍卫是北方最精锐的步兵：装备重甲、坚固大圆盾，主武器为长剑或侍卫矛，腰别投掷飞斧；纪律严明、武艺高强，既能结成坚不可摧的盾墙，也能近战用剑；开战前一轮飞斧常砸乱敌阵；自幼在海浪中长大，海战能力远超内陆士兵。", "fact",
          [
              E(SL, "overview-summary", "summary",
                "皇家侍卫是北边最狠的步兵：重甲大圆盾，长剑侍卫矛，腰里还别着飞斧。盾墙一结推过来，正面进攻全是白给——上了船他们更可怕。",
                [G("townsfolk", "regional", "summary"), G("soldier", "national", "summary")],
                [bref("rg_V0"), bref("rg_V1")]),
              E(SL, "nord-pride-rumor", "rumor",
                "皇家侍卫是咱诺德人的骄傲！大盾挡箭雨，侍卫矛叫骑兵有来无回，开打先一轮飞斧砸乱阵脚。海上更别提——敌船被他们靠上就等死吧。",
                [G("villager", "local", "rumor", culture_ids=["nord"])],
                [bref("rg_V1")]),
              E(SL, "scholar-detail", "detail",
                "皇家侍卫是重装剑盾步兵的典范，北方军事体系的最高成就：弃笨重双手巨斧，改剑盾与长矛组合，辅以飞斧投掷，攻防两利；再加上北方人自幼与风浪搏斗的海洋传统——海上交战，他们的战力翻倍。",
                [G("noble", "elite", "detail")],
                [bref("rg_V7")]),
          ]),
        A(SL, "2", "各方观感：帝国士兵称其盾墙极难突破、飞斧混乱难防、海战跳帮是噩梦；瓦兰迪亚人敬其为对手——盾墙挡弩矢，但平原上缺乏机动性，终是方旗骑士的猎物；斯特吉亚人认作打法相近的北方亲戚，唯海战自愧不如。", "fact",
          [
              E(SL, "empire-detail", "detail",
                "跟皇家侍卫交过手的都知道：盾墙难啃，冷不丁飞来的斧子总能搅出乱子。海上的遭遇战更是噩梦——跳帮那股凶狠熟练，咱们比不了。",
                [G("soldier", "national", "detail", culture_ids=["empire"])],
                [bref("rg_V6")]),
              E(SL, "vlandia-detail", "detail",
                "皇家侍卫值得尊敬：盾墙能挡弩矢，剑术不差。可平原上他们腿慢——再硬的盾墙，也是方旗骑士的猎物。海上另算，那股从小跟风浪搏出来的悍勇，咱们也头疼。",
                [G("noble", "elite", "detail", culture_ids=["vlandia"])],
                [bref("rg_V8")]),
              E(SL, "sturgia-rumor", "rumor",
                "皇家侍卫是咱们北边的亲戚，打法一个路数——盾墙、利剑、长矛。就是海上他们比咱们狠，简直是从海里长出来的。波耶的船见了他们的长船也得绕道。",
                [G("villager", "local", "rumor", culture_ids=["sturgia"])],
                [bref("rg_V9")]),
          ]),
    ]))

# ================= 档5 马穆鲁克 =================
SL = "mamluk"
docs.append(doc("doc.war." + SL, "马穆鲁克", "Mamluk", "war", "troops",
    (["马穆鲁克", "军事奴隶", "阿塞莱马穆鲁克"], ["Mamluk", "Military Slave"]),
    "马穆鲁克：本义「被拥有者」——阿塞莱自幼购入、严格训练的外族少年兵，脱离血缘纽带、忠诚只系于主人；成年获自由身份领俸禄，出身标记终身伴随；已上升为阿塞莱举足轻重的军事政治集团。",
    [
        A(SL, "1", "马穆鲁克一词本义为「被拥有者」或「奴隶」，特指阿塞莱自幼购入、经严格军事训练后编入精锐部队的军事奴隶阶层；来源多为突厥草原、高加索或黑海北岸的非阿塞莱少年，经奴隶商人转卖入宫廷军营，受严整的骑术、弓箭、刀术与战术训练；因自幼脱离血缘纽带、完全依附体制而被视为忠诚可靠的核心武力；成年后通常获自由身份并领取俸禄，但出身标记终身伴随；在阿塞莱苏丹国中已从侍卫部队上升为举足轻重的军事政治集团。", "fact",
          [
              E(SL, "def-detail", "detail",
                "马穆鲁克，词本义是「被拥有的人」。阿塞莱自幼买进外族少年——草原的、高加索的、黑海北岸的都有——在宫廷军营里教骑术弓箭刀术，练成精锐。这些人从小离开爹娘，除了主人和军队谁也不认，所以掌权者拿他们当最可靠的核心武力。成年后给自由身、领军饷，可「马穆鲁克」这个出身的记号跟一辈子。",
                [G("noble", "elite", "detail"), G("merchant", "faction", "detail")],
                [aref("mamluk_def")]),
              E(SL, "aserai-detail", "detail",
                "埃米尔们的账算得清：马穆鲁克自幼购入、宫廷养大，成年便是纯粹的军事资产——「花金子养大的，就得拿命还」。养闲着是亏本买卖，所以手头有马穆鲁克就急着找仗打。但也清楚他们的短处：除了打仗什么都不会，掌印管钱的差事还得交给血亲。",
                [G("noble", "elite", "detail", culture_ids=["aserai"])],
                [bref("mm_V1"), bref("mm_V1b")]),
              E(SL, "aserai-rumor", "rumor",
                "王帐跟前那些黑脸卫士，是打小买来的孩子养大的。听说他们连自己原先的姓都记不得了——就认发饷的人。",
                [G("villager", "local", "rumor", culture_ids=["aserai"])],
                [bref("mm_V1")]),
          ]),
    ]))

# ================= 档6 弩 =================
SL = "crossbow"
docs.append(doc("doc.war." + SL, "弩", "Crossbow", "war", "weapons",
    (["弩", "劲弩", "征召弩手"], ["Crossbow", "Arbalest"]),
    "弩：卡拉德弩砲的手持版本——威力大、上手快，庄稼汉训练数日即可上阵；瓦兰迪亚凭列装弩与系统训练建立海量征召弩手，南瓦兰迪亚劲弩手名满大陆；帝国限量引进，草原与林中民族各有取舍。",
    [
        A(SL, "1", "弩本质上是一种可怕的卡拉德弩砲的手持版本：瓦兰迪亚军阀发现，征召农民虽是不得已，但通过弩的列装和系统训练能让农兵发挥作用——劲弩手在最重的盔甲上也能击穿；南瓦兰迪亚有培养全卡拉迪亚最优秀劲弩手的传统，无论受雇为佣兵还是担任教官都炙手可热；「劲弩」为卡拉迪亚最强弩，弓臂加强、有效射程提高。", "fact",
          [
              E(SL, "essence-summary", "summary",
                "弩就是拿在手上的小弩砲。瓦兰迪亚人把它发给征召来的庄稼汉，练几天就能上阵——甲再厚也扛不住它抵近一箭。",
                [G("townsfolk", "regional", "summary"), G("soldier", "national", "summary")],
                [aref("xb_karad")]),
              E(SL, "vlandia-detail", "detail",
                "弩最大的好处不是威力，是省心：上个月还在晒麦子的小伙，这个月就能在阵地上扣扳机。别国养弓手得从小练，我们养弩手几周就够——瓦兰迪亚弩手比谁家都多，不是靠武艺，是靠工艺。南边那几个谷地更出了名：全卡拉迪亚最好的劲弩手都从那儿出来，佣兵也好教官也好，抢着请。",
                [G("noble", "elite", "detail", culture_ids=["vlandia"])],
                [bref("xb_V1"), aref("xb_south")]),
              E(SL, "levy-rumor", "rumor",
                "村里发的弩就挂在灶台边上，下雨天擦擦。农闲练两天，对着草靶子瞄——不讲天分，手指头扣得动就行。真打起来，咱庄稼汉拿弩也能顶个人头。",
                [G("villager", "local", "rumor", culture_ids=["vlandia"])],
                [bref("xb_V2")]),
          ]),
        A(SL, "2", "各方对弩的态度迥异：帝国经阿雷尼科斯皇帝推荐引进但元老院决议不全面换装——忌惮「奴隶拿弩能射死贵族」的先例，只给辅助部队配；巴旦尼亚视弩为无灵魂的不祥之物，缴获必砸碎；库赛特承认弩射固定目标很准但马背上使不上劲，仅辎重营配备；达西人则根本不知道弩为何物。", "fact",
          [
              E(SL, "empire-detail", "detail",
                "帝国引进了弩，但没全学。元老院当年议过全面换装，阿雷尼科斯皇帝拍板：不要。毛病不在弩，在军制——军团靠重步兵立国，更麻烦的是弩太容易让普通人变强：奴隶拿弩能射死贵族，有先例。所以只用不敢扬，配给辅助部队。前线指挥官私下都认好用，就是数量不够。",
                [G("noble", "elite", "detail", culture_ids=["empire"])],
                [bref("xb_V4")]),
              E(SL, "battania-rumor", "rumor",
                "弩是瓦兰迪亚人的东西，咱不碰。长老说那玩意儿没有灵魂——射箭不费力就是不诚心，山神不收。缴来的弩当场砸碎，铁件埋寨子外头，埋的人还得洗三遍手。",
                [G("villager", "local", "rumor", culture_ids=["battania"])],
                [bref("xb_V11")]),
              E(SL, "khuzait-rumor", "rumor",
                "弩不是草原的东西——装填得站地上踩住拉弦，马背上使不上劲。守营门、盯哨位倒能用，可库赛特战士不拿它。",
                [G("villager", "local", "rumor", culture_ids=["khuzait"])],
                [bref("xb_V12")]),
              E(SL, "darshi-rumor", "rumor",
                "弩？那是什么？没听说过。",
                [G("villager", "local", "rumor", culture_ids=["darshi"])],
                [bref("xb_V0")]),
          ]),
    ]))

# ---------------- 落盘 ----------------
for d in docs:
    fname = d["id"].split(".")[-1] + ".yaml"
    body = ydump(d)
    io.open(os.path.join(AO, fname), "w", encoding="utf-8", newline="\n").write(body)
    io.open(os.path.join(WS, fname), "w", encoding="utf-8", newline="\n").write(body)
    print("written:", fname, "assertions:", len(d["assertions"]),
          "expressions:", sum(len(a["expressions"]) for a in d["assertions"]))
db.close()
print("OK", len(docs), "docs")
