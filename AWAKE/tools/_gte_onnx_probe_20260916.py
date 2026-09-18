# -*- coding: utf-8 -*-
"""查 gte-zh / piccolo / stella 等中文嵌入候选的 onnx 可得性。"""
import json, urllib.request, os

TARGETS = [
    ("thenlper", "gte-small-zh"),
    ("thenlper", "gte-base-zh"),
    ("thenlper", "gte-large-zh"),
    ("Xenova", "gte-small-zh"),
    ("Xenova", "gte-base-zh"),
    ("AI-ModelScope", "gte-small-zh"),
    ("AI-ModelScope", "gte-base-zh"),
    ("iic", "nlp_gte_sentence-embedding_chinese-small"),
    ("iic", "nlp_gte_sentence-embedding_chinese-base"),
    ("sensenova", "piccolo-base-zh"),
    ("infgrad", "stella-base-zh-v2"),
]

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_reranker_survey", "gte_onnx.json")


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
        else:
            for f in top:
                if (f["type"] or "").lower() not in ("tree", "dir", "folder"):
                    print("      %-40s %8.2f MB" % (f["name"], (f["size"] or 0) / 1048576))
        res[key] = entry
    except Exception as e:
        res[key] = {"error": "%s: %s" % (type(e).__name__, e)}
        print("== %-46s ERROR %s %s" % (key, type(e).__name__, e))

with open(OUT, "w", encoding="utf-8") as fh:
    json.dump(res, fh, ensure_ascii=False, indent=2)
print("WROTE", OUT)
