# -*- coding: utf-8 -*-
"""AWAKE 世界书「广铺批」生成器（09-25）。

产出三类知识条目，全部写进 full-geo1/authoring/：
  · clan-<code>.yaml     —— 家族档（80 个，registrar 有、条目没有的那些）
  · hero-<code>.yaml     —— 人物档（27 个有官方背景文的核心人物，含八位君主）
  · concept-<slug>.yaml  —— 概念档（23 个空子域，每个 1-3 档）

规范依据：
  · docs/worldbook-migration/WORLDBOOK-ENCYCLOPEDIA-CHARTER-20260913.md（L 规格、六身份显式落点）
  · docs/worldbook-studio-plan/awake.worldbook.authoring.v1.schema.json（唯一字段权威）

两个已实证的口径（本轮反推）：
  · source_content_hash = sha256(快照原始字节) 小写
  · quote_hash          = sha256(引文.strip() 的 utf-8 字节) 大写

★ 本脚本只写 authoring/*.yaml，不动索引、不跑编译。编译由 _uw_chain_20260925.py 承担。
"""
import io
import os
import sys
import json
import hashlib
import sqlite3
import collections
import re

ROOT = r"D:\AWAKE-Dev\AWAKE"
WS = os.path.join(ROOT, "tools", "worldbook-studio", "workspace", "full-geo1")
AUTH = os.path.join(WS, "authoring")
SDIR = os.path.join(AUTH, "sources")
REG = os.path.join(ROOT, "docs", "mappings", "persona-entity", "generations",
                   "b1-7590085e3662512d7c1c6646c529694bda9850146234827b2dafbd9f20c6a2b3",
                   "entity-registry.v1.json")
DB = r"C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db"

PROFILE_HASH = "309D10583E7B4AF05F28DE2C2E67A2528CB3D6C45752369C0F7D160FABC3F3A5"
REFERRAL_HASH = "6E17075F1F967BE28C0437121DB50E5C30809C4EFC3D2A5F22AD9BB0B3B926CD"
SOURCE_VERSION = "bannerlord-1.3.15.110062"

S_RULERS = "source.calradia.game.lore.rulers"
S_HEROBIO = "source.calradia.game.hero-bio"
S_SETTLEDESC = "source.calradia.game.settlements-desc"


def sha256_upper(s):
    return hashlib.sha256(s.strip().encode("utf-8")).hexdigest().upper()


# ── author_created 唯一正确形态（schema $defs.author_created，unevaluatedProperties:false）──
AUTHOR_ID = "author.awake_worldbook_rollout_2026"


def AC(reason_zh, status="draft"):
    return collections.OrderedDict([
        ("author_id", AUTHOR_ID),
        ("reason", collections.OrderedDict([("zh-CN", reason_zh)])),
        ("review_status", status),
    ])


def load_src_hashes():
    p = os.path.join(ROOT, "tools", "_uw_sources_20260925.json")
    return json.load(io.open(p, encoding="utf-8"))


def load_snapshot(fname):
    """读 `key => text` 快照，返回 dict。"""
    d = {}
    p = os.path.join(SDIR, fname)
    for line in io.open(p, encoding="utf-8"):
        if " => " in line:
            k, v = line.split(" => ", 1)
            d[k.strip()] = v.strip()
    return d


# ---------------------------------------------------------------- 六身份 grant
# 百科化宪章 §三 2：villager/townsfolk/soldier/merchant/headman/noble 六身份
# 必须显式直配。另加 commoner（villager/townsfolk 的父）、tavernkeeper、
# notable、ransom_broker 覆盖链条，避免"继承缺口=簿记黑洞"。
CORE6 = ["profile.villager", "profile.townsfolk", "profile.soldier",
         "profile.merchant", "profile.headman", "profile.noble"]


def grants(scope_map):
    """scope_map: profile_id -> (scope, min_detail)"""
    return [{"profile_id": p, "scope": s, "min_detail": d}
            for p, (s, d) in scope_map.items()]


