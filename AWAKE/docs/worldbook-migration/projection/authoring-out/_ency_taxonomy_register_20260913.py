# -*- coding: utf-8 -*-
"""宪章 §二 兑现：taxonomy economy 域登记 items（器物）/ goods（物产）subdomain。
JSON 结构保持原格式；改前原值备份到 corrections_20260913/knowledge-taxonomy.v1.json.md。
"""
import io, json, os, shutil

P = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-studio-plan/knowledge-taxonomy.v1.json"
BAK = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/corrections_20260913/knowledge-taxonomy.v1.json.md"

raw = io.open(P, encoding="utf-8").read()
d = json.loads(raw)
eco = [x for x in d["domains"] if x["id"] == "economy"][0]
existing = {s["id"] for s in eco["subdomains"]}
assert "items" not in existing and "goods" not in existing, "已登记过"

os.makedirs(os.path.dirname(BAK), exist_ok=True)
with io.open(BAK, "w", encoding="utf-8") as f:
    f.write("# knowledge-taxonomy.v1.json economy 域扩容留痕（2026-09-13，百科化宪章 §二）\n\n")
    f.write("原 economy.subdomains ids：\n\n```\n" + ", ".join(sorted(existing)) + "\n```\n")
    f.write("\n新增：items（器物）、goods（物产）。依据：WORLDBOOK-ENCYCLOPEDIA-CHARTER-20260913.md §二。\n")

eco["subdomains"].extend([
    {
        "id": "items",
        "label": {"zh-CN": "器物"},
        "help": {"zh-CN": "器物、装备、珍宝的来历、归属与流转。"},
        "examples": {"zh-CN": ["这把剑是谁的", "城里能买到什么好东西"]}
    },
    {
        "id": "goods",
        "label": {"zh-CN": "物产"},
        "help": {"zh-CN": "物产、特产、原料的产地与贸易。"},
        "examples": {"zh-CN": ["这里出产什么", "皮毛从哪来"]}
    },
])

io.open(P, "w", encoding="utf-8").write(json.dumps(d, ensure_ascii=False, indent=2) + "\n")
print("taxonomy 扩容完成：economy += items, goods；留痕:", BAK)
