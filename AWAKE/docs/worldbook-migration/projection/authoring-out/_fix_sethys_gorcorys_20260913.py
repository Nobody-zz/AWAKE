# -*- coding: utf-8 -*-
"""塞堤斯河档借引批 3 村（戈耳科律斯）描述文整句 → 改官方文本连续子串（主引权归村庄档）。
同 mount-iltan / 批 2 先例。要点：quote_hash 在 quote 行【上方】，须同步重算，勿找反方向。
双写：authoring-out ＋ workspace authoring。
"""
import io, os, re, hashlib

AUTH = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out/sethys-river.yaml"
WS = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring/sethys-river.yaml"

OLD = "戈耳科律斯坐落于歌里亚河畔，那是塞堤斯河流经三河河谷最南端的支流。"
NEW = "塞堤斯河流经三河河谷最南端的支流"  # 官方 CNs 描述文连续子串
NEW_H = hashlib.sha256(NEW.encode("utf-8")).hexdigest().upper()


def fix(path):
    lines = io.open(path, encoding="utf-8").read().split("\n")
    hits = 0
    last_hash_idx = None
    for i, ln in enumerate(lines):
        s = ln.strip()
        if s.startswith("quote_hash:"):
            last_hash_idx = i
        elif s.startswith("quote:"):
            val = s.split("quote:", 1)[1].strip()
            if val == OLD:
                indent = ln[: len(ln) - len(ln.lstrip())]
                lines[i] = f"{indent}quote: {NEW}"
                # 同步其上方最近的 quote_hash 行
                assert last_hash_idx is not None and last_hash_idx == i - 1, (path, i)
                hl = lines[last_hash_idx]
                hind = hl[: len(hl) - len(hl.lstrip())]
                lines[last_hash_idx] = f"{hind}quote_hash: {NEW_H}"
                hits += 1
            last_hash_idx = None
    io.open(path, "w", encoding="utf-8", newline="\n").write("\n".join(lines))
    return hits


h1 = fix(AUTH)
h2 = fix(WS)
print("replaced:", h1, h2, "new hash:", NEW_H[:16])
