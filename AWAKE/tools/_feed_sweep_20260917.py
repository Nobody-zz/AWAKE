# -*- coding: utf-8 -*-
"""「怎么喂」对照扫描（2026-09-17）。

回答的问题：词条要拼成什么形状喂给嵌入模型？换哪一档模型？要不要加查询指令？

三条纪律（写在前，便于别人挑刺）：
  ① 题集**只有一份** —— tools/_retrieval_cases_20260916.json，与 C# 验台
     tools/worldbook-runtime-smoke（RetrievalProbeCases.cs）读同一份。
  ② 字面侧的数**不从本脚本出** —— 直接读 C# 验台刚跑出来的日志
     tools/_smoke_run_20260917.txt 的 `RETRIEVAL_CASE` 行。本脚本只算**语义侧**，
     末尾给「字面 / 语义 / 并集」三列，避免我在 Python 里再复刻一遍 C#（平行实现必须同源）。
  ③ 每个模型都做**阳性对照**：同句自比余弦须 = 1.0；无关句的最高相似度单独打印，
     用来看「分高不高」还是「分不分得开」。

模型（本机现成，全部已在本仓账上）：
  small_f32  = AnimusForge/ONNX/model.onnx          现状（游戏里跑的就是这份）
  small_int8 = dl/small_int8.onnx                   Xenova/bge-small-zh-v1.5 int8
  base_f32   = dl/model.onnx                        Xenova/bge-base-zh-v1.5 f32
  base_int8  = dl/model_int8.onnx                   Xenova/bge-base-zh-v1.5 int8

喂法（文档侧拼装）：
  A  = title
  B  = title + summary                       （= 字面兜底通道真正读的那两个字段）
  C  = title + summary + keywords(只取含汉字的)
  Ck = title + summary + keywords(全要，含英文字串)
  D  = C + 第一条 expression.text
  E  = C + 全部 expression.text

查询侧两种：加 bge 官方中文指令 / 不加（bge-*-v1.5 官方说「不加只轻微掉分」）。

运行：
  "C:/Users/26811/.workbuddy/binaries/python/envs/default/Scripts/python.exe" -u AWAKE/tools/_feed_sweep_20260917.py
"""
import io
import json
import os
import re
import sys
import time

import numpy as np
import onnxruntime as ort
from tokenizers import Tokenizer

ROOT = r"D:/AWAKE-Dev/AWAKE"
AF = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/AnimusForge/ONNX"
DL = os.path.join(ROOT, "tools/_reranker_survey/dl")
PKG = os.path.join(ROOT, "ModuleData/Worldbook/packages/calradia/runtime.json")
CASES = os.path.join(ROOT, "tools/_retrieval_cases_20260916.json")
SMOKE = os.path.join(ROOT, "tools/_smoke_run_20260917.txt")
OUT = os.path.join(ROOT, "tools/_feed_sweep_20260917.json")
INSTRUCTION = "为这个句子生成表示以用于检索相关文章："
NEGATIVE = "今天午饭吃什么"

MODELS = [
    ("small_f32", os.path.join(AF, "model.onnx")),
    ("small_int8", os.path.join(DL, "small_int8.onnx")),
    ("base_f32", os.path.join(DL, "model.onnx")),
    ("base_int8", os.path.join(DL, "model_int8.onnx")),
]
PLANS = ["A", "B", "C", "Ck", "D", "E"]


# ── 语料侧文本 ────────────────────────────────────────────────────────────────
def pick(v, lang="zh-CN"):
    if isinstance(v, dict):
        return v.get(lang) or v.get("zh-CN") or v.get("en") or ""
    return v or ""


def has_cjk(s):
    return any("\u4e00" <= ch <= "\u9fff" for ch in s)


def join(*parts):
    return "。".join(x for x in parts if x)


def doc_text(plan, e):
    t = pick(e.get("title"))
    s = pick(e.get("summary"))
    kws = [k for k in (e.get("keywords") or []) if isinstance(k, str) and k.strip()]
    kw_cjk = [k for k in kws if has_cjk(k)]
    expr = [pick(x.get("text")) for x in (e.get("expressions") or [])]
    if plan == "A":
        return t
    if plan == "B":
        return join(t, s)
    if plan == "C":
        return join(t, "、".join(kw_cjk), s)
    if plan == "Ck":
        return join(t, "、".join(kws), s)
    if plan == "D":
        return join(t, "、".join(kw_cjk), s, expr[0] if expr else "")
    if plan == "E":
        return join(t, "、".join(kw_cjk), s, "".join(expr))
    raise ValueError(plan)


