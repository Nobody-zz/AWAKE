"""AnimusForge 本地模型（ONNX）使用逻辑探针 —— 只读取证，不改动目标。

用法：
    python _af_onnx_probe_20260916.py strings   # 从 shipped DLL 提字符串/类名
    python _af_onnx_probe_20260916.py logs      # 从 Logs/ 统计 onnx 门控与检索阶段

为什么不用 `strings`：.NET 的字符串字面量存在 #US 堆里，是 UTF-16LE，
本机也没有 strings 命令，所以自己扫 UTF-16LE 可打印序列。

结论（2026-09-16 实测，证据见 AWAKE/docs/ANIMUSFORGE-LOCAL-MODEL-ANALYSIS-20260916.md）：
  - 模型：ONNX/ = bge-small-zh-v1.5（BertModel 512/4 层/21128），
          ONNX/reranker/ = xlm-roberta-base + 分类头（768/12 层/250002）
  - 运行时：进程内 ONNX，Microsoft.ML.OnnxRuntime + 原生 onnxruntime.dll 都在模组 bin
  - 类名：OnnxEmbedding / OnnxGate / OnnxReranker / RagWarmup /
          KnowledgeIndexWarmup / BertNormalizer / GuardrailSemantic / WorldEntityRetrieval.*
  - 生效证据：FreezeWatchdog_Timeline.txt 里 `onnx=True` 443 次、`onnx=False` 0 次
  - 降级路径：mode=lexical_fallback / mode=semantic_unavailable /
              reason=query_embedding_unavailable / reason=title_embedding_empty
  - 融合打分：raw/ctx/mixed/rerank/amp + lexicalAnchor + matchedSeed + intent
"""
import os
import re
import sys

MOD = (r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
       r"\Modules\AnimusForge")
DLL = os.path.join(MOD, r"bin\Win64_Shipping_Client\versions\1.4\AnimusForge.dll")
LOGS = os.path.join(MOD, "Logs")
WATCHDOG = os.path.join(LOGS, "FreezeWatchdog_Timeline.txt")

UTF16_RE = re.compile(rb"(?:[\x20-\x7e]\x00){4,}")
IDENT_RE = re.compile(r"[A-Za-z_][A-Za-z0-9_.`<>/ ]*")

STEMS = ["Onnx", "Bert", "Rerank", "Embed", "Warmup", "Tokenizer", "WordPiece",
         "Semantic", "Vocab", "Cosine", "Inference", "Tensor", "Retriev",
         "Knowledge", "Lexical", "Seed", "HitRate"]
PATHKEYS = ["ONNX", "onnx", "reranker", "tokenizer", "config.json", "model"]


def utf16_strings(path):
    with open(path, "rb") as fh:
        blob = fh.read()
    return {m.group().decode("utf-16-le", "replace")
            for m in UTF16_RE.finditer(blob)}


def do_strings():
    strings = utf16_strings(DLL)
    print(f"# {os.path.basename(DLL)}  ({os.path.getsize(DLL):,} B)  "
          f"utf16 strings={len(strings):,}\n")

    print("-- identifiers --")
    for stem in STEMS:
        hits = sorted(s for s in strings
                      if stem in s and len(s) < 90 and IDENT_RE.fullmatch(s))
        if hits:
            print(f"  [{stem}] {len(hits)}")
            for s in hits:
                print("     ", s)

    print("\n-- path / file literals --")
    for s in sorted(x for x in strings
                    if len(x) < 200 and any(k in x for k in PATHKEYS)
                    and ("\\" in x or "/" in x or x.endswith(".json")
                         or x.endswith(".onnx"))):
        print("   ", s)

    print("\n-- telemetry / degradation strings --")
    for s in sorted(x for x in strings
                    if len(x) < 400
                    and any(k in x for k in ("mode=", "fallback=", "reason=",
                                             "hit=", "eligible="))):
        print("   ", s)


def do_logs():
    pats = ["onnx=True", "onnx=False", "mode=onnx", "lexical_fallback",
            "semantic_unavailable", "query_embedding_unavailable",
            "title_embedding_empty", "lexicalAnchor", "matchedSeed",
            "semanticContextLen=", "loreLen=", "WorldEntityRetrieval"]
    if not os.path.exists(WATCHDOG):
        print("! FreezeWatchdog_Timeline.txt 不存在")
        return
    text = open(WATCHDOG, encoding="utf-8", errors="replace").read()
    print(f"# {os.path.basename(WATCHDOG)}  {len(text):,} chars\n")
    for p in pats:
        print(f"  {p:34s} {text.count(p)}")

    print("\n-- onnx 门控首次出现 --")
    for line in text.splitlines():
        if "onnx=" in line:
            print("   ", line[:200])
            break

    print("\n-- WorldEntityRetrieval.done 样本 --")
    n = 0
    for line in text.splitlines():
        if "WorldEntityRetrieval.done" in line:
            print("   ", line[:220])
            n += 1
            if n >= 3:
                break


def main():
    what = sys.argv[1] if len(sys.argv) > 1 else "strings"
    if what == "strings":
        do_strings()
    elif what == "logs":
        do_logs()
    else:
        print(__doc__)


if __name__ == "__main__":
    main()
