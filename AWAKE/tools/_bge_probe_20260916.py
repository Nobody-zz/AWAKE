# -*- coding: utf-8 -*-
"""AWAKE 检索层对照探针：现在的「字面匹配」 vs 「bge-small-zh-v1.5 语义匹配」。

目的：在决定要不要引入语义模型之前，先在**我们的真实问法**上量一次差距。
不用下载：模型复用 AnimusForge 盘上那份（bge-small-zh-v1.5，MIT）。

方法学（写在最前，便于别人挑刺）：
- 语料 = runtime.json 的 448 条（与上线包同一份）。
- 字面侧：**严格照抄现网逻辑** `WorldKnowledgeQueryService.FindCandidates`（纯子串双向匹配
  `q.IndexOf(kw)>=0 || kw.IndexOf(q)>=0`，按匹配串长度降序），且**只在 keywords 上匹配**——
  因为现网 `BuildKeywordIndex` 只收 keywords，不读 summary。
- 语义侧：文档取 `title(zh) + keywords + summary` 拼接（按我们自己报告的结论：summary 要进索引）；
  查询侧按 bge-zh-v1.5 的**官方用法**加指令前缀 `为这个句子生成表示以用于检索相关文章：`。
- 题集：26 条真人问法，**目标条目由我选定**，问法分两组：
  A 组「含实体名的直白问法」（字面侧应该赢）——对照组，防止把语义吹上天；
  B 组「不含实体名的描述型问法」（想问什么就说什么）——这才是现在最容易漏的一类。
  ⚠️ 写问法时**只读了 title/summary，没有看 keywords**，避免按关键词反推问法。
- 指标：hit@1 / hit@3（目标是否落在前 1 / 前 3）。
- 阳性对照：同一句自比余弦应为 1.0；一句跟世界书完全无关的话，最高分应明显偏低。

运行：
  "C:/Users/26811/.workbuddy/binaries/python/envs/default/Scripts/python.exe" -u AWAKE/tools/_bge_probe_20260916.py
"""
import io
import json
import os
import time

import numpy as np
import onnxruntime as ort
from tokenizers import Tokenizer

ROOT = r"D:/AWAKE-Dev/AWAKE"
AF = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/AnimusForge/ONNX"
PKG = os.path.join(ROOT, "ModuleData/Worldbook/packages/calradia/runtime.json")
OUT = os.path.join(ROOT, "tools/_bge_probe_20260916.json")
INSTRUCTION = "为这个句子生成表示以用于检索相关文章："

# (组, 目标条目 id, 玩家问法)
QUERIES = [
    # ---- A 组：含实体名的直白问法（对照，字面侧应当赢） ----
    ("A", "awake:entry:geography.villages-lamesa", "拉迈萨那地方养什么？"),
    ("A", "awake:entry:geography.castles-simira-castle", "西米拉堡是谁的城堡？"),
    ("A", "awake:entry:geography.villages-chornobas", "乔诺巴斯在哪儿？"),
    ("A", "awake:entry:culture.tales-husn-fulq", "富勒格是谁？"),
    ("A", "awake:entry:culture.tales-kachar-rulership", "卡恰尔半岛归谁？"),
    ("A", "awake:entry:politics.throne-saneopa", "萨涅俄帕是什么地方？"),
    ("A", "awake:entry:war.military-sturgia", "斯特吉亚的军队怎么打仗？"),
    ("A", "awake:entry:war.military-khuzait", "库赛特靠什么打仗？"),
    ("A", "awake:entry:war.troops-royal-guard", "皇家侍卫是什么兵？"),
    ("A", "awake:entry:economy.goods-fur", "毛皮值钱吗？"),
    ("A", "awake:entry:economy.goods-salt", "盐是从哪儿来的？"),
    ("A", "awake:entry:economy.items-western_plated_helmet", "侧护覆板盔谁在用？"),
    ("A", "awake:entry:politics.territories-varcheg-swap", "瓦尔切格怎么会换主人？"),
    # ---- B 组：不含实体名的描述型问法（现在最容易漏的一类） ----
    ("B", "awake:entry:geography.villages-lamesa", "这一带有好马吗？"),
    ("B", "awake:entry:geography.castles-simira-castle", "哪座城堡底下管着两个村子？"),
    ("B", "awake:entry:geography.villages-chornobas", "有大瀑布的村子是哪个？"),
    ("B", "awake:entry:culture.tales-husn-fulq", "集市上说书人讲的那个鬼点子是谁的？"),
    ("B", "awake:entry:culture.tales-kachar-rulership", "有个半岛被三家抢过，谁说了算？"),
    ("B", "awake:entry:culture.tales-dawn-taboo", "山里有不让进的地方吗？"),
    ("B", "awake:entry:politics.throne-saneopa", "隘口上那个商埠，哪个家族在那儿定过都？"),
    ("B", "awake:entry:war.troops-royal-guard", "什么人拿大圆盾扔飞斧？"),
    ("B", "awake:entry:war.military-khuzait", "游牧的人怎么用骑马射手打仗？"),
    ("B", "awake:entry:war.military-sturgia", "盾墙加飞斧是哪个国家的打法？"),
    ("B", "awake:entry:economy.goods-fur", "北方有什么又暖又贵的皮货？"),
    ("B", "awake:entry:economy.goods-salt", "桌上少不了、产地又集中的日用品是什么？"),
    ("B", "awake:entry:economy.items-western_plated_helmet", "哪种头盔护到腮帮子和耳朵？"),
]