def g6(layer, local=("profile.villager", "profile.townsfolk"),
       faction=("profile.merchant", "profile.tavernkeeper"),
       national=("profile.soldier", "profile.headman"),
       elite=("profile.noble",)):
    """把六身份 + 常用身份铺成 grant 列表。"""
    m = {}
    for p in local:
        m[p] = ("local", layer)
    m["profile.commoner"] = ("local", layer)
    for p in faction:
        m[p] = ("faction", layer)
    m["profile.notable"] = ("regional", layer)
    for p in national:
        m[p] = ("national", layer)
    for p in elite:
        m[p] = ("elite", layer)
    return grants(m)


def src_ref(sid, content_hash, locator, quote):
    return {
        "source_id": sid,
        "source_version": SOURCE_VERSION if sid.startswith("source.calradia.game") else "animusforge-20260912",
        "source_content_hash": content_hash,
        "locator": locator,
        "quote_hash": sha256_upper(quote),
        "quote": quote.strip(),
    }


def yaml_scalar(v):
    """把 python 值转成 yaml 行内标量。"""
    if isinstance(v, bool):
        return "true" if v else "false"
    if v is None:
        return "null"
    if isinstance(v, (int, float)):
        return str(v)
    s = str(v)
    if s == "":
        return "''"
    # 需要引号的情形：含冒号+空格、井号、引号、以特殊字符开头
    need = (": " in s or s.startswith(("-", "?", "&", "*", "!", "|", ">", "%", "@", "`", "[", "{", "#"))
            or "#" in s or '"' in s or "'" in s or s != s.strip())
    if need:
        return '"' + s.replace("\\", "\\\\").replace('"', '\\"') + '"'
    return s


def dump_yaml(obj, indent=0):
    """极简 yaml 序列化器：只支持 dict/list/str/int/bool/None。
    列表一律块式。字典键按插入顺序。"""
    out = []
    pad = "  " * indent
    if isinstance(obj, dict):
        for k, v in obj.items():
            if isinstance(v, (dict, list)) and v:
                out.append("%s%s:" % (pad, k))
                out.append(dump_yaml(v, indent + 1))
            elif isinstance(v, (dict, list)):
                # 空容器
                out.append("%s%s: %s" % (pad, k, "[]" if isinstance(v, list) else "{}"))
            else:
                out.append("%s%s: %s" % (pad, k, yaml_scalar(v)))
    elif isinstance(obj, list):
        for it in obj:
            if isinstance(it, dict):
                first = True
                for k, v in it.items():
                    if first:
                        if isinstance(v, (dict, list)) and v:
                            out.append("%s- %s:" % (pad, k))
                            out.append(dump_yaml(v, indent + 2))
                        elif isinstance(v, (dict, list)):
                            out.append("%s- %s: %s" % (pad, k, "[]" if isinstance(v, list) else "{}"))
                        else:
                            out.append("%s- %s: %s" % (pad, k, yaml_scalar(v)))
                        first = False
                    else:
                        if isinstance(v, (dict, list)) and v:
                            out.append("%s  %s:" % (pad, k))
                            out.append(dump_yaml(v, indent + 2))
                        elif isinstance(v, (dict, list)):
                            out.append("%s  %s: %s" % (pad, k, "[]" if isinstance(v, list) else "{}"))
                        else:
                            out.append("%s  %s: %s" % (pad, k, yaml_scalar(v)))
            else:
                out.append("%s- %s" % (pad, yaml_scalar(it)))
    else:
        out.append("%s%s" % (pad, yaml_scalar(obj)))
    return "\n".join(out)


def write_doc(filename, doc):
    p = os.path.join(AUTH, filename)
    body = dump_yaml(doc) + "\n"
    with io.open(p, "w", encoding="utf-8", newline="\n") as f:
        f.write(body)
    return p


