"""从 ModelScope 官方 API 拉候选重排模型的一手元信息（HF 直连不通，ModelScope 通）。

产出：
    tools/_reranker_survey/raw/<org>__<name>.json     原始返回
    tools/_reranker_survey/raw/<org>__<name>.md       ReadMeContent（官方模型卡）
    tools/_reranker_survey/summary.json               汇总表
"""
import json
import os
import time
import urllib.request

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_reranker_survey")
RAW = os.path.join(OUT, "raw")

CANDIDATES = [
    ("BAAI", "bge-reranker-base"),
    ("BAAI", "bge-reranker-large"),
    ("BAAI", "bge-reranker-v2-m3"),
    ("BAAI", "bge-reranker-v2-gemma"),
    ("Alibaba-NLP", "gte-multilingual-reranker-base"),
    ("jinaai", "jina-reranker-v2-base-multilingual"),
    ("Qwen", "Qwen3-Reranker-0.6B"),
    ("Qwen", "Qwen3-Reranker-4B"),
    ("IEITYuan", "Yuan-embedding-2.0-zh"),
    ("netease-youdao", "bce-reranker-base_v1"),
    ("maidalun1020", "bce-reranker-base_v1"),
    ("cross-encoder", "mmarco-mMiniLMv2-L12-H384-v1"),
    ("BAAI", "bge-reranker-v2-minicpm-layerwise"),
]


def fetch(org, name):
    url = "https://modelscope.cn/api/v1/models/%s/%s" % (org, name)
    req = urllib.request.Request(url, headers={"Accept": "application/json",
                                               "User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(req, timeout=30) as resp:
        return json.loads(resp.read().decode("utf-8"))


def main():
    os.makedirs(RAW, exist_ok=True)
    rows = []
    for org, name in CANDIDATES:
        try:
            d = fetch(org, name)
        except Exception as exc:
            print("MISS  %-42s %r" % (org + "/" + name, exc))
            continue
        data = d.get("Data") or {}
        if not data:
            print("EMPTY %-42s %s" % (org + "/" + name, d.get("Message")))
            continue

        key = "%s__%s" % (org, name)
        with open(os.path.join(RAW, key + ".json"), "w", encoding="utf-8") as fh:
            json.dump(d, fh, ensure_ascii=False, indent=2)
        readme = data.get("ReadMeContent") or ""
        with open(os.path.join(RAW, key + ".md"), "w", encoding="utf-8") as fh:
            fh.write(readme)

        row = {
            "id": org + "/" + name,
            "arch": (data.get("Architectures") or [None])[0],
            "license": data.get("License"),
            "storage_bytes": data.get("StorageSize"),
            "downloads": data.get("Downloads"),
            "created": data.get("CreatedTime"),
            "tasks": data.get("Tasks"),
            "readme_chars": len(readme),
        }
        rows.append(row)
        print("OK    %-42s arch=%-42s lic=%-12s storage=%s MB  readme=%d"
              % (row["id"], row["arch"], row["license"],
                 None if row["storage_bytes"] is None else row["storage_bytes"] // 1024 // 1024,
                 row["readme_chars"]))
        time.sleep(0.6)

    with open(os.path.join(OUT, "summary.json"), "w", encoding="utf-8") as fh:
        json.dump(rows, fh, ensure_ascii=False, indent=2)
    print()
    print("已写 summary.json（%d 条）" % len(rows))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
