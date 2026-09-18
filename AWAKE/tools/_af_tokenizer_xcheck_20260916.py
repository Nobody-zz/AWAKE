"""交叉验证：HF tokenizers（原生） vs 我们导出的 vocab.txt + C# BertTokenizer。

为什么要这一步：
    C# 侧判据 1/2/3 全 PASS，只能证明"它自己稳定、句子之间不撞车"，
    不能证明"它和模型训练时用的分词一致"。
    分词只要和训练时不一致，向量就是错的，而且不会报错。
    ⇒ 必须拿原生实现当标准答案，逐条比 id。

用法：
    C:/Users/26811/.workbuddy/binaries/python/envs/default/Scripts/python.exe \
        _af_tokenizer_xcheck_20260916.py

产出：
    tools/_af_tokenizer_20260916/xcheck.json   逐条对比结果
"""
import json
import os
import sys

AF = (r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
      r"\Modules\AnimusForge\ONNX")
SRC = os.path.join(AF, "tokenizer.json")
HERE = os.path.dirname(os.path.abspath(__file__))
OUTDIR = os.path.join(HERE, "_af_tokenizer_20260916")
CSHARP = os.path.join(OUTDIR, "csharp.json")

EXPECTED = ["锅盔", "圆顶锅盔", "那顶锅盔是啥玩意", "斯特吉亚的军队怎么打仗",
            "kettle helmet", "张", "\U0001F60E"]


def main():
    from tokenizers import Tokenizer as HFTokenizer

    with open(CSHARP, "rb") as fh:
        csharp = json.loads(fh.read().decode("utf-8"))

    tk = HFTokenizer.from_file(SRC)
    print("HF tokenizer 已载入：%s" % SRC)
    print("词表大小 = %d" % tk.get_vocab_size())
    print()
    print("%-24s %-6s %s" % ("文本", "一致", "说明"))
    print("-" * 78)

    rows = []
    same = 0
    total = 0
    for text in EXPECTED:
        enc = tk.encode(text)
        ref_ids = list(enc.ids)
        got_ids = csharp.get(text)
        total += 1
        if got_ids is None:
            ok = False
            note = "C# 侧缺这条"
        elif got_ids == ref_ids:
            ok = True
            same += 1
            note = "id 逐位相同"
        else:
            ok = False
            note = "C#=%s  HF=%s" % (got_ids, ref_ids)
        print("%-24s %-6s %s" % (text, "YES" if ok else "NO", note))
        rows.append({
            "text": text,
            "hf_ids": ref_ids,
            "hf_tokens": enc.tokens,
            "csharp_ids": got_ids,
            "match": ok,
        })

    print("-" * 78)
    print("一致 %d / %d" % (same, total))

    verdict = "MATCH" if same == total else "MISMATCH"
    print("结论 = %s" % verdict)

    # 额外：把 [UNK] 和"整字被丢"这两件事也量出来
    unk_id = tk.token_to_id("[UNK]")
    drop = [r["text"] for r in rows if r["hf_ids"] == [101, 102]]
    unk_hits = [(r["text"], r["hf_ids"].count(unk_id)) for r in rows
                if unk_id in r["hf_ids"]]
    print()
    print("[UNK] id = %s" % unk_id)
    print("被整字丢弃（只剩 [CLS][SEP]）= %s" % (drop or "无"))
    print("含 [UNK] 的样本 = %s" % (unk_hits or "无"))

    out = {
        "source": SRC,
        "hf_vocab_size": tk.get_vocab_size(),
        "verdict": verdict,
        "same": same,
        "total": total,
        "unk_id": unk_id,
        "dropped_whole_input": drop,
        "unk_hits": unk_hits,
        "rows": rows,
    }
    path = os.path.join(OUTDIR, "xcheck.json")
    with open(path, "w", encoding="utf-8") as fh:
        json.dump(out, fh, ensure_ascii=False, indent=2)
    print()
    print("已写 xcheck.json: %s" % path)
    return 0 if verdict == "MATCH" else 1


if __name__ == "__main__":
    sys.exit(main())
