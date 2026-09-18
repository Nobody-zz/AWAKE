# -*- coding: utf-8 -*-
"""查各重排器候选在 ModelScope 上是否有 ONNX 导出（决定能否直接复用现有 ORT 通道）。"""
import json, sys, urllib.request, urllib.error, os

IDS = [
    "BAAI/bge-reranker-base",
    "BAAI/bge-reranker-large",
    "BAAI/bge-reranker-v2-m3",
    "BAAI/bge-reranker-v2-gemma",
    "BAAI/bge-reranker-v2-minicpm-layerwise",
    "Qwen/Qwen3-Reranker-0.6B",
    "Qwen/Qwen3-Reranker-4B",
    "jinaai/jina-reranker-v2-base-multilingual",
    "netease-youdao/bce-reranker-base_v1",
    "cross-encoder/mmarco-mMiniLMv2-L12-H384-v1",
    "IEITYuan/Yuan-embedding-2.0-zh",
]

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_reranker_survey", "onnx_avail.json")


def get(url):
    req = urllib.request.Request(url, headers={
        "User-Agent": "Mozilla/5.0",
        "Accept": "application/json",
    })
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.loads(r.read().decode("utf-8", "replace"))


def list_files(mid):
    org, name = mid.split("/", 1)
    # ModelScope repo files API
    cands = [
        "https://modelscope.cn/api/v1/models/%s/%s/repo/files?Revision=master&Root=" % (org, name),
        "https://modelscope.cn/api/v1/models/%s/%s/repo/tree?Revision=master" % (org, name),
    ]
    for u in cands:
        try:
            d = get(u)
            return d
        except urllib.error.HTTPError as e:
            last = "http=%d %s" % (e.code, u)
        except Exception as e:
            last = "%s %s" % (type(e).__name__, u)
    return {"__error__": last}


def flatten(node, prefix="", acc=None):
    """递归收集 {path, size}。"""
    if acc is None:
        acc = []
    if not isinstance(node, dict):
        return acc
    files = node.get("Files") or node.get("files")
    if isinstance(files, list):
        for f in files:
            if isinstance(f, dict):
                nm = f.get("Name") or f.get("name") or ""
                p = prefix + "/" + nm if prefix else nm
                acc.append({"path": p, "size": f.get("Size") or f.get("size") or 0,
                            "type": f.get("Type") or f.get("type")})
    subs = node.get("SubModels") or node.get("subModels") or node.get("children")
    if isinstance(subs, list):
        for s in subs:
            if isinstance(s, dict):
                nm = s.get("Name") or s.get("name") or ""
                p = prefix + "/" + nm if prefix else nm
                flatten(s, p, acc)
    return acc


result = {}
for mid in IDS:
    d = list_files(mid)
    if "__error__" in d:
        result[mid] = {"error": d["__error__"]}
        print("%-52s ERROR %s" % (mid, d["__error__"]))
        continue
    body = d.get("Data") or d
    files = flatten(body if isinstance(body, dict) else {})
    onnx = [f for f in files if ".onnx" in f["path"].lower()]
    result[mid] = {
        "n_files": len(files),
        "onnx": onnx,
        "has_onnx": len(onnx) > 0,
        "all_paths_sample": [f["path"] for f in files][:40],
    }
    print("%-52s files=%-5d onnx=%-3d %s" % (
        mid, len(files), len(onnx),
        (" | ".join("%s(%.1fMB)" % (f["path"], (f["size"] or 0) / 1048576) for f in onnx))[:120]))

with open(OUT, "w", encoding="utf-8") as fh:
    json.dump(result, fh, ensure_ascii=False, indent=2)
print("WROTE", OUT)
