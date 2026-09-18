"""候选重排器一手账单：从 ModelScope 拉每个模型的 config.json，算参数量、f32 体积与 CPU 成本代理。

成本代理标定（都用本机实测值，20 条 × 512 token）：
    bge-small-zh-v1.5  4 层 × 512²  = 1,048,576  →  实测 0.59 s
    XLM-R base（AF 那份）12 层 × 768² = 7,077,888  →  实测 3.30 s
  比值 6.75 → 时间比值 5.6（同量级，故用「层数 × 宽度²」作代理，乘标定系数）

⚠️ 解码器结构（Qwen3 / Gemma / MiniCPM）不适用这个代理公式，单独标注。

产出：tools/_reranker_survey/bill.json
"""
import json
import os
import time
import urllib.request

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_reranker_survey")

MODELS = [
    ("BAAI", "bge-reranker-base"),
    ("BAAI", "bge-reranker-large"),
    ("BAAI", "bge-reranker-v2-m3"),
    ("BAAI", "bge-reranker-v2-gemma"),
    ("BAAI", "bge-reranker-v2-minicpm-layerwise"),
    ("netease-youdao", "bce-reranker-base_v1"),
    ("jinaai", "jina-reranker-v2-base-multilingual"),
    ("cross-encoder", "mmarco-mMiniLMv2-L12-H384-v1"),
    ("Qwen", "Qwen3-Reranker-0.6B"),
    ("Qwen", "Qwen3-Reranker-4B"),
    ("IEITYuan", "Yuan-embedding-2.0-zh"),
    ("BAAI", "bge-small-zh-v1.5"),
    ("iic", "gte_multilingual_reranker_base"),
]

DECODER_ARCHS = ("Qwen3ForCausalLM", "GemmaForCausalLM", "LayerWiseMiniCPMForCausalLM")

CAL_REF = 7077888.0       # XLM-R base：12 层 × 768²
CAL_TIME = 3.30           # 该模型本机实测 3.30 s（20 条 × 512）


def fetch_config(org, name):
    url = ("https://modelscope.cn/api/v1/models/%s/%s/repo?Revision=master&FilePath=config.json"
           % (org, name))
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(req, timeout=30) as resp:
        raw = resp.read().decode("utf-8")
    return json.loads(raw)


def bill(cfg):
    """按 config 算参数量（不含未列出的细节，误差 ~1%）。"""
    h = cfg.get("hidden_size") or cfg.get("d_model") or cfg.get("n_embd")
    layers = cfg.get("num_hidden_layers") or cfg.get("num_layers") or cfg.get("n_layer")
    ffn = cfg.get("intermediate_size") or cfg.get("n_inner") or (4 * h if h else None)
    vocab = cfg.get("vocab_size")
    heads = cfg.get("num_attention_heads") or cfg.get("n_head")
    pos = cfg.get("max_position_embeddings") or cfg.get("n_positions")
    if not (h and layers and vocab):
        return None

    # 注意力：Q/K/V/O 四个 h×h；FFN：两个 h×ffn
    per_layer = 4 * h * h + 2 * h * ffn + 4 * h
    emb = vocab * h
    pos_emb = (pos or 0) * h
    total = emb + layers * per_layer + pos_emb + h + 1
    return {
        "hidden": h, "layers": layers, "ffn": ffn, "vocab": vocab, "heads": heads,
        "max_pos": pos,
        "params": total,
        "f32_bytes": total * 4,
        "emb_share": emb / total,
        "cost_proxy": layers * h * h,
    }


def main():
    rows = []
    for org, name in MODELS:
        mid = org + "/" + name
        try:
            cfg = fetch_config(org, name)
        except Exception as exc:
            print("MISS  %-44s %r" % (mid, exc))
            continue
        b = bill(cfg)
        if not b:
            print("NOCFG %-44s keys=%s" % (mid, list(cfg.keys())[:10]))
            continue
        arch = (cfg.get("architectures") or [None])[0]
        b["id"] = mid
        b["arch"] = arch
        b["dtype"] = cfg.get("torch_dtype")
        b["is_decoder"] = arch in DECODER_ARCHS
        b["est_seconds_20x512"] = round(CAL_TIME * b["cost_proxy"] / CAL_REF, 2)
        rows.append(b)
        print("OK  %-42s %-40s h=%-5d L=%-3d vocab=%-7d %7.1fM  f32=%6.0f MB  估时=%6.2fs%s"
              % (mid, arch or "-", b["hidden"], b["layers"], b["vocab"],
                 b["params"] / 1e6, b["f32_bytes"] / 1024 / 1024,
                 b["est_seconds_20x512"], "  [解码器·估算不适用]" if b["is_decoder"] else ""))
        time.sleep(0.5)

    with open(os.path.join(OUT, "bill.json"), "w", encoding="utf-8") as fh:
        json.dump(rows, fh, ensure_ascii=False, indent=2)
    print()
    print("已写 bill.json（%d 条）" % len(rows))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
