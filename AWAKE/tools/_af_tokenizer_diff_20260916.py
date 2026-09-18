"""查清 emoji 那一条差异到底差在哪：HF 的 8105 是什么 token，C# 为什么把它丢了。

用法：
    C:/Users/26811/.workbuddy/binaries/python/envs/default/Scripts/python.exe \
        _af_tokenizer_diff_20260916.py
"""
import json
import os
import sys

AF = (r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
      r"\Modules\AnimusForge\ONNX")
SRC = os.path.join(AF, "tokenizer.json")
HERE = os.path.dirname(os.path.abspath(__file__))
VOCAB = os.path.join(HERE, "_af_tokenizer_20260916", "vocab.txt")


def main():
    from tokenizers import Tokenizer as HFTokenizer

    with open(SRC, "rb") as fh:
        obj = json.loads(fh.read().decode("utf-8"))

    tk = HFTokenizer.from_file(SRC)

    # 1) 8105 是什么
    print("token 8105 = %r" % tk.id_to_token(8105))
    print("token  100 = %r  ([UNK])" % tk.id_to_token(100))
    print()

    # 2) 一体化 json 里的三段配置
    for key in ("normalizer", "pre_tokenizer", "post_processor", "decoder"):
        seg = obj.get(key)
        if isinstance(seg, dict):
            seg = {k: v for k, v in seg.items() if k != "vocab"}
        print("%-14s = %s" % (key, json.dumps(seg, ensure_ascii=False)[:400]))
    print()
    model = {k: v for k, v in (obj.get("model") or {}).items() if k != "vocab"}
    print("%-14s = %s" % ("model", json.dumps(model, ensure_ascii=False)[:400]))
    print()

    # 3) 词表里有没有 emoji 本身 / 有没有字节级兜底
    with open(VOCAB, "r", encoding="utf-8") as fh:
        lines = [ln.rstrip("\n") for ln in fh]
    emoji = "\U0001F60E"
    print("词表是否含 emoji 本身 = %s" % (emoji in lines))
    print("词表里像字节兜底的 token = %s" % [t for t in lines if t.startswith("<0x")][:8])
    print("词表里 <...> 形式的特殊 token = %s" % [t for t in lines if t.startswith("<") and t.endswith(">")][:20])
    print()

    # 4) 逐段拆开看 emoji 是怎么走到 8105 的
    for text in [emoji, "a" + emoji, emoji + "锅盔", "表情" + emoji]:
        enc = tk.encode(text)
        print("%-10s -> ids=%s" % (text, enc.ids))
        print("%-10s    tokens=%s" % ("", enc.tokens))
    print()

    # 5) 关键问题：C# 丢的是"非 BMP 字符"，还是"所有符号"？做一组边界探针
    probes = {
        "汉字": "锅",
        "拉丁": "a",
        "数字": "1",
        "基本符号 ☆(U+2606)": "\u2606",
        "项目符号 •(U+2022)": "\u2022",
        "全角数字１(U+FF11)": "\uff11",
        "罗马数字 Ⅳ(U+2163)": "\u2163",
        "汉字扩展B 𠀀(U+20000)": "\U00020000",
        "emoji 😎(U+1F60E)": "\U0001F60E",
        "emoji ❤(U+2764)": "\u2764",
    }
    print("%-26s %-8s %s" % ("字符", "码点", "HF ids"))
    for name, ch in probes.items():
        enc = tk.encode(ch)
        print("%-26s %-8s %s" % (name, " ".join("U+%04X" % ord(c) for c in ch), enc.ids))
    return 0


if __name__ == "__main__":
    sys.exit(main())