def base_doc(doc_id, title_zh, title_en, domain, subdomain, summary_zh,
             entity_ids, aliases_zh, aliases_en):
    return collections.OrderedDict([
        ("schema_version", "awake.worldbook.authoring.v1"),
        ("revision", 1),
        ("id", doc_id),
        ("title", collections.OrderedDict([("zh-CN", title_zh), ("en", title_en)])),
        ("status", "needs_review"),
        ("domain", domain),
        ("subdomain", subdomain),
        ("universe", "awake_current"),
        ("era", collections.OrderedDict([("key", "current"), ("certainty", "bounded")])),
        ("content_tier", "base"),
        ("aliases", collections.OrderedDict([
            ("zh-CN", aliases_zh), ("en", aliases_en)])),
        ("entity_ids", entity_ids),
        ("summary", collections.OrderedDict([("zh-CN", summary_zh)])),
        ("registry_bindings", collections.OrderedDict([
            ("profile_registry_version", "1.0.0"),
            ("profile_registry_hash", PROFILE_HASH),
            ("referral_registry_version", "1.0.0"),
            ("referral_registry_hash", REFERRAL_HASH)])),
    ])


def build_assertion(aid, kind, text_zh, srcs, expressions, revision=1):
    a = collections.OrderedDict([
        ("id", aid), ("revision", revision), ("kind", kind),
        ("text", collections.OrderedDict([("zh-CN", text_zh)])),
        ("sources", srcs),
        ("expressions", expressions),
    ])
    return a


def build_expr(eid, layer, text_zh, srcs, grant_list, deny_list=None, revision=1):
    return collections.OrderedDict([
        ("id", eid), ("revision", revision), ("layer", layer),
        ("text", collections.OrderedDict([("zh-CN", text_zh)])),
        ("sources", srcs), ("grants", grant_list), ("denies", deny_list or []),
    ])


# =====================================================================
# 家族档
# =====================================================================
KINGDOM_ZH = {
    "empire": "北帝国", "empire_w": "西帝国", "empire_s": "南帝国",
    "vlandia": "瓦兰迪亚", "sturgia": "斯特吉亚", "battania": "巴旦尼亚",
    "khuzait": "库赛特", "aserai": "阿塞莱", "nord": "诺德",
}
CULTURE_ZH = {
    "empire": "帝国", "vlandia": "瓦兰迪亚", "sturgia": "斯特吉亚",
    "battania": "巴旦尼亚", "khuzait": "库赛特", "aserai": "阿塞莱", "nord": "诺德",
}
# 家族名 → 该家族在 27 条官方背景文里被怎么说的（A 级素材）
# ⚠️ 口径：这里只写「中文名 → 定位用的锚句片段」，**引文一律由 extract_quote() 从快照原文切出**，
#    禁止手抄整句（09-25 手抄掉句 ⇒ WB-SOURCE-001 ×21）。
CLAN_BIO_NOTE = {
    "clan_sturgia_2": ("库洛夫", "库洛夫家族的波耶"),
    "clan_sturgia_3": ("瓦吉罗夫", "瓦吉罗夫这个斯特吉亚年轻家族"),
    "clan_battania_1": ("芬·格鲁芬多克", "芬·格鲁芬多克是他家族的名号"),
    "clan_battania_2": ("芬·登吉尔", "芬·登吉尔家族的族长"),
    "clan_empire_west_3": ("狄俄尼科斯", "狄俄尼科斯，世代卫戍着帝国西北部的防线"),
    "clan_empire_south_2": ("列奥尼帕得斯", "列奥尼帕得斯家族的族长"),
    "clan_empire_north_4": ("印珀斯托雷斯", "印珀斯托雷斯家族的一名女贵族"),
    "clan_empire_north_3": ("涅雷采斯", "涅雷采斯家族一直以来就属于寡头阵营"),
    "clan_empire_west_2": ("瓦罗斯", "阿庇斯·瓦罗斯是旧帝国元老院中"),
    "clan_khuzait_2": ("库吉特", "墨速宜是库吉特的首领"),
    "clan_khuzait_3": ("阿契特", "库赛特的阿契特部的大那颜"),
    "clan_aserai_2": ("萨兰", "尼姆尔是萨兰部族的一名年轻勇士"),
    "clan_aserai_3": ("吉勒德", "吉勒德部族的一名年轻女孩"),
}


