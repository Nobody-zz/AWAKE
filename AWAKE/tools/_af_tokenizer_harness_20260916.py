"""分词验收台：HF 原生（标准答案） vs 官方 BertTokenizer + 导出的 vocab.txt。

分两步跑（中间要插一次 C# 编译执行）：
    gen    -> 写 probes.json（探针清单，避免把中文塞进 C# 源码）
    check  -> 跑 HF 算标准答案，读 csharp.json，逐条比对，写 xcheck.json

探针分三组：
    A 组 真实查询：AWAKE 玩家可能真会打的问句（含标点/数字/中英混排）
    B 组 Unicode 类别：每个类别挑代表字符，用来倒推"哪一类会被丢"
    C 组 非 BMP：代理对字符，验证是不是只在非 BMP 上出问题
"""
import json
import os
import sys
import unicodedata

AF = (r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
      r"\Modules\AnimusForge\ONNX")
SRC = os.path.join(AF, "tokenizer.json")
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "_af_tokenizer_20260916")

# ---- A 组：真实查询 ----
A = [
    "圆顶锅盔是什么",
    "圆顶锅盔多少钱？",
    "斯特吉亚的军队怎么打仗？",
    "怎么打斯特吉亚",
    "你好，请问圆顶锅盔是什么？",
    "圆顶锅盔、板甲衣",
    "（圆顶锅盔）",
    "100 第纳尔",
    "kettle helmet 圆顶锅盔",
    "Roman helmet，多少钱",
    "阿雷尼科斯是谁",
    "涅雷采斯在哪",
    "锅盔!!!",
    "锅盔。。。",
    "「圆顶锅盔」",
    "半角,逗号。句号",
]

# ---- B 组：Unicode 类别代表 ----
B_CANDIDATES = [
    ("Lo 汉字", "锅"),
    ("Ll 小写拉丁", "a"),
    ("Lu 大写拉丁", "A"),
    ("Nd 半角数字", "1"),
    ("Nd 全角数字", "\uff11"),
    ("Nl 罗马数字", "\u2163"),
    ("No 上标二", "\u00b2"),
    ("Po 项目符号", "\u2022"),
    ("Po 中文逗号", "\uff0c"),
    ("Ps 左括号", "\uff08"),
    ("Sm 数学加号", "+"),
    ("Sm 数学乘号", "\u00d7"),
    ("Sc 货币", "\uffe5"),
    ("So 三角星", "\u2606"),
    ("So 扑克红心", "\u2665"),
    ("So emoji 心", "\u2764"),
    ("Cf 零宽空格", "\u200b"),
    ("Cc 控制符 TAB", "\t"),
    ("Zs 全角空格", "\u3000"),
    ("Pd 破折号", "\u2014"),
]

B = [t for _, t in B_CANDIDATES]
B_LABEL = {t: n for n, t in B_CANDIDATES}

# ---- C 组：非 BMP ----
C = [
    "\U0001F60E",   # 😎 So
    "\U00020000",   # 𠀀 CJK 扩展B Lo
    "\U0001D400",   # 𝐀 数学粗体 A Lu
    "\U0001F600",   # 😀 So
]


def cat(ch):
    cp = ord(ch)
    if cp > 0xFFFF:
        return "非BMP/" + unicodedata.category(ch)
    return unicodedata.category(ch)


def gen():
    os.makedirs(OUT, exist_ok=True)
    probes = A + B + C
    path = os.path.join(OUT, "probes.json")
    with open(path, "w", encoding="utf-8") as fh:
        json.dump(probes, fh, ensure_ascii=False, indent=2)
    print("已写 probes.json: %d 条 (A=%d B=%d C=%d)" % (len(probes), len(A), len(B), len(C)))
    for label, ch in B_CANDIDATES:
        print("  B  %-14s U+%04X  %s" % (label, ord(ch), cat(ch)))
    for ch in C:
        print("  C  %-14s U+%05X  %s" % ("非BMP", ord(ch), cat(ch)))
    return 0


