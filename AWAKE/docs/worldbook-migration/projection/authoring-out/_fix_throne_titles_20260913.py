# -*- coding: utf-8 -*-
"""politics/throne 两档 title 补副题（体现内容，对齐同域「实体·主题」命名法）。

- throne-saneopa：帝国旧都（东迁定都三百余年、迁都吕卡隆后迁回呼声未息）＋涅雷采斯家根据地
  → 「萨涅俄帕·旧都与迁回」
- throne-paravenos：巴拉维诺斯旧帝都＋铁臂奥斯里克献城归戴·提尔家（改今名）
  → 「帕拉汶德·旧都与献城」

只改 title.zh-CN；id/文件名/aliases（含「旧都」等检索词）不动。双写。
"""
import io, os, yaml, json

OUT = os.path.dirname(os.path.abspath(__file__))
WS = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring"

JOBS = [
    ("throne-saneopa.yaml", "萨涅俄帕·隘口商埠与旧都"),
    ("throne-paravenos.yaml", "帕拉汶德·旧帝都的沦陷"),
]


class NoAlias(yaml.dumper.Dumper):
    def ignore_aliases(self, data):
        return True


def main():
    for fn, tzh in JOBS:
        for base, tag in ((OUT, "out"), (WS, "ws")):
            p = os.path.join(base, fn)
            if not os.path.exists(p):
                print("MISS", p)
                continue
            doc = yaml.safe_load(io.open(p, encoding="utf-8"))
            old = (doc.get("title") or {}).get("zh-CN")
            doc["title"]["zh-CN"] = tzh
            doc = json.loads(json.dumps(doc))
            text = yaml.dump(doc, Dumper=NoAlias, allow_unicode=True, sort_keys=False,
                             default_flow_style=False, width=100)
            assert "&id" not in text and "*id" not in text
            io.open(p, "w", encoding="utf-8", newline="\n").write(text)
            print("OK", tag, fn, f"{old} -> {tzh}")
    print("done")


if __name__ == "__main__":
    main()