def extract_quote(bio_snapshot, anchor):
    """从 game-hero-bio.txt 快照里，按 anchor 定位到那一行，整行正文作为引文返回。

    口径：引文必须是快照某个 `key => body` 的 **body 全文逐字节子串**，
    否则 validate 的 WB-SOURCE-001 会判红。手抄必错，故一律机械切取。
    """
    hits = [body for body in bio_snapshot.values() if anchor in body]
    if len(hits) != 1:
        raise RuntimeError("引文锚点命中 %d 条（须恰为 1）：%s" % (len(hits), anchor))
    return hits[0]

# 家族名 → 势力性格（只在官方文里说过时才写）
CLAN_TIER_ZH = {"1": "小族", "2": "中族", "3": "大族", "4": "望族", "5": "巨族", "6": "王族"}


def gen_clan_entries(roster, registry_by_eid, src_hashes, bio_snapshot):
    made = []
    skipped = []
    kbio_hash = src_hashes[S_HEROBIO]["hash"]
    for r in roster:
        code = r["code"]
        eid = r["eid"]
        nm_zh = r["name_zh"]
        nm_en = r["name_en"] or code
        # 硬规矩第 0 条：官方译名只许来自权威表。registry 里 display_name_zh 为 null
        # 且 name_source=unknown（未安装 DLC 的家族）⇒ 无权威名，不得自造，跳过留批。
        if not nm_zh:
            skipped.append((code, "无官方中文名（未安装 DLC，registry name_source=unknown）"))
            continue
        kd = r["kingdom"] or ""
        kd_zh = KINGDOM_ZH.get(kd, "")
        tier = r["tier"] or ""
        tier_zh = CLAN_TIER_ZH.get(tier, "")
        members = registry_by_eid.get(eid, {}).get("member_entity_ids") or []
        home = r["home"]

        # 家族成员中文名
        mem_names = []
        reg = registry_by_eid
        for me in members:
            mh = reg.get(me)
            if mh and mh.get("display_name_zh"):
                mem_names.append(mh["display_name_zh"].replace("“", "").replace("”", ""))

        # ---- 断言 1：家族归属与势力 ----
        if kd_zh:
            text1 = "%s家族是%s%s的一家贵族；家中共有%d名在册成员%s。" % (
                nm_zh, kd_zh, ("的" + tier_zh) if tier_zh else "",
                len(members),
                ("，其中有" + "、".join(mem_names[:3])) if mem_names else "")
        else:
            text1 = "%s家族；家中共有%d名在册成员。" % (nm_zh, len(members))

        # 来源：有 bio 的用 bio，没有的用 registry（作者登记）
        if code in CLAN_BIO_NOTE:
            disp, anchor = CLAN_BIO_NOTE[code]
            quote = extract_quote(bio_snapshot, anchor)
            srcs1 = [{
                "source_id": S_HEROBIO,
                "source_version": SOURCE_VERSION,
                "source_content_hash": kbio_hash,
                "locator": "bannerlord.db#heroes.text",
                "quote_hash": sha256_upper(quote),
                "quote": quote,
            }]
        else:
            srcs1 = None

        expr_rumor_txt = "%s家的名号，%s一带的人都听过；族里的事由族中长辈拿主意。" % (nm_zh, kd_zh or "本地")
        expr_detail_txt = text1

        a1_expr = [
            build_expr("expr.clan-%s-rumor" % code, "rumor", expr_rumor_txt, srcs1 or [],
                       g6("rumor")),
            build_expr("expr.clan-%s-detail" % code, "detail", expr_detail_txt, srcs1 or [],
                       g6("detail")),
        ]
        a1 = build_assertion("assertion.clan-%s-1" % code, "fact", text1, srcs1, a1_expr)
        # sources 与 author_created 互斥：无 bio 的家族走 author_created 登记
        if srcs1 is None:
            a1.pop("sources", None)
            a1["author_created"] = AC("由家族注册表（member_entity_ids / related_codes）机械事实登记，"
                                      "无官方叙述文可引，按 D 级原创登记留痕。")
            # 把 author_created 插到 sources 的位置（text 之后、expressions 之前）
            a1 = collections.OrderedDict([
                ("id", a1["id"]), ("revision", 1), ("kind", "fact"),
                ("text", a1["text"]), ("author_created", a1["author_created"]),
                ("expressions", a1["expressions"]),
            ])
            # 表达式也要从 sources 换成 author_created
            for e in a1["expressions"]:
                e.pop("sources", None)
                e["author_created"] = AC("同上：家族注册表机械事实，按 D 级原创登记留痕。")
                e_ordered = collections.OrderedDict([
                    ("id", e["id"]), ("revision", 1), ("layer", e["layer"]),
                    ("text", e["text"]), ("author_created", e["author_created"]),
                    ("grants", e["grants"]), ("denies", e["denies"]),
                ])
                e.clear()
                e.update(e_ordered)

        doc = base_doc(
            "doc.politics.clan-%s" % code, nm_zh, nm_en,
            "politics", "clans", text1,
            [eid],
            [nm_zh + "家", nm_zh], [nm_en, nm_en + " Clan"],
        )
        doc["sources"] = srcs1 if srcs1 else []
        if not srcs1:
            doc.pop("sources", None)
            doc["author_created"] = AC("家族注册表机械事实登记（无官方叙述文）。")
        doc["authority"] = collections.OrderedDict([
            ("owner", "awake_canon"), ("conflict_policy", "canon_wins")])
        doc["assertions"] = [a1]
        write_doc("clan-%s.yaml" % code, doc)
        made.append(code)
    return made, skipped