def check():
    from tokenizers import Tokenizer as HFTokenizer

    probes = json.load(open(os.path.join(OUT, "probes.json"), encoding="utf-8"))
    csharp = json.load(open(os.path.join(OUT, "csharp.json"), encoding="utf-8"))
    tk = HFTokenizer.from_file(SRC)
    unk = tk.token_to_id("[UNK]")

    rows = []
    for text in probes:
        ids_hf = list(tk.encode(text).ids)
        ids_cs = csharp.get(text)
        tag = "A"
        if text in B:
            tag = "B"
        elif text in C:
            tag = "C"
        rows.append({
            "tag": tag,
            "label": B_LABEL.get(text, ""),
            "text": text,
            "cats": "".join(cat(c) for c in text),
            "hf": ids_hf,
            "cs": ids_cs,
            "match": ids_cs == ids_hf,
        })

    def content(ids):
        # 去掉 [CLS]/[SEP]，数内容 token
        return max(0, len(ids) - 2) if ids else 0

    print("=" * 96)
    print("A 组 真实查询（玩家可能真会打的）")
    print("=" * 96)
    a_bad = 0
    for r in rows:
        if r["tag"] != "A":
            continue
        mark = "OK " if r["match"] else "DIFF"
        if not r["match"]:
            a_bad += 1
        print("  %-4s %-24s HF=%s" % (mark, r["text"], r["hf"]))
        if not r["match"]:
            print("       %-24s CS=%s" % ("", r["cs"]))

    print()
    print("=" * 96)
    print("B 组 Unicode 类别（倒推丢字规律）")
    print("=" * 96)
    print("  %-8s %-16s %-6s %-6s %-6s %s" % ("码点", "类别/名称", "HF", "CS", "一致", "CS 是否整字丢失"))
    for r in rows:
        if r["tag"] != "B":
            continue
        cp = "U+%04X" % ord(r["text"])
        hf_c = content(r["hf"])
        cs_c = content(r["cs"])
        print("  %-8s %-16s %-6d %-6d %-6s %s"
              % (cp, r["cats"] + " " + r["label"], hf_c, cs_c,
                 "Y" if r["match"] else "N",
                 "LOST" if cs_c == 0 else ""))

    print()
    print("=" * 96)
    print("C 组 非 BMP")
    print("=" * 96)
    for r in rows:
        if r["tag"] != "C":
            continue
        print("  %-8s %-16s HF=%s  CS=%s  %s"
              % ("U+%05X" % ord(r["text"]), r["cats"], r["hf"], r["cs"],
                 "一致" if r["match"] else "不一致"))

    same = sum(1 for r in rows if r["match"])
    print()
    print("=" * 96)
    print("合计 %d / %d 逐位一致；A 组（真实查询）不一致 %d 条" % (same, len(rows), a_bad))
    verdict = "PASS" if a_bad == 0 else "FAIL"
    print("判定 = %s  （标准：A 组必须全对；B/C 组不一致只作记录）" % verdict)

    # 丢字清单：单字符输入，CS 内容为 0
    lost = [(("U+%04X" % ord(r["text"])), r["cats"], r["label"], r["hf"], r["cs"])
            for r in rows if len(r["text"]) == 1 and content(r["cs"]) == 0]
    print()
    print("C# 侧整字丢弃清单（%d 条）：" % len(lost))
    for cp, cats, label, hf, cs in lost:
        print("  %-8s %-16s %-14s HF=%s CS=%s" % (cp, cats, label, hf, cs))

    with open(os.path.join(OUT, "xcheck.json"), "w", encoding="utf-8") as fh:
        json.dump({
            "source": SRC, "verdict": verdict, "same": same, "total": len(rows),
            "a_bad": a_bad, "unk_id": unk,
            "lost_chars": [{"cp": c, "cats": k, "label": l, "hf": h, "cs": s}
                           for c, k, l, h, s in lost],
            "rows": rows,
        }, fh, ensure_ascii=False, indent=2)
    print("已写 xcheck.json")
    return 0 if verdict == "PASS" else 1


if __name__ == "__main__":
    sys.exit(gen() if (len(sys.argv) > 1 and sys.argv[1] == "gen") else check())
