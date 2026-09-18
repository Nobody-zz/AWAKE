# -*- coding: utf-8 -*-
"""取 bge-base-zh-v1.5 / bge-small-zh-v1.5 官方 README，抽 C-MTEB 检索分。"""
import urllib.request, os, re

TARGETS = [
    ("AI-ModelScope", "bge-base-zh-v1.5"),
    ("AI-ModelScope", "bge-small-zh-v1.5"),
    ("AI-ModelScope", "gte-base-zh"),
    ("AI-ModelScope", "gte-small-zh"),
]

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_reranker_survey", "embed_readme")


def get(url):
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(req, timeout=40) as r:
        return r.read()


os.makedirs(OUT, exist_ok=True)
for org, name in TARGETS:
    u = "https://modelscope.cn/api/v1/models/%s/%s/repo?Revision=master&FilePath=README.md" % (org, name)
    try:
        b = get(u)
        p = os.path.join(OUT, "%s__%s.md" % (org, name))
        open(p, "wb").write(b)
        t = b.decode("utf-8", "replace")
        print("== %s/%s  (%d chars)" % (org, name, len(t)))
        # 找含 C-MTEB / T2Retrieval 之类关键词的行
        for ln in t.splitlines():
            if re.search(r"Retrieval|C-MTEB|MTEB|T2Retrieval|avg", ln) and ("|" in ln or ":" in ln):
                print("   ", ln.strip()[:220])
    except Exception as e:
        print("== %s/%s  ERROR %s %s" % (org, name, type(e).__name__, e))
