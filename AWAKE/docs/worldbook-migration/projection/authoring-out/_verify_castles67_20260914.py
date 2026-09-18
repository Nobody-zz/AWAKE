# -*- coding: utf-8 -*-
"""67 城堡档结构自检（编译前）：双写一致 / quote_hash / aliases 收村名 / entity_ids / grants 层一致。
只读，不依赖编译。用法：python _verify_castles67_20260914.py
"""
import io, os, json, re, hashlib, yaml

HERE = os.path.dirname(os.path.abspath(__file__))
WS_AUTH = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring"
SNAP = os.path.join(WS_AUTH, "sources", "game-castles-desc.txt")


def sha(b):
    return hashlib.sha256(b).hexdigest().upper()


def main():
    inv = json.load(io.open(os.path.join(HERE, "_castle_inventory_20260914.json"), encoding="utf-8"))
    snap_hash = sha(open(SNAP, "rb").read())
    problems = []
    n = 0
    for sid in sorted(inv):
        c = inv[sid]
        slug = re.sub(r"[^a-z0-9]+", "-", c["en"].lower()).strip("-")
        fn = f"castle-{slug}.yaml"
        p1 = os.path.join(HERE, fn)
        p2 = os.path.join(WS_AUTH, fn)
        if not (os.path.exists(p1) and os.path.exists(p2)):
            problems.append([sid, "missing-file", fn]); continue
        b1 = open(p1, "rb").read(); b2 = open(p2, "rb").read()
        if b1 != b2:
            problems.append([sid, "dual-write-mismatch", fn]); continue
        d = yaml.safe_load(b1.decode("utf-8"))
        n += 1
        if d.get("id") != f"doc.geography.castle-{slug}":
            problems.append([sid, "bad-id", d.get("id")])
        if d.get("subdomain") != "settlements":
            problems.append([sid, "bad-subdomain", d.get("subdomain")])
        # entity_ids 自身锚
        if d.get("entity_ids") != [f"entity.settlement.{sid.lower()}"]:
            problems.append([sid, "bad-entity_ids", d.get("entity_ids")])
        # aliases 收下辖村名
        az = d.get("aliases", {}).get("zh-CN", [])
        if c["cn"] not in az or "城堡" not in az:
            problems.append([sid, "alias-missing-self", az])
        for v in c["villages"]:
            if v["cn"] and v["cn"] not in az:
                problems.append([sid, "alias-missing-village", v["cn"]])
        # source / quote
        s = d["sources"][0]
        if s["locator"] != f"bannerlord.castles#{sid}":
            problems.append([sid, "bad-locator", s["locator"]])
        if s["quote"] != c["cn"]:
            problems.append([sid, "quote-not-name", s["quote"]])
        if s["quote_hash"] != sha(c["cn"].encode("utf-8")):
            problems.append([sid, "quote_hash-mismatch", ""])
        if s["source_content_hash"] != snap_hash:
            problems.append([sid, "snap-hash-mismatch", ""])
        # expressions
        exprs = d["assertions"][0]["expressions"]
        if [e["layer"] for e in exprs] != ["rumor", "detail"]:
            problems.append([sid, "bad-layers", [e["layer"] for e in exprs]])
        for e in exprs:
            for g in e["grants"]:
                if g["min_detail"] != e["layer"]:
                    problems.append([sid, "min_detail!=layer", e["id"]])
    print(f"检查 {n}/{len(inv)} 档；snapshot hash {snap_hash[:16]}")
    print("问题数:", len(problems))
    for p in problems[:50]:
        print("  ", p)
    print("CASTLE67-STRUCT-DONE")


if __name__ == "__main__":
    main()