NEGATIVE = "今天午饭吃什么"


def zh_title(e):
    t = e.get("title") or {}
    return t.get("zh-CN") or (list(t.values())[0] if t else "")


def zh_summary(e):
    return (e.get("summary") or {}).get("zh-CN", "")


def doc_text(e):
    return "。".join(x for x in (zh_title(e), "、".join(e.get("keywords") or []), zh_summary(e)) if x)


class LexicalIndex:
    """严格照抄 WorldKnowledgeQueryService.FindCandidates + MatchQuality 的匹配与排序。

    ⚠️ 两条纪律（2026-09-16 踩过，务必保留）：
      ① 候选**只从 keywords 出来**——标题不单独作为入口 ⇒「标题命中」只对**关键词也命中了**的条目生效；
         且候选表是**不分大小写**的（C# 用 OrdinalIgnoreCase）。
      ② 排序是 **Rank 升序 → 同级 Length 降序 → Id 升序**；Rank 定义为
         0 标题全等 / 1 关键词全等 / 2 标题互为子串 / 3 关键词互为子串。
         早先这一臂只按「匹配串长度」排序、漏了 Rank ⇒ **A 组少算 2 条**，
         把真实现的成绩说低了（C# 真代码给 9/13，复刻只给 7/13）。补上后两边一致。
    """

    def __init__(self, entries):
        self.ids = [e["id"] for e in entries]
        self.titles = [zh_title(e) for e in entries]
        self.kws = [list(e.get("keywords") or []) for e in entries]
        self.pairs = []          # (entry_index, keyword)
        for i, ks in enumerate(self.kws):
            for kw in ks:
                if isinstance(kw, str) and kw.strip():
                    self.pairs.append((i, kw))

    def rank_of(self, i, text):
        title = self.titles[i]
        if title and title.lower() == text.lower():
            return (0, len(title))
        exact = [len(k) for k in self.kws[i] if k and k.lower() == text.lower()]
        if exact:
            return (1, max(exact))
        if title and (text.lower() in title.lower() or title.lower() in text.lower()):
            return (2, len(title))
        matched = [len(k) for k in self.kws[i] if k and (text.lower() in k.lower() or k.lower() in text.lower())]
        return (3, max(matched) if matched else 0)

    def search(self, q, topk=3):
        low = q.lower()
        ids = set()
        for i, kw in self.pairs:
            k = kw.lower()
            if low in k or k in low:
                ids.add(i)
        scored = [(self.rank_of(i, q), self.ids[i], i) for i in ids]
        scored.sort(key=lambda x: (x[0][0], -x[0][1], x[1]))
        return [(i, s[1]) for s, _, i in scored[:topk]]


