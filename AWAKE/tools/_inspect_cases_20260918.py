import json, os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
p = os.path.join(ROOT, "tools", "_retrieval_cases_20260916.json")
d = json.load(open(p, encoding="utf-8"))

print("top type:", type(d).__name__)
if isinstance(d, dict):
    print("keys:", list(d.keys()))
    for k, v in d.items():
        if isinstance(v, list):
            print("  list key:", k, "len", len(v))
            for item in v[:2]:
                print("   -", json.dumps(item, ensure_ascii=False)[:500])
            break
else:
    print("len", len(d))
    for item in d[:3]:
        print(" -", json.dumps(item, ensure_ascii=False)[:500])
