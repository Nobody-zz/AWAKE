# -*- coding: utf-8 -*-
"""补检索面：5 档 title 去副题后，把原副题全文与拆分词补进 aliases.zh-CN。

原因：compiler 的 Keywords = title + aliases + anchor + sourceId；
早期 4 城镇档原 title「名·副题」是检索词来源（矩阵 A1/A2/A8/A10 回归探针即以「城与港」「石山要塞」检索）。
title 已按新批规范改为纯名，副题须转存 aliases 以保检索面（项目纪律：检索全靠 aliases）。
双写 authoring-out ＋ workspace。
"""
import io, os, yaml, json

OUT = os.path.dirname(os.path.abspath(__file__))
WS = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring"

# (file, 追加的 zh-CN 检索词)
JOBS = [
    ("town-charas.yaml", ["沙拉斯·城与港", "城与港"]),
    ("town-husn-fulq.yaml", ["侯森·富勒格·卡尔得亚边上的城", "卡尔得亚边上的城"]),
    ("town-lycaron.yaml", ["吕卡隆·石山要塞", "石山要塞"]),
    ("town-varcheg.yaml", ["瓦尔切格·海崖与港", "海崖与港"]),
    # 2026-09-17 撤：这条复合别名覆盖只有 1、绕过索引卫生 R1，成了「村庄」在索引里的
    # 唯一主人，把 272 条村庄的泛问全劫走（见 docs/FIT-20260917-识别链路与条目的契合度.md §三）。
    # 「村庄」已升格为概念词条 doc.geography.settlement-types-village，此处不再补。
    # ("village-deriat.yaml", ["德里亚特·村庄"]),
]


class NoAlias(yaml.dumper.Dumper):
    def ignore_aliases(self, data):
        return True


def main():
    for fn, extra in JOBS:
        for base, tag in ((OUT, "out"), (WS, "ws")):
            p = os.path.join(base, fn)
            if not os.path.exists(p):
                print("MISS", p)
                continue
            doc = yaml.safe_load(io.open(p, encoding="utf-8"))
            al = doc.setdefault("aliases", {})
            zh = al.setdefault("zh-CN", [])
            added = []
            for w in extra:
                if w not in zh:
                    zh.append(w)
                    added.append(w)
            doc = json.loads(json.dumps(doc))
            text = yaml.dump(doc, Dumper=NoAlias, allow_unicode=True, sort_keys=False,
                             default_flow_style=False, width=100)
            assert "&id" not in text and "*id" not in text
            io.open(p, "w", encoding="utf-8", newline="\n").write(text)
            print("OK", tag, fn, "added", added)
    print("done")


if __name__ == "__main__":
    main()
