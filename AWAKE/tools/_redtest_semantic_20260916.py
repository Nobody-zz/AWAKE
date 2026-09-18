# -*- coding: utf-8 -*-
"""红测 v2 · 把同一批对抗样本打到「语义通道」上：量它的漏报面、过匹配面，以及**闸的形状**。

前置：`_redtest_kw_report.md`（09-16）已把靶打在**字面层**上 —— 48 条 RED 30 / OK 18。
本探针**不重复**那个结论，只回答它回答不了的那一个问题：

    语义通道接上以后，"换个说法也能听懂"能变多多少，代价是"乱答"涨多少，
    以及 —— 能不能靠**一个阈值**把两边分开？

方法学（写在最前，便于挑刺）
- 样本：**完全复用**字面红测那 48 条（`_redtest_kw_spec.json`），两侧可比；不另造样本。
- 语料：与上线包同一份 `ModuleData/Worldbook/packages/calradia/runtime.json`（448 条）。
- 模型：本机现成 AnimusForge 版 bge-small-zh-v1.5（ONNX），**零下载**；查询侧按官方用法加指令前缀。
- 文档侧：`title(zh) + keywords + summary` 拼接（与 `_bge_probe_20260916.py` 同一口径）。
- 字面侧**不自己复刻**：直接读真代码跑出来的 `_redtest_kw_result.json`（避免"平行实现不同源"）。
- 阳性对照：同句自比余弦须 = 1.0；无关句最高分须明显偏低。

判据（语义通道与字面通道的**结构差别**就在这）
- 字面通道："命中 / 没命中"是二元的 —— 没命中就不开口。
- 语义通道：**永远有 top-N** —— 所以问题变成"它会不会开口"。故 OVER-RISK 的判据是
  `top1 分数 >= 阈值 ⇒ 会开口 ⇒ RED`。**这个阈值就是"闸"。**
- 核心问题：存不存在一个阈值同时过两边？⇒ 扫阈值，并给出"无解"的**硬判据**：
  `max(OVER-RISK 的 top1) >= min(MISS-RISK 的 top1)` ⇒ 单一阈值无解。
- 附带量一个**相对闸**：`margin = top1 - top5`（泛词跟谁都"平均地像" ⇒ margin 小）。

运行：
  "C:/Users/26811/.workbuddy/binaries/python/envs/default/Scripts/python.exe" -u AWAKE/tools/_redtest_semantic_20260916.py
"""
import io
import json
import os
import re
import time
from collections import defaultdict

import numpy as np
import onnxruntime as ort
from tokenizers import Tokenizer

ROOT = r"D:/AWAKE-Dev/AWAKE"
AF = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/AnimusForge/ONNX"
CASE = os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1")
PKG = os.path.join(CASE, "compiled/geo1-v9/runtime.json")   # ★ 必须与字面红测同包（v9 = 458 档）
# 注意：不要换成 ModuleData/Worldbook/packages/calradia/runtime.json —— 那份只有 448 条，
# 缺 5 条头盔条目（正是本轮红测的靶），拿它跑 = 判据目标全落空、结果不可比。
OUT_JSON = os.path.join(CASE, "_redtest_semantic_result.json")
OUT_MD = os.path.join(CASE, "_redtest_semantic_report.md")
INSTRUCTION = "为这个句子生成表示以用于检索相关文章："

NEGATIVE = "今天午饭吃什么"
TOPKS = 5

# ---- 「不值得救」的分区（09-16 22:0x 甲方口径，原话：拼音缩写过不了很正常，只能怪玩家自己不好好说话）----
# ⇒ 语义通道的**义务面**要摘掉这两类：① 拼音首字母（不是"名字"，是偷懒代号）；
#   ② 纯拉丁／英文写法（甲方："英文名过了还能理解"＝由字面通道负责，不要求语义层救）。
# 摘掉它们之后再看重叠 —— 那部分重叠属于**假重叠**：拿"不配救的"去跟"该挡的"比，本来就不该比。
EXEMPT = {"C3", "C4", "D1", "D2", "D3", "D4"}
# 争议区：截断／倒装／单字／半截词 —— 算"手滑"还是"不好好说"，尚无口径，单列不并入。
DISPUTED = {"C1", "C2", "C5", "C6"}