# =====================================================================
# 英雄档（含各国国王 / 君主）
# =====================================================================
# 八王国君主（王族 clan_*_1 的族长）——甲方点名"各国国王/君主也要做"。
MONARCH_CODES = ["lord_1_1", "lord_1_7", "lord_1_14", "lord_2_1",
                 "lord_3_1", "lord_4_1", "lord_5_1", "lord_6_1"]
MONARCH_TITLE = {
    "lord_1_1": "北帝国皇帝", "lord_1_7": "西帝国皇帝", "lord_1_14": "南帝国女皇",
    "lord_2_1": "斯特吉亚大公", "lord_3_1": "阿塞莱苏丹",
    "lord_4_1": "瓦兰迪亚国王", "lord_5_1": "巴旦尼亚至高王", "lord_6_1": "库赛特可汗",
}


def gen_hero_entries(registry_by_eid, src_hashes, bio_snapshot, bio_by_code):
    made, skipped = [], []
    khash = src_hashes[S_HEROBIO]["hash"]
    by_code = {}
    for e in registry_by_eid.values():
        c = e.get("hero_code")
        if c:
            by_code[c] = e

    for code, body in bio_by_code.items():
        e = by_code.get(code)
        if not e:
            skipped.append((code, "registry 里没有对应 hero 码"))
            continue
        eid = e["entity_id"]
        nm = (e.get("display_name_zh") or "").strip()
        if not nm:
            skipped.append((code, "registry 无官方中文名"))
            continue
        rc = e.get("related_codes") or {}
        kd = rc.get("kingdom") or ""
        kd_zh = KINGDOM_ZH.get(kd, "")
        cl = rc.get("clan") or ""
        home = rc.get("home_settlement") or ""
        is_monarch = code in MONARCH_CODES

        # 断言 1：这个人是谁 —— 官方背景文原文打头，我的定位句在后
        if is_monarch:
            text1 = "%s%s" % (body, "（%s，%s的族长，%s。）" % (
                MONARCH_TITLE[code], e.get("family_name_zh") or cl, kd_zh or "本国"))
        else:
            text1 = body

        title_en = nm
        alias_zh = [nm]
        if e.get("family_name_zh"):
            alias_zh.append(e["family_name_zh"] + "家的" + nm)
        if is_monarch:
            alias_zh.append(MONARCH_TITLE[code])

        srcs = [src_ref(S_HEROBIO, khash, "bannerlord.db#heroes.text", body)]

        # 表达分档：君主给定 elite 档（贵族上层），其余按身份给
        if is_monarch:
            g_rumor = g6("rumor", elite=("profile.noble", "profile.noble_high_steward"))
            g_detail = g6("detail", elite=("profile.noble", "profile.noble_high_steward"))
            rumor_txt = "%s的名号在%s无人不知——他是这片土地上说了算的那个人。" % (nm, kd_zh or "本地")
        else:
            g_rumor = g6("rumor")
            g_detail = g6("detail")
            rumor_txt = "%s这个名字，%s一带的贵族圈子里都听过。" % (nm, kd_zh or "本地")

        exprs = [
            build_expr("expr.hero-%s-rumor" % code, "rumor", rumor_txt, srcs, g_rumor),
            build_expr("expr.hero-%s-detail" % code, "detail", text1, srcs, g_detail),
        ]
        a1 = build_assertion("assertion.hero-%s-1" % code, "fact", text1, srcs, exprs)

        doc = base_doc("doc.politics.hero-%s" % code, nm, title_en,
                       "politics",
                       "kingdoms" if is_monarch else "clans",
                       text1[:70], [eid], alias_zh, [nm])
        doc["sources"] = srcs
        doc["authority"] = collections.OrderedDict([
            ("owner", "game_state"), ("conflict_policy", "runtime_wins")])
        doc["assertions"] = [a1]
        write_doc("hero-%s.yaml" % code, doc)
        made.append(code)
    return made, skipped


