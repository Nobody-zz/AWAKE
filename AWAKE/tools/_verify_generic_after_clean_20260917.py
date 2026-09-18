# -*- coding: utf-8 -*-
"""复查：三个泛词（村庄/城堡/城镇）在两个 authoring 目录里各挂几条、谁在挂。"""
import collections
import glob
import io
import os
import re
import sys

sys.stdout.reconfigure(encoding="utf-8")

DIRS = ["tools/worldbook-studio/workspace/full-geo1/authoring",
        "docs/worldbook-migration/projection/authoring-out"]
GENERIC = ["村庄", "城堡", "城镇"]

for d in DIRS:
    c = collections.Counter()
    owners = collections.defaultdict(list)
    comp = 0
    comp_owners = []
    nfiles = 0
    for f in glob.glob(os.path.join(d, "*.yaml")):
        nfiles += 1
        t = io.open(f, encoding="utf-8").read()
        m = re.search(r"^aliases:\s*$(.*?)^[A-Za-z_][A-Za-z0-9_]*:", t, re.M | re.S)
        if not m:
            continue
        for line in m.group(1).splitlines():
            s = line.strip()
            if not s.startswith("- "):
                continue
            v = s[2:].strip().strip('"').strip("'")
            for g in GENERIC:
                if v == g:
                    c[g] += 1
                    owners[g].append(os.path.basename(f))
            if "·" in v and any(g in v for g in GENERIC):
                comp += 1
                comp_owners.append(os.path.basename(f))
    label = "workspace/authoring" if "studio" in d else "projection/authoring-out"
    print("=== %-26s 档数 %d" % (label, nfiles))
    for g in GENERIC:
        print("     「%s」 = %d 条   %s" % (g, c[g], owners[g][:4]))
    print("     含泛词的复合别名 = %d  %s" % (comp, comp_owners[:4]))

print()
print("期望：三个泛词各 1 条（都应是 settlement-types-*.yaml）；复合别名 0 条。")
