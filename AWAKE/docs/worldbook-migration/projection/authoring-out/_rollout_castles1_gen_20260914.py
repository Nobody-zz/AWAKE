# -*- coding: utf-8 -*-
"""城堡铺开批（P1 全量 67 座）生成器。
- 城堡无官方描述文 ⇒ A 级锚＝官方名（localization `Settlements.Settlement.name.castle_*`）。
- 联系（本批核心）：castle 档 aliases 收「自身名＋城堡＋下辖村名」，entity_ids 绑自身聚落锚。
- 独立来源登记 source.calradia.game.castles；快照 game-castles-desc.txt。
- L2（人工层）按文化分组存 _l2_c_*.json，本脚本合并后写档。
"""
import io, os, json, re, hashlib, yaml

OUT = os.path.dirname(os.path.abspath(__file__))
WS_AUTH = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring"
SRC_ID = "source.calradia.game.castles"
SRC_VER = "bannerlord-1.3.15.110062"
SNAP = "game-castles-desc.txt"
L2_PARTS = ["_l2_c_aserai.json", "_l2_c_battania.json", "_l2_c_empire_n.json",
            "_l2_c_empire_s.json", "_l2_c_empire_w.json", "_l2_c_khuzait.json",
            "_l2_c_sturgia.json", "_l2_c_vlandia.json"]


def load_l2():
    l2 = {}
    for p in L2_PARTS:
        d = json.load(io.open(os.path.join(OUT, p), encoding="utf-8"))
        for k, v in d.items():
            assert k not in l2, "重复 sid: " + k
            assert isinstance(v, list) and len(v) == 3, k
            l2[k] = tuple(v)
    return l2


RUMOR_GRANTS = [
    ("profile.commoner", "local"), ("profile.villager", "local"),
    ("profile.tavernkeeper", "faction"), ("profile.ransom_broker", "faction"),
    ("profile.townsfolk", "regional"), ("profile.notable", "regional"),
    ("profile.merchant", "faction"), ("profile.headman", "national"),
    ("profile.soldier", "national"), ("profile.noble", "elite"),
]
DETAIL_GRANTS = [
    ("profile.headman", "national"), ("profile.merchant", "faction"),
    ("profile.soldier", "national"), ("profile.noble", "elite"),
]
REG_BIND = {
    "profile_registry_version": "1.0.0",
    "profile_registry_hash": "309D10583E7B4AF05F28DE2C2E67A2528CB3D6C45752369C0F7D160FABC3F3A5",
    "referral_registry_version": "1.0.0",
    "referral_registry_hash": "6E17075F1F967BE28C0437121DB50E5C30809C4EFC3D2A5F22AD9BB0B3B926CD",
}


def sha(b):
    return hashlib.sha256(b).hexdigest().upper()