# =====================================================================
# 概念档（21 个空子域，补"完整世界观"的广度）
# =====================================================================
S_CONCEPTS = "source.calradia.game.concepts"


def gen_concept_entries(plan, src_hashes, concept_snapshot, concept_meta):
    """concept_meta: stringId -> 百科原文"""
    made, d_level = [], []
    chash = src_hashes[S_CONCEPTS]["hash"]

    for sub, slug, t_zh, t_en, sids, lead, alias_zh in plan:
        doc_id = "doc.%s.%s" % (sub.split("/")[0], slug)
        texts = [concept_meta[s] for s in sids if s in concept_meta]
        missing = [s for s in sids if s not in concept_meta]
        if missing:
            raise RuntimeError("%s 的素材 %s 不在快照里" % (slug, missing))

        if texts:
            quote = texts[0]                      # 引文＝第一条素材原文（机械取自快照）
            body = "".join(texts)                  # 正文＝全部素材拼接
            text1 = lead + body
            srcs = [src_ref(S_CONCEPTS, chash,
                            "bannerlord.db#localization.%s" % sids[0], quote)]
        else:
            text1 = lead
            srcs = None

        # rumor 档＝"街面上会怎么传"：取引文首句作可传播的说法（不是套模板，
        # 否则正文与素材脱钩、读起来是病句，09-25 已犯）。
        first_sent = ""
        if texts:
            for sep in ("。", "；", "！", "?"):
                if sep in texts[0]:
                    first_sent = texts[0].split(sep)[0] + sep
                    break
            if not first_sent:
                first_sent = texts[0][:40]
        rumor_txt = ("听人说，%s" % first_sent) if first_sent else (
            "%s——这种事，问谁也说不周全。" % t_zh)

        exprs = [
            build_expr("expr.%s-rumor" % slug, "rumor", rumor_txt, srcs or [],
                       g6("rumor")),
            build_expr("expr.%s-detail" % slug, "detail", text1, srcs or [],
                       g6("detail")),
        ]
        a1 = build_assertion("assertion.%s-1" % slug, "fact", text1, srcs, exprs)
        authority = collections.OrderedDict(
            [("owner", "game_state"), ("conflict_policy", "runtime_wins")])
        if srcs is None:
            a1.pop("sources", None)
            a1["author_created"] = AC(
                "该子域在官方 CNs 里没有可引的整段叙述文，按 D 级原创登记留痕。")
            a1 = collections.OrderedDict([
                ("id", a1["id"]), ("revision", 1), ("kind", "fact"),
                ("text", a1["text"]), ("author_created", a1["author_created"]),
                ("expressions", a1["expressions"]),
            ])
            for e in a1["expressions"]:
                e.pop("sources", None)
                e["author_created"] = AC("同上：官方无整段叙述文，D 级原创登记。")
                eo = collections.OrderedDict([
                    ("id", e["id"]), ("revision", 1), ("layer", e["layer"]),
                    ("text", e["text"]), ("author_created", e["author_created"]),
                    ("grants", e["grants"]), ("denies", e["denies"]),
                ])
                e.clear(); e.update(eo)
            authority = collections.OrderedDict(
                [("owner", "awake_canon"), ("conflict_policy", "canon_wins")])
            d_level.append(slug)

        doc = base_doc(doc_id, t_zh, t_en, sub.split("/")[0], sub.split("/")[1],
                       lead, [], [t_zh] + list(alias_zh), [t_en])
        if srcs:
            doc["sources"] = srcs
        else:
            doc["author_created"] = AC("通识概念档；官方无整段叙述文，D 级原创登记。")
        doc["authority"] = authority
        doc["assertions"] = [a1]
        write_doc("%s.yaml" % slug, doc)
        made.append(slug)
    return made, d_level


