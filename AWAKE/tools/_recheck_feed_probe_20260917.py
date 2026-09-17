# -*- coding: utf-8 -*-
"""只读探针：看 _retrieval_cases_20260916.json 与 _feed_sweep_20260917.json 的结构。
本脚本不写任何文件，只打印。"""
import json, os, io, sys

sys.stdout.reconfigure(encoding="utf-8")

HERE = os.path.dirname(os.path.abspath(__file__))

def peek(name):
    p = os.path.join(HERE, name)
    with io.open(p, "r", encoding="utf-8") as f:
        d = json.load(f)
    print("=" * 70)
    print("FILE:", name, "  size:", os.path.getsize(p))
    print("top type:", type(d).__name__)
    if isinstance(d, dict):
        print("top keys:", list(d.keys())[:40])
        for k, v in list(d.items())[:6]:
            print("  -", k, "->", type(v).__name__,
                  (len(v) if hasattr(v, "__len__") else v))
            if isinstance(v, list) and v and isinstance(v[0], dict):
                print("     first item keys:", list(v[0].keys()))
                print("     first item:", json.dumps(v[0], ensure_ascii=False)[:400])
            elif isinstance(v, dict):
                print("     sub keys:", list(v.keys())[:20])
    elif isinstance(d, list):
        print("len:", len(d))
        if d and isinstance(d[0], dict):
            print("first item keys:", list(d[0].keys()))
            print("first item:", json.dumps(d[0], ensure_ascii=False)[:400])

for n in ("_retrieval_cases_20260916.json", "_feed_sweep_20260917.json"):
    try:
        peek(n)
    except Exception as e:
        print("ERR", n, type(e).__name__, e)
