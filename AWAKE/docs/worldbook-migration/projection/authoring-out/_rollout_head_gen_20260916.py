# -*- coding: utf-8 -*-
"""头盔形制批 · 生成器（war/weapons，10 张类卡）。

与器物 PoC 生成器的差别：
  · 卡讲**形制类别**（doc.war.weapons-head-*），不绑定某一顶具体盔；
  · A 级引证＝官方物品属性快照 game-items-head.txt 的**代表件属性行**（子串可定位）；
  · 每件物品只归一张卡（生成器断言），避免跨卡 quote_hash 重复；
  · 文化专属层次用 grants 的 culture_ids 限定。

⚠️ **文化限定的 rumor 只能挂给 `villager`**（09-16 实测坐实，两轮才到位）：
  投影选表达的分数 = `身份/条件分 × 10 + 层号`（WorldKnowledgeQueryService.SelectExpression:286），
  而条件分里文化匹配 +20（WorldbookIdentityEvaluator:83）⇒ **+200，压过层差（层号最多差 3）**。
  ⇒ 若文化 rumor 命中了能看 detail 的身份，它会把 detail 抢走、层级判定降为 partial
    （症状：巴旦尼亚贵族问护颊盔，拿到村民口吻的 rumor）。
  **父链是第二个坑**：编译包 identities 里 noble→notable→commoner、merchant→townsfolk→commoner，
  发给 notable/commoner/townsfolk 会**沿父链命中贵族/商人**，等于没收窄。
  `villager` 不是任何 detail 身份的先祖 ⇒ 唯一安全落点（既有 `weapons-crossbow` 正是这么写的）。
  文化 detail 照旧挂 headman/merchant/soldier/noble：同层对同层，文化版胜出，结果仍是 known。

产物：10 档 yaml **双写** authoring-out ＋ workspace/full-geo1/authoring。
"""
import hashlib
import io
import json
import os
import yaml

HERE = os.path.dirname(os.path.abspath(__file__))
WS_AUTH = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring"
SNAP = os.path.join(WS_AUTH, "sources", "game-items-head.txt")
SRC_ID = "source.calradia.game.items-head"
SRC_VER = "bannerlord-1.3.15.110062"
L2 = ["_l2_head_a_20260916.json", "_l2_head_b_20260916.json"]

RUMOR_GRANTS = [
    ("profile.commoner", "local"), ("profile.villager", "local"),
    ("profile.tavernkeeper", "faction"), ("profile.ransom_broker", "faction"),
    ("profile.townsfolk", "regional"), ("profile.notable", "regional"),
    ("profile.merchant", "faction"), ("profile.headman", "national"),
    ("profile.soldier", "national"), ("profile.noble", "elite"),
]
# 文化限定的 rumor：**只给 villager**（理由见文件头）。
# 父链实测（编译包 identities）：noble→notable→commoner；merchant→townsfolk→commoner；
# headman→notable；soldier→commoner。⇒ 发给 notable/commoner/townsfolk 都会**沿父链命中贵族/商人/士兵**，
# 把他们的 detail 抢走。只有 villager 不是任何 detail 身份的先祖。
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
        if not line.startswith("head."):
            continue
        eid, seg = line[5:].split(" => ", 1)
        segs[eid] = seg
    print("snapshot items:", len(segs), "hash:", file_hash[:16])

    cards = []
    for fn in L2:
        cards += json.load(io.open(os.path.join(HERE, fn), encoding="utf-8"))["cards"]
    print("cards:", len(cards))

    # ---- 硬断言：引证必须存在；每件物品只归一张卡 ----
    seen = {}
    for c in cards:
        for a in c["asserts"]:
            for r in a["refs"]:
                assert r in segs, "ref 不在快照: %s (%s)" % (r, c["slug"])
                if r in seen and seen[r] != c["slug"]:
                    raise SystemExit("ref 跨卡重复: %s  ←  %s 与 %s" % (r, seen[r], c["slug"]))
                seen[r] = c["slug"]
    print("refs unique across cards:", len(seen))
    print("cards:", ", ".join(c["slug"] for c in cards))

    made = []
    for c in cards:
        slug = c["slug"]

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
                    "id": "expr.weapon-%s-%s" % (slug, e["tag"]),
                    "revision": 1, "layer": e["layer"],
                    "text": {"zh-CN": e["text"]}, "sources": asrc,
                    "grants": g, "denies": [],
                })
            asserts.append({
                "id": "assertion.weapon-%s-%d" % (slug, i), "revision": 1, "kind": "fact",
                "text": {"zh-CN": a["text"]}, "sources": asrc, "expressions": exprs,
            })

        doc = {
            "schema_version": "awake.worldbook.authoring.v1",
            "revision": 1,
            "id": "doc.war.weapons-%s" % slug,
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

        fname = "weapons-%s.yaml" % slug
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
