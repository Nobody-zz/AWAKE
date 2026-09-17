# -*- coding: utf-8 -*-
"""护甲形制批 · 生成器（war/weapons，21 张类卡）。

与头盔批生成器（`_rollout_head_gen_20260916.py`）的差别：
  · 快照换成 game-items-armor.txt（486 件，含躯干／披风／盾／腿／手五个部位）；
  · 卡 id 前缀 `doc.war.weapons-armor-<slug>`（与头盔 `-head-` 并列，便于名录与门禁按前缀筛）；
  · **多一条硬断言**：每个 ref 必须真属于**本卡**（拿 `_armor_taxonomy_20260918.json` 的成员表核对）。
    头盔批只断言"在快照里 + 不跨卡重复"，抓不住"引了别卡的件"。本批实测抓到 4 处，已修。
  · **再加两条硬断言**（09-18 矩阵验收时踩出来，见脚本内"硬断言 3"）：每卡 tag 唯一（否则表达 id 重复）、
    每卡至少一条"通用 detail 层"表达（否则不带文化的询问者只能拿到 rumor 层，读数是 partial）。
  · 盾的护值来自 <Weapon> 节点（快照里记 `护 N`），其余部位来自 <Armor>；文案里的数字已按快照核过。

⚠️ 文化限定的 rumor 只能挂给 `villager`；文化 detail 挂 headman/merchant/soldier/noble。
   理由（09-16 实测）见头盔批生成器文件头：投影选表达 = 身份/条件分 × 10 + 层号，
   文化匹配 +20 ⇒ +200，压过层差；且父链 noble→notable→commoner、merchant→townsfolk→commoner。

产物：21 档 yaml **双写** authoring-out ＋ workspace/full-geo1/authoring。
"""
import collections
import hashlib
import io
import json
import os

import yaml

HERE = os.path.dirname(os.path.abspath(__file__))
WS_AUTH = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring"
SNAP = os.path.join(WS_AUTH, "sources", "game-items-armor.txt")
TAX = r"D:/AWAKE-Dev/AWAKE/tools/_armor_taxonomy_20260918.json"
SRC_ID = "source.calradia.game.items-armor"
SRC_VER = "bannerlord-1.3.15.110062"
L2 = ["_l2_armor_20260918.json"]
CARD_PREFIX = "armor-"

RUMOR_GRANTS = [
    ("profile.commoner", "local"), ("profile.villager", "local"),
    ("profile.tavernkeeper", "faction"), ("profile.ransom_broker", "faction"),
    ("profile.townsfolk", "regional"), ("profile.notable", "regional"),
    ("profile.merchant", "faction"), ("profile.headman", "national"),
    ("profile.soldier", "national"), ("profile.noble", "elite"),
]
CULTURE_RUMOR_GRANTS = [("profile.villager", "local")]
DETAIL_GRANTS = [
    ("profile.headman", "national"), ("profile.merchant", "faction"),
    ("profile.soldier", "national"), ("profile.noble", "elite"),
]
SUMMARY_GRANTS = [("profile.townsfolk", "regional"), ("profile.soldier", "national")]

REG_BIND = {
    "profile_registry_version": "1.0.0",
    "profile_registry_hash": "309D10583E7B4AF05F28DE2C2E67A2528CB3D6C45752369C0F7D160FABC3F3A5",
    "referral_registry_version": "1.0.0",
    "referral_registry_hash": "6E17075F1F967BE28C0437121DB50E5C30809C4EFC3D2A5F22AD9BB0B3B926CD",
}


def sha(b):
    return hashlib.sha256(b).hexdigest().upper()


def grants_for(kind, culture=False):
    if kind == "rumor" and culture:
        return CULTURE_RUMOR_GRANTS
    return {"rumor": RUMOR_GRANTS, "detail": DETAIL_GRANTS, "summary": SUMMARY_GRANTS}[kind]


