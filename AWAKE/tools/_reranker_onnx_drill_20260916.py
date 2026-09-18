# -*- coding: utf-8 -*-
"""下钻 onnx/ 目录，取 ONNX 文件名与体积。"""
import json, urllib.request, urllib.error, os

TARGETS = [
    ("BAAI", "bge-reranker-base", "onnx"),
    ("BAAI", "bge-reranker-large", "onnx"),
    ("jinaai", "jina-reranker-v2-base-multilingual", "onnx"),
    ("cross-encoder", "mmarco-mMiniLMv2-L12-H384-v1", "onnx"),
    ("cross-encoder", "mmarco-mMiniLMv2-L12-H384-v1", "openvino"),
    ("BAAI", "bge-reranker-v2-m3", "assets"),
]

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_reranker_survey", "onnx_drill.json")


def get(url):
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0", "Accept": "application/json"})
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.loads(r.read().decode("utf-8", "replace"))


def files_at(org, name, root):
    u = "https://modelscope.cn/api/v1/models/%s/%s/repo/files?Revision=master&Root=%s" % (org, name, root)
    d = get(u)
    body = d.get("Data") or d
    fl = (body.get("Files") or body.get("files")) if isinstance(body, dict) else None
    out = []
    for f in (fl or []):
        out.append({"name": f.get("Name") or f.get("name"), "size": f.get("Size") or f.get("size"),
                    "type": f.get("Type") or f.get("type")})
    return out


res = {}
for org, name, root in TARGETS:
    key = "%s/%s::%s" % (org, name, root)
    try:
        fl = files_at(org, name, root)
        res[key] = fl
        print("== %s" % key)
        for f in fl:
            print("   %-46s %10.2f MB  type=%s" % (f["name"], (f["size"] or 0) / 1048576, f["type"]))
    except Exception as e:
        res[key] = {"error": "%s: %s" % (type(e).__name__, e)}
        print("== %s  ERROR %s" % (key, e))

with open(OUT, "w", encoding="utf-8") as fh:
    json.dump(res, fh, ensure_ascii=False, indent=2)
print("WROTE", OUT)
