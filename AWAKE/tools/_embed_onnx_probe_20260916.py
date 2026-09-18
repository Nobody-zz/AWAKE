# -*- coding: utf-8 -*-
"""查嵌入侧候选（可替换 bge-small-zh-v1.5）在 ModelScope 的 onnx/ 目录与顶层文件。"""
import json, urllib.request, urllib.error, os

# (org, name)  嵌入/双塔候选 + 几个重排候选的顶层复核
TARGETS = [
    ("BAAI", "bge-large-zh-v1.5"),
    ("BAAI", "bge-base-zh-v1.5"),
    ("BAAI", "bge-m3"),
    ("AI-ModelScope", "bge-small-zh-v1.5"),
    ("Xenova", "bge-small-zh-v1.5"),
    ("Xenova", "bge-base-zh-v1.5"),
    ("BAAI", "bge-reranker-v2-m3"),
]

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_reranker_survey", "embed_onnx.json")


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
        res[key] = {"top": top, "dirs": dirs}
        print("== %-42s dirs=%s" % (key, dirs))
        if "onnx" in dirs:
            try:
                fl = top_files(org, name, "onnx")
                res[key]["onnx"] = fl
                for f in fl:
                    print("      onnx/%-40s %8.2f MB" % (f["name"], (f["size"] or 0) / 1048576))
            except Exception as e:
                res[key]["onnx_err"] = str(e)
                print("      onnx ERROR", e)
        for f in top:
            if (f["type"] or "").lower() not in ("tree", "dir", "folder"):
                print("      %-40s %8.2f MB" % (f["name"], (f["size"] or 0) / 1048576))
    except Exception as e:
        res[key] = {"error": "%s: %s" % (type(e).__name__, e)}
        print("== %-42s ERROR %s" % (key, e))

with open(OUT, "w", encoding="utf-8") as fh:
    json.dump(res, fh, ensure_ascii=False, indent=2)
print("WROTE", OUT)
