# -*- coding: utf-8 -*-
"""临时：列出 economy / culture 域各档 id + subdomain + 文件名。"""
import io, os, glob, yaml
HERE = os.path.dirname(os.path.abspath(__file__))
for p in sorted(glob.glob(os.path.join(HERE, "*.yaml"))):
    y = yaml.safe_load(io.open(p, encoding="utf-8"))
    if not str(y.get("id", "")).startswith("doc."):
        continue
    if y.get("domain") in ("economy", "culture"):
        print(f"{y['domain']:<8} {y.get('subdomain',''):<16} {y['id']:<42} {os.path.basename(p)}")
