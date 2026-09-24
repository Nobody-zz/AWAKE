# -*- coding: utf-8 -*-
"""查 alley-* 两档的 slug / doc id / assertion id / expr id 在哪些文件里被引用。
只读，不改任何东西。
"""
import os, re, json

ROOT = r"D:\AWAKE-Dev\AWAKE"
NEEDLES = [
    "alley-gang-leaders", "alley-struggle",
    "doc.politics.alley-gang-leaders", "doc.politics.alley-struggle",
    "politics.alley-gang-leaders", "politics.alley-struggle",
]
SKIP_DIRS = {".git", "bin", "obj", ".vs", "node_modules", "__pycache__"}
TEXT_EXT = {".cs", ".yaml", ".yml", ".json", ".md", ".py", ".ps1", ".txt", ".xml", ".cfg", ".toml"}

hits = {}
for dirpath, dirnames, filenames in os.walk(ROOT):
    dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
    for fn in filenames:
        ext = os.path.splitext(fn)[1].lower()
        if ext not in TEXT_EXT:
            continue
        p = os.path.join(dirpath, fn)
        try:
            with open(p, "r", encoding="utf-8", errors="replace") as f:
                lines = f.readlines()
        except Exception:
            continue
        for i, line in enumerate(lines, 1):
            for n in NEEDLES:
                if n in line:
                    rel = os.path.relpath(p, ROOT).replace("\\", "/")
                    hits.setdefault(rel, []).append((i, n, line.strip()[:160]))
                    break

out = []
out.append("== alley-* 引用点 ==")
for rel in sorted(hits):
    out.append("")
    out.append("### " + rel + "  (%d 处)" % len(hits[rel]))
    for (i, n, t) in hits[rel]:
        out.append("  %5d  [%s]  %s" % (i, n, t))

txt = "\n".join(out)
with open(os.path.join(ROOT, "tools", "_alley_refs_20260924.txt"), "w", encoding="utf-8") as f:
    f.write(txt)
print("files:", len(hits), "lines:", sum(len(v) for v in hits.values()))