def grams(s):
    s = "".join(ch for ch in s if not ch.isspace())
    return {s[i:i + 2] for i in range(len(s) - 1)}


class CheapIndex:
    """零字节基线：把 title+keywords+summary 全进索引，用 2-gram（IDF 加权）打分。

    这是「不引入任何模型」条件下能拿到的上限参照 —— 用来判断语义模型到底值不值那 90 MB。

    ⚠️ **这不是上线实现，也别拿它的数去预测上线的数（2026-09-16 实测：它给 B 5/13，上线给 7/13）。**
    本臂是「全库 2-gram 打分」，上线的是 `WorldKnowledgeQueryService` 里那条**兜底通道**
    （只在关键词一无所获时才启用 ＋ 高频 term 不进口 ＋ 候选上限 5）。两者机制不同：
    这条只是**探索**「补 summary 值不值」，不是那条通道的复刻。
    ⇒ 调参请跑 C# 验台 `tools/worldbook-runtime-smoke`（~4 秒一轮，且带回门禁），本探针不做上线预测。
    """

    def __init__(self, entries):
        self.docs = [grams(doc_text(e)) for e in entries]
        self.texts = [doc_text(e) for e in entries]
        df = {}
        for g in self.docs:
            for x in g:
                df[x] = df.get(x, 0) + 1
        import math
        self.idf = {k: 1.0 / math.log(1.0 + v) for k, v in df.items()}

    def search(self, q, topk=3):
        qg = grams(q)
        scores = []
        for i, dg in enumerate(self.docs):
            shared = qg & dg
            if not shared:
                continue
            s = sum(self.idf.get(g, 1.0) for g in shared)
            scores.append((i, s))
        scores.sort(key=lambda x: -x[1])
        return scores[:topk]


