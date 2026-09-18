"""AnimusForge 分发件清单与分词实现取证 —— 只读，不改动目标。

目的（回应甲方 2026-09-16 22:5x）：
    「自带 ONNX 运行库的 native 文件和分词实现」这套，AF 具体是怎么做的。

产出：
    1. AnimusForge.dll 里分词相关的类名 / 常量 —— 判定「读 tokenizer.json」还是「硬编码词表」
    2. onnxruntime 版本线索（托管封装与原生库同版本号）
    3. 语料 / 模型文件引用
    4. ONNX/tokenizer.json 的顶层结构（是不是 HuggingFace tokenizers 格式）

用法：
    python _af_dist_manifest_20260916.py
"""
import json
import os
import re
import sys

MOD = (r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
       r"\Modules\AnimusForge")
BIN = os.path.join(MOD, r"bin\Win64_Shipping_Client")
IMPL = os.path.join(BIN, r"versions\1.4\AnimusForge.dll")
ONNX = os.path.join(MOD, "ONNX")

UTF16_RE = re.compile(rb"(?:[\x20-\x7e]\x00){4,}")


def utf16_strings(path):
    with open(path, "rb") as fh:
        blob = fh.read()
    return {m.group().decode("utf-16-le", "replace")
            for m in UTF16_RE.finditer(blob)}


def show(title, items, limit=70):
    print("=" * 72)
    print(title + "   (共 %d 条)" % len(items))
    print("=" * 72)
    for it in sorted(items)[:limit]:
        print("  " + it)
    if len(items) > limit:
        print("  ... 省略 %d 条" % (len(items) - limit))
    print()


def main():
    if not os.path.exists(IMPL):
        print("找不到实现体: " + IMPL)
        return 1
    strings = utf16_strings(IMPL)

    tok_keys = ("Tokeniz", "Normalizer", "WordPiece", "Vocab", "Bert",
                "Unigram", "SentencePiece", "SpecialToken", "[CLS]", "[SEP]",
                "[MASK]", "[PAD]", "[UNK]")
    show("1) 分词相关字符串（判「自写」还是「用现成库」）",
         {x for x in strings if any(k in x for k in tok_keys)})

    show("2) onnxruntime / 版本线索",
         {x for x in strings
          if re.search(r"\b\d+\.\d{2}\.\d", x) or "onnxruntime" in x.lower()})

    show("3) 模型 / 语料文件引用",
         {x for x in strings
          if re.search(r"\.onnx|\.json$|knowledge|corpus|lore|reranker", x, re.I)})

    path = os.path.join(ONNX, "tokenizer.json")
    if os.path.exists(path):
        with open(path, "rb") as fh:
            raw = fh.read()
        print("=" * 72)
        print("4) %s   %d 字节" % (path, len(raw)))
        print("=" * 72)
        try:
            obj = json.loads(raw.decode("utf-8"))
            print("  顶层键: " + ", ".join(sorted(obj.keys())))
            model = obj.get("model") or {}
            if isinstance(model, dict):
                print("  model.type      = %s" % model.get("type"))
                print("  model.unk_id    = %s" % model.get("unk_id"))
                vocab = model.get("vocab") or []
                print("  model.vocab 条数 = %s" % len(vocab))
                if vocab:
                    head = [v[0] if isinstance(v, list) else v for v in vocab[:8]]
                    print("  vocab 前 8 项    = %s" % head)
            for key in ("normalizer", "pre_tokenizer", "post_processor", "decoder"):
                if key in obj:
                    print("  %-14s = %s" % (
                        key, json.dumps(obj[key], ensure_ascii=False)[:260]))
        except Exception as exc:
            print("  解析失败: %r" % (exc,))
        print()
    else:
        print("找不到 tokenizer.json: " + path)
    return 0


if __name__ == "__main__":
    sys.exit(main())