def main():
    raw = io.open(SNAP, encoding="utf-8").read()
    file_hash = sha(raw.encode("utf-8"))
    segs = {}
    for line in raw.splitlines():
        if not line.startswith("armor."):
            continue
        eid, seg = line[6:].split(" => ", 1)
        segs[eid] = seg
    print("snapshot items:", len(segs), "hash:", file_hash[:16])

    # 成员表：entityId -> 卡 slug（本批 21 张）
    tax = json.load(io.open(TAX, encoding="utf-8"))
    owner = {}
    for c in tax["cards"]:
        key = c.get("card") or c["key"]
        for it in c["items"]:
            prev = owner.get(it["entityId"])
            assert prev is None or prev == key, "成员表里 %s 同时归 %s 与 %s" % (it["entityId"], prev, key)
            owner[it["entityId"]] = key

    cards = []
    for fn in L2:
        cards += json.load(io.open(os.path.join(HERE, fn), encoding="utf-8"))["cards"]
    print("cards:", len(cards))

    # ---- 硬断言 1：ref 在快照里；不跨卡重复；且**真属于本卡** ----
    seen = {}
    for c in cards:
        for a in c["asserts"]:
            for r in a["refs"]:
                assert r in segs, "ref 不在快照: %s (%s)" % (r, c["slug"])
                if r in seen and seen[r] != c["slug"]:
                    raise SystemExit("ref 跨卡重复: %s  ←  %s 与 %s" % (r, seen[r], c["slug"]))
                seen[r] = c["slug"]
                if owner.get(r) != c["slug"]:
                    raise SystemExit("ref 不属于本卡: %s 被 %s 引用，实际归 %s"
                                     % (r, c["slug"], owner.get(r)))
    print("refs unique & in-own-card:", len(seen))

    # ---- 硬断言 2：21 张卡与分类定稿一一对应 ----
    assert len(cards) == len(tax["cards"]), (len(cards), len(tax["cards"]))
    assert {c["slug"] for c in cards} == {c.get("card") or c["key"] for c in tax["cards"]}
    assert len({c["zh"] for c in cards}) == len(cards), "卡标题有重复"
    print("cards:", ", ".join(c["slug"] for c in cards))

    # ---- 硬断言 3：分层表达的两条"静默失效"防线（2026-09-18 加，矩阵验收时踩出来）----
    #
    # 甲、**每卡 tag 必须唯一**。表达 id 是 `expr.weapon-<卡>-<tag>`（见下面 `exprs.append`），
    #     tag 重名 ⇒ 上线件同一张卡里躺着两条同 id 表达，谁按 id 取都只会拿到第一条。
    #     踩到的：torso-civil / torso-gambeson / torso-mail / cape-mantle / hands ——
    #     这 5 张的两个断言都带通用 detail，两次都写成 tag="detail"。
    #
    # 乙、**每卡至少要有一条不带 culture 的 detail 层表达**（"通用 detail 层"）。
    #     踩到的：torso-ring（文化句占了通用位）与 shield-wicker（两个断言的 detail 都是文化限定）
    #     ⇒ 一个不带文化的商人/头人/士兵/贵族问「环甲」「柳条盾」时只拿得到 rumor 层，
    #       探针读数 partial，且 partial 不渲染标题 ⇒ 正文变成一段没有出处的口语 + 别的条目，
    #       是"答非所问"的形态。**它不抛错、不报错，只是答得比该答的矮一层** —— 所以必须用断言钉住。
    for c in cards:
        tg = [e["tag"] for a in c["asserts"] for e in a["exprs"]]
        dup = {k: v for k, v in collections.Counter(tg).items() if v > 1}
        if dup:
            raise SystemExit("卡 %s 的 tag 重名（会生成同 id 表达）: %s" % (c["slug"], dup))
        gen = [e for a in c["asserts"] for e in a["exprs"]
               if e["layer"] == "detail" and not e.get("culture")]
        if not gen:
            raise SystemExit("卡 %s 没有不带 culture 的 detail 层表达（通用 detail 层缺失）" % c["slug"])
    print("分层表达自检: tag 唯一 + 每卡都有通用 detail 层  ✔")

    made = []
    for c in cards:
        slug = c["slug"]
        full = CARD_PREFIX + slug

        def src_of(eid):
            quote = segs[eid].split(" | 出处", 1)[0]
            return {"source_id": SRC_ID, "source_version": SRC_VER,
                    "source_content_hash": file_hash,
                    "locator": "bannerlord.items.xml#%s" % eid,
                    "quote_hash": sha(quote.encode("utf-8")), "quote": quote}

        doc_srcs, seen_src = [], set()
        for a in c["asserts"]:
            for r in a["refs"]:
                if r not in seen_src:
                    seen_src.add(r)
                    doc_srcs.append(src_of(r))

        asserts = []
        for i, a in enumerate(c["asserts"], 1):
            asrc = [src_of(r) for r in a["refs"]]
            exprs = []
            for e in a["exprs"]:
                culture = e.get("culture")
                g = [{"profile_id": p, "scope": s, "min_detail": e["layer"]}
                     for p, s in grants_for(e["grants"], bool(culture))]
                if culture:
                    for x in g:
                        x["culture_ids"] = ["entity.culture.%s" % culture]
                exprs.append({
                    "id": "expr.weapon-%s-%s" % (full, e["tag"]),
                    "revision": 1, "layer": e["layer"],
                    "text": {"zh-CN": e["text"]}, "sources": asrc,
                    "grants": g, "denies": [],
                })
            asserts.append({
                "id": "assertion.weapon-%s-%d" % (full, i), "revision": 1, "kind": "fact",
                "text": {"zh-CN": a["text"]}, "sources": asrc, "expressions": exprs,
            })

        doc = {
            "schema_version": "awake.worldbook.authoring.v1",
            "revision": 1,
            "id": "doc.war.weapons-%s" % full,
            "title": {"zh-CN": c["zh"], "en": c["en"]},
            "status": "needs_review",
            "domain": "war",
            "subdomain": "weapons",
            "universe": "awake_current",
            "era": {"key": "current", "certainty": "bounded"},
            "content_tier": "base",
            "aliases": {"zh-CN": c["al_zh"], "en": c["al_en"]},
            "summary": {"zh-CN": c["summary"]},
            "registry_bindings": dict(REG_BIND),
            "sources": doc_srcs,
            "authority": {"owner": "awake_canon", "conflict_policy": "canon_wins"},
            "assertions": asserts,
        }
        doc = json.loads(json.dumps(doc))

        class NoAlias(yaml.dumper.Dumper):
            def ignore_aliases(self, data):
                return True

        text = yaml.dump(doc, Dumper=NoAlias, allow_unicode=True, sort_keys=False,
                         default_flow_style=False, width=100)
        assert "&id" not in text and "*id" not in text

        fname = "weapons-%s.yaml" % full
        for root in (HERE, WS_AUTH):
            io.open(os.path.join(root, fname), "w", encoding="utf-8", newline="\n").write(text)
        made.append(fname)
        n_culture = sum(1 for a in asserts for e in a["expressions"]
                        if e["grants"] and e["grants"][0].get("culture_ids"))
        print("  %-34s asserts=%d exprs=%d srcs=%d 文化限定层次=%d" % (
            fname, len(asserts), sum(len(a["expressions"]) for a in asserts),
            len(doc_srcs), n_culture))

    print("\ngenerated:", len(made), "-> dual-written to authoring-out + workspace")


if __name__ == "__main__":
    main()