# ── 语义臂 ────────────────────────────────────────────────────────────────────
class Embedder(object):
    def __init__(self, path, tok):
        so = ort.SessionOptions()
        so.log_severity_level = 3
        self.sess = ort.InferenceSession(path, so, providers=["CPUExecutionProvider"])
        self.tok = tok
        self.outs = [o.name for o in self.sess.get_outputs()]
        self.ins = [i.name for i in self.sess.get_inputs()]
        # 是「已池化并归一」的导出，还是裸 last_hidden_state？判据只看输出名，不猜。
        self.pooled = "sentence_embedding" in self.outs
        self.head = "sentence_embedding" if self.pooled else (
            "pooler_output" if "pooler_output" in self.outs else "last_hidden_state")

    def encode(self, texts, batch=32):
        out = []
        for i in range(0, len(texts), batch):
            chunk = texts[i:i + batch]
            encs = self.tok.encode_batch(chunk)
            ids = np.array([e.ids for e in encs], dtype=np.int64)
            mask = np.array([e.attention_mask for e in encs], dtype=np.int64)
            feed = {"input_ids": ids, "attention_mask": mask}
            if "token_type_ids" in self.ins:
                feed["token_type_ids"] = np.zeros_like(ids)
            v = self.sess.run([self.head], feed)[0]
            if not self.pooled:
                # BGE 用 CLS 池化（取第 0 位），不是 mean —— 别想当然。
                v = v[:, 0, :] if v.ndim == 3 else v
            out.append(np.asarray(v, dtype=np.float32))
        return np.vstack(out)


def norm(v):
    return v / (np.linalg.norm(v, axis=1, keepdims=True) + 1e-9)


# ── 字面臂：只读 C# 验台的日志 ────────────────────────────────────────────────
def literal_from_smoke():
    """返回 {query: (h1, h3, top3_ids)}，来源是 C# 验台真代码的输出，不是复刻。"""
    if not os.path.exists(SMOKE):
        return None
    txt = io.open(SMOKE, encoding="utf-8", errors="replace").read()
    res = {}
    for line in txt.splitlines():
        if "RETRIEVAL_CASE" not in line:
            continue
        q = re.search(r"q=(\S.*)$", line)
        h1 = re.search(r"h1=(\d)", line)
        top3 = re.search(r"top3=\[([^\]]*)\]", line)
        if not (q and h1):
            continue
        ids = [x.strip() for x in top3.group(1).split(",")] if top3 and top3.group(1).strip() else []
        res[q.group(1).strip()] = (int(h1.group(1)), ids)
    return res or None