def zh_title(e):
    t = e.get("title") or {}
    return t.get("zh-CN") or (list(t.values())[0] if t else "")


def zh_summary(e):
    return (e.get("summary") or {}).get("zh-CN", "")


def doc_text(e):
    return "。".join(x for x in (zh_title(e), "、".join(e.get("keywords") or []), zh_summary(e)) if x)


def main():
    t0 = time.time()
    entries = json.load(io.open(PKG, encoding="utf-8"))["entries"]
    spec = json.load(io.open(os.path.join(CASE, "_redtest_kw_spec.json"), encoding="utf-8"))["queries"]
    expect = json.load(io.open(os.path.join(CASE, "_redtest_kw_expect.json"), encoding="utf-8"))
    lexres = json.load(io.open(os.path.join(CASE, "_redtest_kw_result.json"), encoding="utf-8"))
    by_no = {e["no"]: e for e in expect}
    lex_by_no = {re.match(r"KW([A-Z]\d+)", r["name"]).group(1): r for r in lexres}

    print("语料 %d 条 / 样本 %d 条" % (len(entries), len(spec)))
    all_ids = {e["id"] for e in entries}
    missing = sorted({t for e in expect for t in e["targets"]} - all_ids)
    print("判据目标缺失：%s" % (missing or "无"))
    assert not missing, "语料包与判据不同源 —— 换回 compiled/geo1-v9/runtime.json"

    tok = Tokenizer.from_file(os.path.join(AF, "tokenizer.json"))
    tok.enable_truncation(max_length=512)
    tok.enable_padding(pad_id=0, pad_token="[PAD]")
    so = ort.SessionOptions()
    so.log_severity_level = 3
    sess = ort.InferenceSession(os.path.join(AF, "model.onnx"), so, providers=["CPUExecutionProvider"])

    def encode(texts):
        encs = tok.encode_batch(texts)
        ids = np.array([e.ids for e in encs], dtype=np.int64)
        mask = np.array([e.attention_mask for e in encs], dtype=np.int64)
        feed = {"input_ids": ids, "attention_mask": mask, "token_type_ids": np.zeros_like(ids)}
        return sess.run(["sentence_embedding"], feed)[0].astype(np.float32)

    # ---- 阳性对照 ----
    probe = encode(["帕拉汶德", "帕拉汶德"])
    n0 = np.linalg.norm(probe, axis=1)
    self_cos = float(probe[0] @ probe[1] / (n0[0] * n0[1] + 1e-9))
    print("阳性对照：同句自比余弦 = %.6f（须 = 1.0）" % self_cos)
    assert abs(self_cos - 1.0) < 1e-3, "自比不等于 1 ⇒ 编码链路有问题，后面的数不可信"

    t = time.time()
    D = encode([doc_text(e) for e in entries])
    Dn = D / (np.linalg.norm(D, axis=1, keepdims=True) + 1e-9)
    print("文档编码 %d 条 %.1f s" % (len(entries), time.time() - t))

    qtexts = [INSTRUCTION + q["text"] for q in spec]
    Q = encode(qtexts)
    Qn = Q / (np.linalg.norm(Q, axis=1, keepdims=True) + 1e-9)
    S = Qn @ Dn.T                       # (48, 448)
    order = np.argsort(-S, axis=1)[:, :TOPKS]

    negv = encode([INSTRUCTION + NEGATIVE])[0]
    negv = negv / (np.linalg.norm(negv) + 1e-9)
    neg_top = float((Dn @ negv).max())
    print("阳性对照：无关句『%s』在 %d 条上的最高相似度 = %.4f" % (NEGATIVE, len(entries), neg_top))

    idx_of = {e["id"]: i for i, e in enumerate(entries)}

    rows = []
    for k, q in enumerate(spec):
        no = re.match(r"KW([A-Z]\d+)", q["name"]).group(1)
        e = by_no[no]
        sc = S[k]
        top = order[k]
        top1 = float(sc[top[0]])
        margin = top1 - float(sc[top[4]])
        top_ids = [entries[int(i)]["id"] for i in top]
        targets = set(e["targets"])
        sem_hit3 = bool(targets & set(top_ids[:3]))
        sem_hit1 = top_ids[0] in targets
        lex = lex_by_no[no]
        lex_hits = set(lex.get("hits") or [])
        lex_ok = bool(lex_hits & targets) if e["cat"] == "MISS-RISK" else (not lex_hits)
        rows.append({
            "no": no, "group": e["group"], "cat": e["cat"], "text": e["text"],
            "targets": e["targets"], "note": e["note"],
            "sem_top1": round(top1, 4), "sem_margin": round(margin, 4),
            "sem_top5": [{"id": entries[int(i)]["id"], "sim": round(float(sc[int(i)]), 4)} for i in top],
            "sem_hit3": sem_hit3, "sem_hit1": sem_hit1,
            "sem_verdict": ("OK" if sem_hit3 else "RED") if e["cat"] == "MISS-RISK" else None,
            "lex_hits_n": len(lex_hits), "lex_ok": lex_ok,
            "lex_state": lex.get("state"),
            # 并集：字面命中 或 语义 top3 命中
            "union_hit": (lex_ok or sem_hit3) if e["cat"] == "MISS-RISK" else None,
        })

    # ================= 统计 =================
    print()
    print("=" * 84)
    print("A. 语义通道的漏报面（MISS-RISK）")
    print("=" * 84)
    miss = [r for r in rows if r["cat"] == "MISS-RISK"]
    over = [r for r in rows if r["cat"] == "OVER-RISK"]
    g = defaultdict(lambda: [0, 0, 0])   # group -> [总, 字面OK, 语义OK]
    for r in miss:
        g[r["group"]][0] += 1
        g[r["group"]][1] += 1 if r["lex_ok"] else 0
        g[r["group"]][2] += 1 if r["sem_hit3"] else 0
    print("%-8s %-8s %-8s %-8s" % ("分组", "总", "字面OK", "语义OK"))
    for k in sorted(g):
        print("%-8s %-8d %-8d %-8d" % (k, g[k][0], g[k][1], g[k][2]))
    print("%-8s %-8d %-8d %-8d" % ("合计", len(miss),
                                   sum(1 for r in miss if r["lex_ok"]),
                                   sum(1 for r in miss if r["sem_hit3"])))
    print("并集（字面 或 语义）合计 OK = %d/%d" % (sum(1 for r in miss if r["union_hit"]), len(miss)))

    print()
    print("=" * 84)
    print("B. ★ 闸：语义通道永远有 top-N ⇒ 必须有人决定“开不开口”。两边能不能用一个阈值分开？")
    print("=" * 84)
    miss_min = min(r["sem_top1"] for r in miss)
    miss_arg = [r for r in miss if r["sem_top1"] == miss_min][0]
    over_max = max(r["sem_top1"] for r in over)
    over_arg = [r for r in over if r["sem_top1"] == over_max][0]
    print("MISS-RISK（该开口的）top1 最低 = %.4f  ← 『%s』" % (miss_min, miss_arg["text"]))
    print("OVER-RISK（不该开口的）top1 最高 = %.4f  ← 『%s』(%s)" % (
        over_max, over_arg["text"] or "(空)", over_arg["group"]))
    separable = over_max < miss_min
    print("⇒ 单一绝对阈值可分？ %s" % ("是" if separable else "**否（两类重叠）**"))
    if not separable:
        print("   重叠区 [%.4f, %.4f]：阈值取在这里，两边都过不了。" % (over_max, miss_min))
        print("   该开口但会被闸挡住的 = %d 条：" % sum(1 for r in miss if r["sem_top1"] < over_max))
        for r in miss:
            if r["sem_top1"] < over_max:
                print("      %-4s %-22r top1=%.4f  目标 %s" % (
                    r["no"], r["text"][:20], r["sem_top1"],
                    ",".join(x.split(":")[-1] for x in r["targets"])))

    print()
    print("--- 阈值扫描（th -> 救回 MISS / 压住 OVER）---")
    print("%-7s %-12s %-12s" % ("th", "救回(该开口)", "压住(不该开口)"))
    scan = []
    for th in [round(0.30 + 0.02 * i, 2) for i in range(0, 26)]:
        a = sum(1 for r in miss if r["sem_hit3"] and r["sem_top1"] >= th)
        b = sum(1 for r in over if r["sem_top1"] < th)
        scan.append({"th": th, "rescued": a, "suppressed": b, "n_miss": len(miss), "n_over": len(over)})
        if 0.40 <= th <= 0.72:
            print("%-7.2f %-12s %-12s" % (th, "%d/%d" % (a, len(miss)), "%d/%d" % (b, len(over))))
    best = [x for x in scan if x["rescued"] + x["suppressed"] == max(
        y["rescued"] + y["suppressed"] for y in scan)]
    print("两边相加最优：th=%.2f → 救回 %d + 压住 %d = %d/%d（理论满 = %d）"
          % (best[0]["th"], best[0]["rescued"], best[0]["suppressed"],
             best[0]["rescued"] + best[0]["suppressed"], len(miss) + len(over),
             len(miss) + len(over)))

    print()
    print("=" * 84)
    print("C. 两条并列召回 + 闸 —— 最终形态的两个数（救回多少 / 添乱多少）")
    print("=" * 84)
    lex_ok_miss = sum(1 for r in miss if r["lex_ok"])
    sem1 = sum(1 for r in miss if r["sem_hit1"])
    sem3 = sum(1 for r in miss if r["sem_hit3"])
    over_lex_noise = sum(1 for r in over if not r["lex_ok"])
    print("（无闸）字面 %d/35 ／ 语义 top1 直答 %d/35 ／ 语义 top3 含答案 %d/35 ／ 并集(top3) %d/35"
          % (lex_ok_miss, sem1, sem3, sum(1 for r in miss if r["union_hit"])))
    print("（无闸）乱答：字面侧自身 = %d/13" % over_lex_noise)
    print()
    print("%-7s %-18s %-18s" % ("th", "救回(并集, top1口径)", "乱答(字面既有 或 语义开口)"))
    gate = []
    for th in [round(0.30 + 0.02 * i, 2) for i in range(0, 26)]:
        rescued = sum(1 for r in miss
                      if r["lex_ok"] or (r["sem_hit1"] and r["sem_top1"] >= th))
        noisy = sum(1 for r in over
                    if (not r["lex_ok"]) or r["sem_top1"] >= th)
        gate.append({"th": th, "rescued": rescued, "noisy": noisy})
        if 0.40 <= th <= 0.68:
            print("%-7.2f %-18s %-18s" % (th, "%d/35" % rescued, "%d/13" % noisy))

    print()
    print("--- 相对闸（margin = top1 - top5）---")
    m_miss = sorted(r["sem_margin"] for r in miss)
    m_over = sorted(r["sem_margin"] for r in over)
    print("MISS-RISK margin 最低 = %.4f ／ OVER-RISK margin 最高 = %.4f" % (m_miss[0], m_over[-1]))
    print("⇒ margin 可分？ %s" % ("是" if m_over[-1] < m_miss[0] else "**否**"))

    print()
    print("--- 过匹配 token 在语义通道下具体什么样 ---")
    for r in over:
        top1 = r["sem_top5"][0]
        print("%-4s %-14r top1=%.4f (%s)  targets=%s"
              % (r["no"], r["text"][:12], r["sem_top1"], top1["id"].split(":")[-1],
                 ",".join(x.split(":")[-1] for x in r["targets"]) or "(不该命中)"))

    # ================= D. 对照：绝对分被什么带着走 =================
    # 上面 13 条 OVER-RISK 是**为字面层设计的**（短词/内部 id），不能用来量语义层的过匹配面。
    # 这里补三类**为语义层设计**的探测，且都是硬事实、不是判断题：
    #   长无关句（长度伪装）／泛类词（语义层的天然过匹配源）／准确短词（下限对照）
    EXTRA = [
        ("长无关-1", "今天天气不错啊，我来看看你们这儿最近过得怎么样，路上还顺利吗"),
        ("长无关-2", "我听人说北边最近不太平，你觉得是不是该多准备点人手，免得到时候手忙脚乱"),
        ("长无关-3", "这个我记不太清了，你再仔细跟我说一遍，我有点没听明白你刚才在讲什么"),
        ("长无关-4", "嗯，让我想想。你说的那个东西我好像在什么地方见过，可一时半会儿想不起来"),
        ("泛类词-a", "武器"),
        ("泛类词-b", "村子"),
        ("泛类词-c", "城堡"),
        ("泛类词-d", "多少钱"),
        ("准确短词-a", "锅盔"),
        ("准确短词-b", "护鼻盔"),
        ("同名-富勒格", "富勒格是谁？"),
        ("无关短句(标尺)", NEGATIVE),
    ]
    X = encode([INSTRUCTION + t for _, t in EXTRA])
    Xn = X / (np.linalg.norm(X, axis=1, keepdims=True) + 1e-9)
    XS = Xn @ Dn.T
    xo = np.argsort(-XS, axis=1)[:, :3]
    print()
    print("=" * 84)
    print("D. 绝对分被什么带着走（对照：无关句『%s』= %.4f）" % (NEGATIVE, neg_top))
    print("=" * 84)
    extra_rows = []
    for i, (nm, tx) in enumerate(EXTRA):
        t1 = float(XS[i][xo[i][0]])
        ids = [entries[int(j)]["id"] for j in xo[i]]
        extra_rows.append({"name": nm, "text": tx, "top1": round(t1, 4),
                           "top3": [{"id": x, "sim": round(float(XS[i][idx_of[x]]), 4)} for x in ids]})
        print("%-14s top1=%.4f  %s" % (nm, t1, " / ".join(x.split(":")[-1] for x in ids)))
    long_avg = np.mean([r["top1"] for r in extra_rows if r["name"].startswith("长无关")])
    print("长无关句平均 top1 = %.4f（无关短句 = %.4f ⇒ 只把话说长，分就抬起来了 %.4f）"
          % (long_avg, neg_top, long_avg - neg_top))

    # ================= E. 摘掉「不值得救」的那类之后：假重叠 vs 真重叠 =================
    over_all = [(r["sem_top1"], "该挡 " + r["no"] + " 「" + r["text"] + "」") for r in over]
    over_all += [(r["top1"], "该挡 泛称「" + r["text"] + "」")
                 for r in extra_rows if r["name"].startswith("泛类词")]
    over_max_all = max(s for s, _ in over_all)
    over_arg_all = [t for s, t in over_all if s == over_max_all][0]
    req = [r for r in miss if r["no"] not in EXEMPT]
    exm = [r for r in miss if r["no"] in EXEMPT]
    dis = [r for r in miss if r["no"] in DISPUTED]
    req_min = min(r["sem_top1"] for r in req)
    req_arg = [r for r in req if r["sem_top1"] == req_min][0]
    print()
    print("=" * 84)
    print("E. 摘掉「不值得救」的之后：假重叠 vs 真重叠")
    print("=" * 84)
    print("该救面 %d → %d 条（摘掉拼音首字母＋拉丁写法：%s）"
          % (len(miss), len(req), "／".join(sorted(EXEMPT))))
    print("  其中争议区（截断／倒装／单字／半截词）%d 条：%s"
          % (len(dis), "／".join(sorted(DISPUTED))))
    print("假重叠（不值得救的 vs 该挡的，本来就不该比）：%.4f–%.4f，落进该挡面 %.4f–%.4f"
          % (min(r["sem_top1"] for r in exm), max(r["sem_top1"] for r in exm),
             min(s for s, _ in over_all), over_max_all))
    print("真重叠（值得救的 vs 该挡的）：该救最低 %.4f ← 『%s』；该挡最高 %.4f ← %s"
          % (req_min, req_arg["text"], over_max_all, over_arg_all))
    fan = [s for s, t in over_all if "泛称" in t]
    fan_min = min(fan)
    low = sorted([r for r in req if r["sem_top1"] < over_max_all], key=lambda x: x["sem_top1"])
    print("该救面里低于『最高该挡分 %.4f』的 = %d 条（这才是单阈值挡不住的部分）：" % (over_max_all, len(low)))
    for r in low:
        print("   %-4s %-16r %.4f  ｜ 与最低泛称（%.4f）比：%s"
              % (r["no"], r["text"][:14], r["sem_top1"], fan_min,
                 "更低" if r["sem_top1"] < fan_min else "更高"))

    items = [(r["sem_top1"], 1, r["no"] + " " + r["text"]) for r in req] + [(s, 0, t) for s, t in over_all]
    items.sort(key=lambda x: -x[0])
    best_cut = None
    for i in range(len(items) + 1):
        hi = items[i - 1][0] if i else items[0][0] + 0.001
        lo = items[i][0] if i < len(items) else 0.0
        wrong_open = [x for x in items[:i] if x[1] == 0]
        wrong_shut = [x for x in items[i:] if x[1] == 1]
        score = len(items) - len(wrong_open) - len(wrong_shut)
        if best_cut is None or score > best_cut[0]:
            best_cut = (score, (hi + lo) / 2, wrong_open, wrong_shut)
    print("全局最优切点（只数对错）：th≈%.4f → 对 %d/%d；仍错——拦掉该救的 %d 条、放进该挡的 %d 条"
          % (best_cut[1], best_cut[0], len(items), len(best_cut[3]), len(best_cut[2])))
    print("   被拦掉该救的：%s" % "；".join(x[2] for x in best_cut[3]))
    print("   放进该挡的：%s" % "；".join(x[2] for x in best_cut[2]))

    # ================= F. 换个形状试试：不看分数高低，看「指向是否集中」 =================
    # 依据：被拦掉的 4 条都是"字打歪了的具体名"，被放进来的 5 条都是"说对了的泛称"。
    # 两者共同点是"指向模糊"，区别在于——前者只对得上**一条**，后者对得上**一片**。
    # 所以试一个量：gap = top1 − top2（第一名与第二名的落差）。
    def g2(sims):
        return sims[0] - sims[1]

    req_g = sorted([(g2([x["sim"] for x in r["sem_top5"]]), r) for r in req], key=lambda x: x[0])
    ov_g = sorted([(g2([x["sim"] for x in r["sem_top5"]]), r) for r in over], key=lambda x: x[0])
    ov_g += sorted([(g2([x["sim"] for x in r["top3"]]), r)
                    for r in extra_rows if r["name"].startswith("泛类词")], key=lambda x: x[0])
    ov_g.sort(key=lambda x: x[0])
    print()
    print("=" * 84)
    print("F. 换个形状：gap = top1 − top2（指向集中度）")
    print("=" * 84)
    print("该救面最小 gap = %.4f ← 『%s』" % (req_g[0][0], req_g[0][1]["text"]))
    print("该挡面最大 gap = %.4f ← 『%s』" % (ov_g[-1][0], ov_g[-1][1]["text"]))
    sep_gap = ov_g[-1][0] < req_g[0][0]
    print("⇒ gap 可分？ %s" % ("是" if sep_gap else "**否（重叠区 [%.4f, %.4f]）**" % (ov_g[-1][0], req_g[0][0])))
    if not sep_gap:
        print("   跨线两侧（gap 最小的该救 / gap 最大的该挡）：")
        for s, r in req_g[:3]:
            print("     该救 gap=%.4f  %-18r %.4f" % (s, r["text"][:16], r["sem_top1"]))
        for s, r in ov_g[-3:]:
            nm = r.get("text") or r.get("name")
            print("     该挡 gap=%.4f  %-18r" % (s, str(nm)[:16]))
    else:
        print("   切点可定在 (%.4f, %.4f) 之间" % (ov_g[-1][0], req_g[0][0]))

    # ================= 报告 =================
    L = []
    L.append("# 红队测试 v2 · 语义通道：漏报面、过匹配面，与「闸」的形状（09-16）\n")
    L.append("> 样本：**完全复用**字面红测那 48 条（`_redtest_kw_spec.json`），两侧可比。")
    L.append("> 语料：`compiled/geo1-v9/runtime.json`（%d 条，与字面红测**同包**）｜模型：本机 bge-small-zh-v1.5（ONNX，零下载）。" % len(entries))
    L.append("> 字面侧**不自己复刻**：读真代码跑出的 `_redtest_kw_result.json`。\n")
    L.append("## 零、阳性对照\n")
    L.append("- 同句自比余弦 = **%.6f**（须 = 1.0）" % self_cos)
    L.append("- 无关句『%s』在全部 %d 条上的最高相似度 = **%.4f** —— 这就是“什么都不问”时的底噪。\n" % (NEGATIVE, len(entries), neg_top))
    L.append("## 一、漏报面（该开口的 35 条）\n")
    L.append("| 分组 | 总 | 字面 OK | 语义 OK |\n|---|---|---|---|")
    for k in sorted(g):
        L.append("| %s | %d | %d | %d |" % (k, g[k][0], g[k][1], g[k][2]))
    L.append("| **合计** | **%d** | **%d** | **%d** |" % (
        len(miss), sum(1 for r in miss if r["lex_ok"]), sum(1 for r in miss if r["sem_hit3"])))
    L.append("")
    L.append("**并集（字面命中 或 语义 top3 命中）= %d/%d**\n" % (
        sum(1 for r in miss if r["union_hit"]), len(miss)))
    L.append("## 二、★ 闸：一个阈值能不能分开两边\n")
    L.append("- 该开口的（MISS-RISK）top1 最低 = **%.4f**（『%s』）" % (miss_min, miss_arg["text"]))
    L.append("- 不该开口的（OVER-RISK）top1 最高 = **%.4f**（『%s』）" % (over_max, over_arg["text"] or "(空)"))
    L.append("- ⇒ **单一绝对阈值可分？%s**\n" % ("是" if separable else "否（两类重叠，重叠区 [%.4f, %.4f]）" % (over_max, miss_min)))
    L.append("| th | 救回（该开口） | 压住（不该开口） |\n|---|---|---|")
    for x in scan:
        if 0.40 <= x["th"] <= 0.72:
            L.append("| %.2f | %d/%d | %d/%d |" % (x["th"], x["rescued"], x["n_miss"], x["suppressed"], x["n_over"]))
    L.append("")
    L.append("- 两边相加最优：**th=%.2f → 救回 %d + 压住 %d = %d/%d**" % (
        best[0]["th"], best[0]["rescued"], best[0]["suppressed"],
        best[0]["rescued"] + best[0]["suppressed"], len(miss) + len(over)))
    L.append("- 相对闸（margin = top1 − top5）：该开口的最低 %.4f ／ 不该开口的最高 %.4f ⇒ **%s**\n" % (
        m_miss[0], m_over[-1], "可分" if m_over[-1] < m_miss[0] else "否"))
    L.append("## 二·B、两条并列召回 + 闸：最终形态的两个数\n")
    L.append("（无闸）字面 %d/35 · 语义 top1 直答 %d/35 · 语义 top3 含答案 %d/35 · 并集 %d/35；乱答 字面 %d/13\n"
             % (lex_ok_miss, sem1, sem3, sum(1 for r in miss if r["union_hit"]), over_lex_noise))
    L.append("| th | 救回（并集，top1 口径） | 乱答（字面既有 或 语义开口） |\n|---|---|---|")
    for x in gate:
        if 0.40 <= x["th"] <= 0.68:
            L.append("| %.2f | %d/35 | %d/13 |" % (x["th"], x["rescued"], x["noisy"]))
    L.append("")
    L.append("## 二·C、绝对分被什么带着走（★ 这一节是「闸」能不能用绝对分的判据）\n")
    L.append("| 探测 | 输入 | 语义 top1 | top1 落到 |\n|---|---|---|---|")
    for r in extra_rows:
        L.append("| %s | `%s` | %.4f | %s |" % (
            r["name"], r["text"], r["top1"], r["top3"][0]["id"].split(":")[-1]))
    L.append("")
    L.append("- 无关**短**句『%s』最高分 = %.4f；四条无关**长**句平均 top1 = **%.4f**" % (
        NEGATIVE, neg_top, long_avg))
    L.append("- ⇒ 只把话说长、内容仍然是无意义闲聊，分数就抬了 **%.4f**。\n" % (long_avg - neg_top))
    L.append("## 二·D、摘掉「不值得救」的之后：假重叠 vs 真重叠（09-16 22:0x 甲方口径）\n")
    L.append("口径原话：**拼音缩写过不了很正常，只能怪玩家自己不好好说话** ⇒ 语义通道的义务面摘掉")
    L.append("拼音首字母（`gk`／`hbk`）与拉丁写法（`kettle helmet`／`iron hat`）；**英文名由字面通道负责**。\n")
    L.append("- 该救面 **%d → %d 条**；争议区（截断／倒装／单字／半截词）**%d 条**单列，未并入。"
             % (len(miss), len(req), len(dis)))
    L.append("- **假重叠**（不值得救的 vs 该挡的，本就不该拿来比）：%.4f–%.4f，落进该挡面 %.4f–%.4f。"
             % (min(r["sem_top1"] for r in exm), max(r["sem_top1"] for r in exm),
                min(s for s, _ in over_all), over_max_all))
    L.append("- **真重叠**（值得救的 vs 该挡的）：该救最低 **%.4f**（『%s』）／该挡最高 **%.4f**（%s）。\n"
             % (req_min, req_arg["text"], over_max_all, over_arg_all))
    L.append("- **全局最优切点 th≈%.4f → 对 %d/%d**；仍错：**拦掉该救的 %d 条**（%s）、**放进该挡的 %d 条**（%s）。\n"
             % (best_cut[1], best_cut[0], len(items), len(best_cut[3]),
                "／".join(x[2] for x in best_cut[3]),
                len(best_cut[2]), "／".join(x[2] for x in best_cut[2])))
    L.append("## 二·E、换个形状：gap = top1 − top2（指向是否集中）\n")
    L.append("- 该救面 gap 最小 = **%.4f**（『%s』）／该挡面 gap 最大 = **%.4f**（『%s』）"
             % (req_g[0][0], req_g[0][1]["text"], ov_g[-1][0], ov_g[-1][1]["text"]))
    L.append("- ⇒ **可分？%s**\n" % ("是" if sep_gap else "否（重叠区 [%.4f, %.4f]）" % (ov_g[-1][0], req_g[0][0])))
    L.append("## 三、逐条\n")
    L.append("| # | 分组 | 输入 | 目标 | 字面 | 语义 top1 | 语义 hit3 | 并集 | 语义 top1 命中 |")
    L.append("|---|---|---|---|---|---|---|---|---|")
    for r in rows:
        t = r["text"].replace("\t", "\\t") or "(空)"
        L.append("| %s | %s | `%s` | %s | %s | %.4f | %s | %s | %s |" % (
            r["no"], r["group"], t,
            ",".join(x.split(":")[-1] for x in r["targets"]) or "(不该命中)",
            "OK" if r["lex_ok"] else "RED", r["sem_top1"],
            ("OK" if r["sem_hit3"] else "RED") if r["cat"] == "MISS-RISK" else "—",
            ("OK" if r["union_hit"] else "RED") if r["cat"] == "MISS-RISK" else "—",
            r["sem_top5"][0]["id"].split(":")[-1]))
    L.append("")
    io.open(OUT_MD, "w", encoding="utf-8").write("\n".join(L))
    json.dump({
        "entries": len(entries), "self_cos": self_cos, "negative_top_sim": neg_top,
        "miss_min_top1": miss_min, "over_max_top1": over_max, "separable_abs": separable,
        "margin_separable": m_over[-1] < m_miss[0], "scan": scan, "rows": rows,
        "extra": extra_rows, "long_avg_top1": float(long_avg),
    }, io.open(OUT_JSON, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print("\n报告 -> %s\n结果 -> %s（总用时 %.1f s）" % (OUT_MD, OUT_JSON, time.time() - t0))


if __name__ == "__main__":
    main()
