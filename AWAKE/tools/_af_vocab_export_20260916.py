"""从 HuggingFace tokenizer.json 导出 vocab.txt，并（若有 HF tokenizers）算标准答案。

背景：Microsoft.ML.Tokenizers 2.0.0 的公开 API 里，
      BertTokenizer / WordPieceTokenizer 的 Create 只接受 vocabFilePath（vocab.txt），
      没有任何方法能读一体化的 tokenizer.json。
      ⇒ 这是 AF 自写 BertNormalizer 的原因。
      ⇒ 绕开办法：tokenizer.json 的 model.vocab 里本来就有完整词表，离线导出成 vocab.txt 即可。

用法：
    python _af_vocab_export_20260916.py

产出：
    tools/_af_tokenizer_20260916/vocab.txt        按 id 排序，一行一个 token
    tools/_af_tokenizer_20260916/reference.json   若有 HF tokenizers，则含标准 token id 序列
"""
import json
import os
import sys

AF = (r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
      r"\Modules\AnimusForge\ONNX")
SRC = os.path.join(AF, "tokenizer.json")
OUTDIR = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                      "_af_tokenizer_20260916")

PROBES = [
    "锅盔",
    "圆顶锅盔",
    "那顶锅盔是啥玩意",
    "斯特吉亚的军队怎么打仗",
    "kettle helmet",
]


def main():
    os.makedirs(OUTDIR, exist_ok=True)
    with open(SRC, "rb") as fh:
        obj = json.loads(fh.read().decode("utf-8"))

    model = obj.get("model") or {}
    vocab = model.get("vocab")
    print("model.type = %s" % model.get("type"))
    print("vocab 类型 = %s" % type(vocab).__name__)

    if isinstance(vocab, dict):
        pairs = sorted(vocab.items(), key=lambda kv: kv[1])
    else:
        pairs = [(t, i) for i, t in enumerate(vocab)]

    print("词表条数 = %d" % len(pairs))
    print("前 6 项 = %s" % [t for t, _ in pairs[:6]])
    print("末 3 项 = %s" % [t for t, _ in pairs[-3:]])
    ids = [i for _, i in pairs]
    print("id 是否连续 0..n-1 = %s" % (ids == list(range(len(ids)))))
    print("id 最大值 = %d" % max(ids))
    print()

    out = os.path.join(OUTDIR, "vocab.txt")
    with open(out, "w", encoding="utf-8", newline="\n") as fh:
        for token, _ in pairs:
            fh.write(token + "\n")
    print("已写 vocab.txt: %s  (%d 行)" % (out, len(pairs)))
    print()

    ref = {"source": SRC, "model_type": model.get("type"), "results": {}}
    try:
        from tokenizers import Tokenizer as HFTokenizer
        tk = HFTokenizer.from_file(SRC)
        print("可复现性对照（HF tokenizers 算的标准答案）:")
        for text in PROBES:
            enc = tk.encode(text)
            ref["results"][text] = {"ids": enc.ids, "tokens": enc.tokens}
            print("  %-24s -> %s" % (text, enc.tokens))
    except Exception as exc:
        print("本机没有 HF tokenizers（%r），reference.json 只记导出信息。" % (exc,))
        ref["note"] = "HF tokenizers 不可用，未生成标准答案"

    refpath = os.path.join(OUTDIR, "reference.json")
    with open(refpath, "w", encoding="utf-8") as fh:
        json.dump(ref, fh, ensure_ascii=False, indent=2)
    print()
    print("已写 reference.json: %s" % refpath)
    return 0


if __name__ == "__main__":
    sys.exit(main())
