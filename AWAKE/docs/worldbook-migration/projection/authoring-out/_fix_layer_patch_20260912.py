# -*- coding: utf-8 -*-
"""补丁：T1 化的表达 layer summary→rumor（不变式），并同步修 corrections 留痕。"""
import io, os, re, glob

ROOT = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out"
CORR = os.path.join(ROOT, "corrections_20260912")

for path in sorted(glob.glob(os.path.join(ROOT, "*.yaml"))):
    fname = os.path.basename(path)
    with io.open(path, "r", encoding="utf-8") as f:
        raw = f.read()
    blocks = raw.split("- id: expr.")
    changed = []
    for i in range(1, len(blocks)):
        b = blocks[i]
        if "min_detail: rumor" in b and "layer: summary" in b:
            blocks[i] = b.replace("layer: summary", "layer: rumor", 1)
            changed.append(b.split("\n")[0].strip())
    if changed:
        with io.open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write("- id: expr.".join(blocks))
        corr_path = os.path.join(CORR, fname + ".md")
        with io.open(corr_path, "r", encoding="utf-8") as f:
            c = f.read()
        c += ("\n## layer 同步（补丁）\n\n以下表达 layer: summary → rumor（IMPL §4.2 白描类明文：layer 降 rumor + T1；"
              "初次替换漏了 layer，不变式自检抓出，当场落改）：\n\n" +
              "\n".join("- " + x for x in changed) + "\n")
        with io.open(corr_path, "w", encoding="utf-8", newline="\n") as f:
            f.write(c)
        print(fname, "->", len(changed), "expressions:", ", ".join(changed))
print("PATCH-DONE")
