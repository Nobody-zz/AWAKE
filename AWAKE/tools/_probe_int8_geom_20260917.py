# -*- coding: utf-8 -*-
"""base_int8 掉分到底是「量化坏了」还是「池化选错了」？（2026-09-17）

背景：喂法扫描里 `Xenova/bge-base-zh-v1.5` 的 int8 导出在 26 例上只拿 7/26（f32 拿 19/26），
且「无关句」最高相似度从 0.37 涨到 0.68 —— 典型的「什么都很像」塌缩。但同样是 int8 的
`small_int8` 却正常（19/26）。所以要分清两件事：

  ① 量化真的把几何搞坏了（与池化无关）；
  ② 只是池化方式不对（我们按 BGE 惯例取了 CLS；裸 BERT 导出可能该用 mean）。

判据：
  · 池化无关性：同一份权重下，CLS 与 mean 两种池化，若**都**塌缩 ⇒ 是权重问题。
  · f32↔int8 同句余弦：如果远低于 0.99 ⇒ 输出向量本身已经变了（权重问题，不是池化）。
  · 归一化前的 L2 范数：int8 若显著更大 ⇒ 激活被量化夹到饱和，方向也一起失真。
  对照：`small_int8` 同样跑一遍（它是正常的），两边一比就知道是不是普遍现象。

运行：
  "C:/Users/26811/.workbuddy/binaries/python/envs/default/Scripts/python.exe" -u AWAKE/tools/_probe_int8_geom_20260917.py
"""
import io
import json
import os

import numpy as np
import onnxruntime as ort
from tokenizers import Tokenizer

ROOT = r"D:/AWAKE-Dev/AWAKE"
AF = r"D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules/AnimusForge/ONNX"
DL = os.path.join(ROOT, "tools/_reranker_survey/dl")
PKG = os.path.join(ROOT, "ModuleData/Worldbook/packages/calradia/runtime.json")
CASES = os.path.join(ROOT, "tools/_retrieval_cases_20260916.json")
NEGATIVE = "今天午饭吃什么"

PAIRS = [
    ("small", os.path.join(AF, "model.onnx"), os.path.join(DL, "small_int8.onnx")),
    ("base", os.path.join(DL, "model.onnx"), os.path.join(DL, "model_int8.onnx")),
]


def pick(v, lang="zh-CN"):
    return (v.get(lang) or v.get("en") or "") if isinstance(v, dict) else (v or "")


def doc_text(e):
    t = pick(e.get("title"))
    s = pick(e.get("summary"))
    kws = [k for k in (e.get("keywords") or []) if isinstance(k, str) and any("\u4e00" <= c <= "\u9fff" for c in k)]
    return "。".join(x for x in (t, "、".join(kws), s) if x)


class M(object):
    def __init__(self, path, tok):
        so = ort.SessionOptions()
        so.log_severity_level = 3
        self.s = ort.InferenceSession(path, so, providers=["CPUExecutionProvider"])
        self.tok = tok
        self.head = "sentence_embedding" if "sentence_embedding" in [o.name for o in self.s.get_outputs()] else "last_hidden_state"
        self.ins = [i.name for i in self.s.get_inputs()]

    def raw(self, texts, pool):
        encs = self.tok.encode_batch(texts)
        ids = np.array([e.ids for e in encs], dtype=np.int64)
        mask = np.array([e.attention_mask for e in encs], dtype=np.int64)
        feed = {"input_ids": ids, "attention_mask": mask}
        if "token_type_ids" in self.ins:
            feed["token_type_ids"] = np.zeros_like(ids)
        v = self.s.run([self.head], feed)[0]
        if v.ndim == 2:                      # 已池化导出
            return v.astype(np.float32)
        if pool == "cls":
            return v[:, 0, :].astype(np.float32)
        m = mask[:, :, None].astype(np.float32)
        return ((v * m).sum(1) / np.clip(m.sum(1), 1e-6, None)).astype(np.float32)


def nz(x):
    return x / (np.linalg.norm(x, axis=1, keepdims=True) + 1e-9)


def cos(a, b):
    a, b = nz(a), nz(b)
    return float((a[0] * b[0]).sum())


def main():
    doc = json.load(io.open(PKG, encoding="utf-8"))
    entries = doc["entries"]
    cases = json.load(io.open(CASES, encoding="utf-8"))["cases"]
    idx_of = {e["id"]: i for i, e in enumerate(entries)}

    tok = Tokenizer.from_file(os.path.join(AF, "tokenizer.json"))
    tok.enable_truncation(max_length=512)
    tok.enable_padding(pad_id=0, pad_token="[PAD]")

    sample = [doc_text(entries[0]), doc_text(entries[10]), NEGATIVE]
    print("样本 %d 条（2 条真词条 + 1 条无关句）" % len(sample))

    for tag, f32p, i8p in PAIRS:
        print()
        print("================ %s ================" % tag)
        lo, hi = M(f32p, tok), M(i8p, tok)
        docs = [doc_text(e) for e in entries]
        qs = [c["query"] for c in cases]
        for pool in ("cls", "mean"):
            a = lo.raw(sample, pool)
            b = hi.raw(sample, pool)
            na, nb = np.linalg.norm(a, axis=1), np.linalg.norm(b, axis=1)
            print(" 池化 %-4s 归一前范数 f32=%s int8=%s" % (pool, np.round(na, 1), np.round(nb, 1)))
            for i in range(len(sample)):
                print("        同句 f32↔int8 余弦 = %.4f   （%s）"
                      % (cos(a[i:i + 1], b[i:i + 1]), sample[i][:24]))
        # 池化无关性：两种池化各跑一遍题集，看 hit1 会不会一个塌一个不塌
        for pool in ("cls", "mean"):
            D = nz(hi.raw(docs, pool))
            Q = nz(hi.raw(qs, pool))
            nraw = hi.raw([NEGATIVE, NEGATIVE], pool)
            neg = nz(nraw[:1].reshape(1, -1))[0]     # 取成 1-D，才能写 D @ neg
            h1 = h3 = 0
            grp = {}
            for k, c in enumerate(cases):
                ti = idx_of[c["target"]]
                order = np.argsort(-(D @ Q[k]))[:3]
                o = [int(x) for x in order]
                h1 += (o[0] == ti)
                h3 += (ti in o)
                g = grp.setdefault(c["group"], [0, 0, 0])
                g[0] += (o[0] == ti)
                g[1] += (ti in o)
                g[2] += 1
            print(" int8 · %-4s 池化：hit1 %d/26  hit3 %d/26  A %d/%d B %d/%d  无关句最高相似度 %.4f"
                  % (pool, h1, h3, grp["A"][0], grp["A"][2], grp["B"][0], grp["B"][2],
                     float((D @ neg).max())))


if __name__ == "__main__":
    main()
