# -*- coding: utf-8 -*-
"""地理档命名规范化：后缀式 <slug>-town / deriat-village → 前缀式 town-<slug> / village-<slug>。

对齐新批（49 档 town-*）写法：
  - id：doc.geography.town-<slug> / doc.geography.village-<slug>
  - 文件名：town-<slug>.yaml / village-<slug>.yaml
  - title.zh-CN：纯名（副题不迁移）
  - 档内 assertion./expr. 前缀同步为 town-<slug> / village-<slug>
  - 字段顺序与 block 风格对齐新批（der-vill 原为 flow 风格）
保留：entity_ids、既有 sources/assertions 全部内容（只规范化形式，不删素材）。
双写：authoring-out ＋ workspace/authoring；旧文件移除。
"""
import io, os, yaml, json

OUT = os.path.dirname(os.path.abspath(__file__))
WS = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring"

TOP_ORDER = ["schema_version", "revision", "id", "title", "status", "domain", "subdomain",
             "universe", "era", "content_tier", "aliases", "entity_ids", "summary",
             "registry_bindings", "sources", "authority", "assertions"]

# (old_file, new_file, old_id, new_id, old_prefix, new_slug_prefix, new_title_zh)
JOBS = [
    ("charas-town.yaml", "town-charas.yaml", "doc.geography.charas-town",
     "doc.geography.town-charas", "charas-town", "town-charas", "沙拉斯"),
    ("husn-fulq-town.yaml", "town-husn-fulq.yaml", "doc.geography.husn-fulq-town",
     "doc.geography.town-husn-fulq", "husn-fulq-town", "town-husn-fulq", "侯森·富勒格"),
    ("lycaron-town.yaml", "town-lycaron.yaml", "doc.geography.lycaron-town",
     "doc.geography.town-lycaron", "lycaron-town", "town-lycaron", "吕卡隆"),
    ("varcheg-town.yaml", "town-varcheg.yaml", "doc.geography.varcheg-town",
     "doc.geography.town-varcheg", "varcheg-town", "town-varcheg", "瓦尔切格"),
    ("der-vill.yaml", "village-deriat.yaml", "doc.geography.deriat-village",
     "doc.geography.village-deriat", "der-vill", "village-deriat", "德里亚特"),
]


class NoAlias(yaml.dumper.Dumper):
    def ignore_aliases(self, data):
        return True


def reorder(d):
    nd = {}
    for k in TOP_ORDER:
        if k in d:
            nd[k] = d[k]
    for k, v in d.items():
        if k not in nd:
            nd[k] = v
    return nd


def fix(doc, new_id, old_pfx, new_pfx, title_zh):
    doc["id"] = new_id
    doc["title"]["zh-CN"] = title_zh
    oa, na = f"assertion.{old_pfx}", f"assertion.{new_pfx}"
    oe, ne = f"expr.{old_pfx}", f"expr.{new_pfx}"
    for a in doc.get("assertions", []):
        if str(a.get("id", "")).startswith(oa):
            a["id"] = na + a["id"][len(oa):]
        for e in a.get("expressions", []):
            if str(e.get("id", "")).startswith(oe):
                e["id"] = ne + e["id"][len(oe):]
    return reorder(doc)


def main():
    for old_f, new_f, old_id, new_id, old_pfx, new_pfx, tzh in JOBS:
        for base, tag in ((OUT, "out"), (WS, "ws")):
            p = os.path.join(base, old_f)
            if not os.path.exists(p):
                print("MISS", p)
                continue
            doc = yaml.safe_load(io.open(p, encoding="utf-8"))
            assert doc["id"] == old_id, (p, doc["id"])
            doc = fix(doc, new_id, old_pfx, new_pfx, tzh)
            doc = json.loads(json.dumps(doc))  # 断共享引用
            text = yaml.dump(doc, Dumper=NoAlias, allow_unicode=True, sort_keys=False,
                             default_flow_style=False, width=100)
            assert "&id" not in text and "*id" not in text
            io.open(os.path.join(base, new_f), "w", encoding="utf-8", newline="\n").write(text)
            os.remove(p)
            print("OK", tag, old_f, "->", new_f)
    print("done")


if __name__ == "__main__":
    main()
