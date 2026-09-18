# -*- coding: utf-8 -*-
"""补探：榜单第一梯队里还缺的几个（gte-multilingual 族、bge-zh 家族的 onnx）。"""
import json, urllib.request, os

TARGETS = [
    ("Alibaba-NLP", "gte-multilingual-reranker-base"),
    ("iic", "gte-multilingual-reranker-base"),
    ("AI-ModelScope", "gte-multilingual-reranker-base"),
    ("Xenova", "gte-multilingual-base"),
    ("Alibaba-NLP", "gte-multilingual-base"),
    ("AI-ModelScope", "gte-multilingual-base"),
    ("Xenova", "bge-large-zh-v1.5"),
    ("AI-ModelScope", "bge-large-zh-v1.5"),
    ("Xenova", "bge-m3"),
    ("Xenova", "bge-reranker-base"),
    ("Xenova", "bge-reranker-v2-m3"),
    ("Xenova", "ms-marco-MiniLM-L6-v2"),
]

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_reranker_survey", "extra_onnx.json")


def get(url):
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0", "Accept": "application/json"})
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.loads(r.read().decode("utf-8", "replace"))


def top_files(org, name, root=""):
    u = "https://modelscope.cn/api/v1/models/%s/%s/repo/files?Revision=master&Root=%s" % (org, name, root)
    d = get(u)
    body = d.get("Data") or d
    fl = (body.get("Files") or body.get("files")) if isinstance(body, dict) else None
    return [{"name": f.get("Name") or f.get("name"), "size": f.get("Size") or f.get("size"),
             "type": f.get("Type") or f.get("type")} for f in (fl or [])]


res = {}
for org, name in TARGETS:
    key = "%s/%s" % (org, name)
    try:
        top = top_files(org, name)
        dirs = [f["name"] for f in top if (f["type"] or "").lower() in ("tree", "dir", "folder")]
        entry = {"dirs": dirs}
        print("== %-46s dirs=%s" % (key, dirs))
        if "onnx" in dirs:
            fl = top_files(org, name, "onnx")
            entry["onnx"] = fl
            for f in fl:
                print("      onnx/%-38s %8.2f MB" % (f["name"], (f["size"] or 0) / 1048576))
        res[key] = entry
    except Exception as e:
        res[key] = {"error": "%s" % type(e).__name__}
        print("== %-46s ERROR %s" % (key, type(e).__name__))

with open(OUT, "w", encoding="utf-8") as fh:
    json.dump(res, fh, ensure_ascii=False, indent=2)
print("WROTE", OUT)
