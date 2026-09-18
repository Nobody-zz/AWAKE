# -*- coding: utf-8 -*-
"""抽查 should-link 边表：字段完整性 ＋ 分桶×方向分布 ＋ 抽样。可复用，不带日期。

用法：
    python tools/_should_link_sample.py                                   # 用 v2 默认路径
    python tools/_should_link_sample.py <json> [bucket] [mutual]
    python tools/_should_link_sample.py <json> proper true                # 只看专名互提边

为什么要有它：① 边表的字段是**契约**，缺字段下游会静默少算（不是抛错）；
② 抽样让人一眼看到"这条边凭什么"（viaName ＋ evidence），是**可以当场否掉**的。
"""
import io, json, sys, collections, os

DEFAULT = r"D:\AWAKE-Dev\AWAKE\docs\mappings\worldbook-should-link\20260918\should-link.v2.json"
REQUIRED = ["from", "to", "viaName", "strength", "bucket", "mutual", "direction", "usableAs", "evidence"]


def main(argv):
    path = argv[1] if len(argv) > 1 else DEFAULT
    if not os.path.exists(path):
        print("找不到：%s" % path)
        return 2
    d = json.load(io.open(path, encoding="utf-8"))
    edges = d["edges"]
    print("文件 %s" % os.path.basename(path))
    print("schema %s ；边 %d ；counts %s" % (d.get("schema_version"), len(edges),
                                          json.dumps(d.get("counts", {}), ensure_ascii=False)[:200]))

    # ---- ① 字段完整性 ----
    missing = collections.Counter()
    for e in edges:
        for k in REQUIRED:
            if k not in e or e[k] in (None, "", []):
                missing[k] += 1
    print()
    print("字段完整性：%s" % ("全部齐备" if not missing else "缺失 %s" % dict(missing)))
    bad_usable = [e for e in edges if e.get("mutual") and e.get("usableAs") != ["forward", "backward"]]
    bad_usable += [e for e in edges if not e.get("mutual") and e.get("usableAs") != ["forward"]]
    print("usableAs 与 mutual 一致：%s" % ("一致" if not bad_usable else "**不一致 %d 条**" % len(bad_usable)))

    # ---- ② 分桶 × 方向 ----
    print()
    print("分桶 × 方向：")
    for b in ("proper", "hubproper", "star"):
        sub = [e for e in edges if e["bucket"] == b]
        mu = len([e for e in sub if e["mutual"]])
        print("  %-10s 边 %4d   互提 %3d   单向 %3d" % (b, len(sub), mu, len(sub) - mu))

    # ---- ③ 抽样 ----
    flt = edges
    if len(argv) > 2:
        flt = [e for e in flt if e["bucket"] == argv[2]]
    if len(argv) > 3:
        want = argv[3].lower() in ("1", "true", "yes")
        flt = [e for e in flt if e["mutual"] == want]
    print()
    print("抽样（%d 条候选，打印前 6）：" % len(flt))
    for e in flt[:6]:
        print("  %s → %s" % (e["from"].split(":")[-1], e["to"].split(":")[-1]))
        print("      靠「%s」 DF=%s %s %s %s" % (e["viaName"], e.get("df"), e["bucket"],
                                              e["direction"], e["usableAs"]))
        for s in e["evidence"][:1]:
            print("      原文：%s" % s[:78])
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