def main():
    only = sys.argv[1:] or None
    t0 = time.time()
    doc = json.load(io.open(PKG, encoding="utf-8"))
    entries = doc["entries"]
    cases = json.load(io.open(CASES, encoding="utf-8"))["cases"]
    idx_of = {e["id"]: i for i, e in enumerate(entries)}

    tok = Tokenizer.from_file(os.path.join(AF, "tokenizer.json"))
    tok.enable_truncation(max_length=512)
    tok.enable_padding(pad_id=0, pad_token="[PAD]")

    # 各拼法的文本长度（按 token 数算，比按字符数准；两个中文词的 token 数可差一倍）
    print("==== 各拼法的 token 长度（全 448 条） ====")
    plan_docs = {}
    for plan in PLANS:
        texts = [doc_text(plan, e) for e in entries]
        plan_docs[plan] = texts
        lens = np.array([len(tok.encode(t).ids) for t in texts])
        print("%-3s tokens: 中位 %3d / 均值 %5.1f / 最大 %3d / 超 512 的 %d 条"
              % (plan, int(np.median(lens)), lens.mean(), int(lens.max()), int((lens > 512).sum())))

    lit = literal_from_smoke()
    if lit is None:
        print("⚠️ 没找到 %s —— 字面列留空。先跑：cd tools/worldbook-runtime-smoke && dotnet run -c Release" % SMOKE)

    report = {"plans_token_len": {}, "arms": {}, "literal": None}
    for plan in PLANS:
        lens = [len(tok.encode(t).ids) for t in plan_docs[plan]]
        report["plans_token_len"][plan] = {
            "median": int(np.median(lens)), "max": int(max(lens)),
            "over512": int(sum(1 for x in lens if x > 512))}

    for name, path in MODELS:
        if only and name not in only:
            continue
        if not os.path.exists(path):
            print("跳过 %s（文件不在：%s）" % (name, path))
            continue
        print()
        print("==== 模型 %s ====" % name)
        model = Embedder(path, tok)
        print("  输出 %s / 输入 %s / 池化导出=%s" % (model.outs, model.ins, model.pooled))

        # 阳性对照：同句自比 + 无关句最高分
        pc = norm(model.encode([entries[0]["id"], entries[0]["id"], NEGATIVE]))
        self_cos = float(pc[0] @ pc[1])
        print("  阳性对照：同句自比余弦 = %.6f（须 = 1.0）" % self_cos)
        assert abs(self_cos - 1.0) < 1e-3, "自比不等于 1 ⇒ 编码链路有问题，后面的数不可信"

        arm = {"output_head": model.head, "pooled": model.pooled, "self_cos": self_cos}
        for plan in PLANS:
            t = time.time()
            D = norm(model.encode(plan_docs[plan]))
            enc_ms = (time.time() - t) * 1000 / len(plan_docs[plan])
            neg_top = float((D @ pc[2]).max())
            for use_instr in (True, False):
                qs = [(INSTRUCTION + c["query"]) if use_instr else c["query"] for c in cases]
                Q = norm(model.encode(qs))
                rows = []
                for k, c in enumerate(cases):
                    ti = idx_of[c["target"]]
                    order = np.argsort(-(D @ Q[k]))[:3]
                    rows.append({"group": c["group"], "query": c["query"], "target": c["target"],
                                 "hit1": bool(int(order[0]) == ti),
                                 "hit3": bool(ti in [int(x) for x in order]),
                                 "top3": [entries[int(i)]["id"] for i in order],
                                 "top1_sim": round(float((D @ Q[k])[int(order[0])]), 4)})
                key = "%s|%s|%s" % (plan, "instr" if use_instr else "raw", name)
                st = {}
                for grp in ("A", "B", "ALL"):
                    rs = [r for r in rows if grp == "ALL" or r["group"] == grp]
                    st["%s_h1" % grp] = "%d/%d" % (sum(1 for r in rs if r["hit1"]), len(rs))
                    st["%s_h3" % grp] = "%d/%d" % (sum(1 for r in rs if r["hit3"]), len(rs))
                arm[key] = {"stats": st, "neg_top_sim": neg_top, "enc_ms_per_doc": round(enc_ms, 2),
                            "rows": rows}
                print("  %-3s %-5s  A %s/%s  B %s/%s  合计 %s/%s  (top1均值 %.3f, 无关句最高 %.3f, %.1f ms/条)"
                      % (plan, "指令" if use_instr else "裸句",
                         st["A_h1"], st["A_h3"], st["B_h1"], st["B_h3"], st["ALL_h1"], st["ALL_h3"],
                         float(np.mean([r["top1_sim"] for r in rows])), neg_top, enc_ms))
        report["arms"][name] = arm

    if lit is not None:
        report["literal"] = {q: {"hit1": h1, "top3": ids} for q, (h1, ids) in lit.items()}
        lh1 = sum(1 for c in cases if lit.get(c["query"], (0, []))[0] == 1)
        print()
        print("==== 字面臂（C# 验台真代码，非复刻） 合计 hit1 = %d/%d ====" % (lh1, len(cases)))

    # ── 并集：语义最好的那一档 vs 字面 ────────────────────────────────────────
    if lit is not None:
        best = None
        for name in (only or [m[0] for m in MODELS]):
            a = report["arms"].get(name)
            if not a:
                continue
            for key, v in a.items():
                if not isinstance(v, dict) or "stats" not in v:
                    continue
                n = sum(1 for r in v["rows"] if r["hit1"])
                if best is None or n > best[0]:
                    best = (n, name, key, v)
        if best:
            n, name, key, v = best
            print()
            print("==== 并集（语义最好档 %s %s，hit1 %d/%d） ====" % (name, key, n, len(cases)))
            union = 0
            sem_only, lit_only = [], []
            for r in v["rows"]:
                l = lit.get(r["query"])
                l_h1 = bool(l and l[0] == 1)
                s_h1 = bool(r["hit1"])
                union += (l_h1 or s_h1)
                if s_h1 and not l_h1:
                    sem_only.append((r["group"], r["query"], r["target"].split(":")[-1]))
                if l_h1 and not s_h1:
                    lit_only.append((r["group"], r["query"], r["target"].split(":")[-1]))
            print("  并集 hit1 = %d/%d（字面 %d + 仅语义 %d；仅字面 %d）"
                  % (union, len(cases), sum(1 for c in cases if lit.get(c["query"], (0, []))[0] == 1),
                     len(sem_only), len(lit_only)))
            print("  ── 只有语义救得回来的：")
            for g, q, t in sem_only:
                print("     [%s] %-28s → %s" % (g, q, t))
            print("  ── 只有字面救得回来的（语义掉了的）：")
            for g, q, t in lit_only:
                print("     [%s] %-28s → %s" % (g, q, t))
            report["union"] = {"arm": "%s|%s" % (name, key), "hit1": union, "total": len(cases),
                               "semantic_only": sem_only, "literal_only": lit_only}

    json.dump(report, io.open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print()
    print("结果已写 %s（总用时 %.1f s）" % (OUT, time.time() - t0))


if __name__ == "__main__":
    main()