def main():
    inv = json.load(io.open(os.path.join(OUT, "_castle_inventory_20260914.json"), encoding="utf-8"))
    L2 = load_l2()
    assert set(L2.keys()) == set(inv.keys()), (
        "L2 与清单不符: 缺 " + str(sorted(set(inv) - set(L2))) +
        " 多 " + str(sorted(set(L2) - set(inv))))
    print("inventory:", len(inv), "; L2:", len(L2))

    rows = {k: v for k, v in sorted(inv.items())}

    lines = []
    for sid, c in rows.items():
        vv = "、".join(v["cn"] for v in c["villages"])
        lines.append(f"castle.{sid} => {c['cn']} | {c['en']} | 文化 {c['culture']} | 领主 {c['owner']} | "
                     f"繁荣 {c['prosperity']} | 下辖 {vv} | 出处 bannerlord_settlements#{sid} | 名token {c['name_token']}")
    snap_text = "\n".join(lines) + "\n"
    file_hash = sha(snap_text.encode("utf-8"))
    io.open(os.path.join(WS_AUTH, "sources", SNAP), "w", encoding="utf-8", newline="\n").write(snap_text)

    reg = {
        "source_id": SRC_ID, "source_version": SRC_VER, "source_nature": "game_snapshot",
        "universe": "awake_current", "era": "current", "locator_root": SNAP,
        "source_content_hash": file_hash, "content_tier": "base",
        "license_status": "permitted", "use_status": "active", "valid_until": None,
        "imported_at": "2026-09-14T00:00:00Z", "normalization_version": "utf8-lf-no-bom-v1",
    }
    with io.open(os.path.join(WS_AUTH, "sources", "source-game-castles.yaml"), "w",
                 encoding="utf-8", newline="\n") as f:
        yaml.safe_dump(reg, f, allow_unicode=True, sort_keys=False)

    made = []
    for sid, c in rows.items():
        cn, en = c["cn"], c["en"]
        assert cn and en, sid
        qhash = sha(cn.encode("utf-8"))
        src = {"source_id": SRC_ID, "source_version": SRC_VER,
               "source_content_hash": file_hash,
               "locator": f"bannerlord.castles#{sid}",
               "quote_hash": qhash, "quote": cn}
        a_text, rumor, detail = L2[sid]
        slug = re.sub(r"[^a-z0-9]+", "-", en.lower()).strip("-")
        assert slug, sid
        vcn = [v["cn"] for v in c["villages"] if v["cn"]]
        ven = [v["en"] for v in c["villages"] if v["en"]]
        doc = {
            "schema_version": "awake.worldbook.authoring.v1",
            "revision": 1,
            "id": f"doc.geography.castle-{slug}",
            "title": {"zh-CN": cn, "en": en},
            "status": "needs_review",
            "domain": "geography",
            "subdomain": "settlements",
            "universe": "awake_current",
            "era": {"key": "current", "certainty": "bounded"},
            "content_tier": "base",
            # 2026-09-17：类别词「城堡」不再进 aliases。
            # 它挂在几百条上 ⇒ 覆盖 > 40 ⇒ 被索引卫生 R1 剔掉 ⇒ 指不到任何东西，
            # 还留了「谁都能沾」的坑（德里亚特的「德里亚特·村庄」就是这么劫走 272 条泛问的）。
            # 类别词已升格为概念词条 doc.geography.settlement-types-castle。
            "aliases": {"zh-CN": [cn] + vcn, "en": [en, sid] + ven},
            "entity_ids": [f"entity.settlement.{sid.lower()}"],
            "summary": {"zh-CN": a_text},
            "registry_bindings": dict(REG_BIND),
            "sources": [src],
            "authority": {"owner": "awake_canon", "conflict_policy": "canon_wins"},
            "assertions": [{
                "id": f"assertion.castle-{slug}-1", "revision": 1, "kind": "fact",
                "text": {"zh-CN": a_text},
                "sources": [src],
                "expressions": [
                    {"id": f"expr.castle-{slug}-rumor", "revision": 1, "layer": "rumor",
                     "text": {"zh-CN": rumor}, "sources": [src],
                     "grants": [{"profile_id": p, "scope": s, "min_detail": "rumor"}
                                for p, s in RUMOR_GRANTS], "denies": []},
                    {"id": f"expr.castle-{slug}-detail", "revision": 1, "layer": "detail",
                     "text": {"zh-CN": detail}, "sources": [src],
                     "grants": [{"profile_id": p, "scope": s, "min_detail": "detail"}
                                for p, s in DETAIL_GRANTS], "denies": []},
                ],
            }],
        }
        doc = json.loads(json.dumps(doc))

        class NoAlias(yaml.dumper.Dumper):
            def ignore_aliases(self, data):
                return True

        text = yaml.dump(doc, Dumper=NoAlias, allow_unicode=True, sort_keys=False,
                         default_flow_style=False, width=100)
        assert "&id" not in text and "*id" not in text
        fn = f"castle-{slug}.yaml"
        io.open(os.path.join(OUT, fn), "w", encoding="utf-8", newline="\n").write(text)
        io.open(os.path.join(WS_AUTH, fn), "w", encoding="utf-8", newline="\n").write(text)
        made.append(fn)
    print("generated:", len(made), "castles; snapshot hash:", file_hash[:16])


if __name__ == "__main__":
    main()