def main():
    t0 = time.time()
    doc = json.load(io.open(PKG, encoding="utf-8"))
    entries = doc["entries"]
    print("语料 %d 条" % len(entries))

    lex = LexicalIndex(entries)
    cheap = CheapIndex(entries)

    tok = Tokenizer.from_file(os.path.join(AF, "tokenizer.json"))
    tok.enable_truncation(max_length=512)
    tok.enable_padding(pad_id=0, pad_token="[PAD]")
    so = ort.SessionOptions()
    so.log_severity_level = 3
    sess = ort.InferenceSession(os.path.join(AF, "model.onnx"), so,
                                providers=["CPUExecutionProvider"])

    def encode(texts):
        encs = tok.encode_batch(texts)
        ids = np.array([e.ids for e in encs], dtype=np.int64)
        mask = np.array([e.attention_mask for e in encs], dtype=np.int64)
        feed = {"input_ids": ids, "attention_mask": mask,
                "token_type_ids": np.zeros_like(ids)}
        v = sess.run(["sentence_embedding"], feed)[0]
        return v.astype(np.float32)

    # ---- 阳性对照 ----
    probe = encode(["帕拉汶德", "帕拉汶德", NEGATIVE])
    n0 = np.linalg.norm(probe, axis=1)
    print("阳性对照：向量范数 = %s（≈1 表示模型自带归一化）" % np.round(n0, 4))
    self_cos = float(probe[0] @ probe[1] / (n0[0] * n0[1] + 1e-9))
    print("阳性对照：同句自比余弦 = %.6f（须 = 1.0）" % self_cos)
    assert abs(self_cos - 1.0) < 1e-3, "自比不等于 1 ⇒ 编码链路有问题，后面的数不可信"

    # ---- 文档侧 ----
    t = time.time()
    D = encode([doc_text(e) for e in entries])
    Dn = D / (np.linalg.norm(D, axis=1, keepdims=True) + 1e-9)
    print("文档编码 %d 条，用时 %.1f s（%.1f ms/条，CPU）"
          % (len(entries), time.time() - t, (time.time() - t) * 1000 / len(entries)))

    # ---- 跑题集 ----
    rows = []
    qt = time.time()
    Q = encode([INSTRUCTION + q for _, _, q in QUERIES])
    Qn = Q / (np.linalg.norm(Q, axis=1, keepdims=True) + 1e-9)
    print("查询编码 %d 条，用时 %.2f s"
          % (len(QUERIES), time.time() - qt))

    negv = encode([NEGATIVE])[0]
    negv = negv / (np.linalg.norm(negv) + 1e-9)
    neg_top = float((Dn @ negv).max())
    print("阳性对照：无关句『%s』在 448 条上的最高相似度 = %.4f" % (NEGATIVE, neg_top))

    idx_of = {e["id"]: i for i, e in enumerate(entries)}
    for k, (grp, target, q) in enumerate(QUERIES):
        ti = idx_of[target]
        # 字面（现网）
        lhits = [i for i, _ in lex.search(q, 3)]
        ltop1 = (lhits[0] == ti) if lhits else False
        ltop3 = ti in lhits
        # 零字节基线（title+keywords+summary，2-gram IDF）
        chits = [i for i, _ in cheap.search(q, 3)]
        ctop1 = (chits[0] == ti) if chits else False
        ctop3 = ti in chits
        # 语义
        sims = Dn @ Qn[k]
        sorder = np.argsort(-sims)[:3]
        stop1 = int(sorder[0]) == ti
        stop3 = ti in [int(x) for x in sorder]
        rows.append({
            "group": grp, "query": q, "target": target,
            "lexical_top3": [entries[i]["id"] for i in lhits],
            "lexical_hit1": ltop1, "lexical_hit3": ltop3,
            "cheap_top3": [entries[i]["id"] for i in chits],
            "cheap_hit1": ctop1, "cheap_hit3": ctop3,
            "semantic_top3": [{"id": entries[int(i)]["id"], "sim": round(float(sims[int(i)]), 4)}
                              for i in sorder],
            "semantic_hit1": stop1, "semantic_hit3": stop3,
        })

    def agg(grp, key):
        rs = [r for r in rows if grp == "ALL" or r["group"] == grp]
        return sum(1 for r in rs if r[key]), len(rs)

    stats = {}
    for grp in ("A", "B", "ALL"):
        for src in ("lexical", "cheap", "semantic"):
            for kk in ("hit1", "hit3"):
                n, d = agg(grp, "%s_%s" % (src, kk))
                stats["%s_%s_%s" % (grp, src, kk)] = "%d/%d" % (n, d)

    print()
    print("==== 结果（命中数/总数） ====")
    print("%-16s %-16s %-16s %-16s" % ("", "字面·现网", "字面·补 summary", "语义 bge-small"))
    for grp, label in (("A", "A 组·含实体名"), ("B", "B 组·口语描述"), ("ALL", "合计")):
        print("%-16s %-16s %-16s %-16s" % (
            label,
            "h1 %s / h3 %s" % (stats["%s_lexical_hit1" % grp], stats["%s_lexical_hit3" % grp]),
            "h1 %s / h3 %s" % (stats["%s_cheap_hit1" % grp], stats["%s_cheap_hit3" % grp]),
            "h1 %s / h3 %s" % (stats["%s_semantic_hit1" % grp], stats["%s_semantic_hit3" % grp])))
    print()
    for r in rows:
        print("[%s] %s" % (r["group"], r["query"]))
        print("     目标 %s" % r["target"])
        print("     字面·现网 top3: %s" % (r["lexical_top3"] or "(无命中)"))
        print("     字面·补summary top3: %s" % (r["cheap_top3"] or "(无命中)"))
        print("     语义 top3: %s" % [(x["id"].split(":")[-1], x["sim"]) for x in r["semantic_top3"]])

    json.dump({"stats": stats, "rows": rows,
               "negative_top_sim": neg_top, "self_cos": self_cos},
              io.open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print()
    print("结果已写 %s（总用时 %.1f s）" % (OUT, time.time() - t0))


if __name__ == "__main__":
    main()