def main():
    src_hashes = load_src_hashes()
    reg = json.load(io.open(REG, encoding="utf-8"))
    registry_by_eid = {e["entity_id"]: e for e in reg["entities"]}
    roster = json.load(io.open(os.path.join(ROOT, "tools", "_uw_clan_roster_20260925.json"),
                               encoding="utf-8"))
    bio = load_snapshot("game-hero-bio.txt")
    bio_by_code = collections.OrderedDict(
        (ln.split(" => ", 1)[0], ln.split(" => ", 1)[1])
        for ln in io.open(os.path.join(SDIR, "game-hero-bio.txt"), encoding="utf-8")
        .read().splitlines() if " => " in ln)

    which = sys.argv[1] if len(sys.argv) > 1 else "all"
    written = []
    if which in ("concepts", "all"):
        sys.path.insert(0, os.path.join(ROOT, "tools"))
        import _uw_concept_plan_20260925 as CP
        cm = load_snapshot("game-concepts.txt")
        made, dl = gen_concept_entries(CP.PLAN, src_hashes, cm, cm)
        written += made
        print("概念档写出 %d 个（覆盖 %d 个子域；其中 D 级登记 %d 个：%s）" % (
            len(made), len(set(p[0] for p in CP.PLAN)), len(dl), "、".join(dl)))
    if which in ("heroes", "all"):
        made, skipped = gen_hero_entries(registry_by_eid, src_hashes, bio, bio_by_code)
        written += ["hero-%s" % c for c in made]
        print("英雄档写出 %d 个（其中君主 %d 个）" % (
            len(made), sum(1 for c in made if c in MONARCH_CODES)))
        for c, why in skipped:
            print("  跳过 %-24s %s" % (c, why))
    if which in ("clans", "all"):
        made, skipped = gen_clan_entries(roster, registry_by_eid, src_hashes, bio)
        written += ["clan-%s" % c for c in made]
        print("家族档写出 %d 个" % len(made))
        print("跳过 %d 个：" % len(skipped))
        for c, why in skipped:
            print("  %-24s %s" % (c, why))

    # 落盘本批精确名单：编译链据此选档，**不靠前缀猜**（前缀会误吞旧探针档）
    lp = os.path.join(ROOT, "tools", "_uw_written_20260925.json")
    io.open(lp, "w", encoding="utf-8").write(
        json.dumps(written, ensure_ascii=False, indent=1))
    print("\n本批名单落盘 %s（%d 档）" % (lp, len(written)))


if __name__ == "__main__":
    main()
