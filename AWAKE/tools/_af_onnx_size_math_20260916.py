"""算 AF 那个 reranker 的参数账，解释 1.04 GiB 是怎么来的。"""

RERANKER = dict(vocab=250002, hidden=768, layers=12, ffn=3072, pos=514, heads=12)
BGE = dict(vocab=21128, hidden=512, layers=4, ffn=1536, pos=512, heads=8)

SIZES = {
    "reranker/model.onnx": 1112459588,
    "model.onnx + model.onnx_data": 41689 + 94765056,
}


def params(c):
    emb = c["vocab"] * c["hidden"]
    per_layer = 4 * c["hidden"] ** 2 + 2 * c["hidden"] * c["ffn"] + 4 * c["hidden"]
    layers = c["layers"] * per_layer
    extra = c["pos"] * c["hidden"] + c["hidden"] + 2 * c["hidden"]
    head = c["hidden"] + 1
    return emb, layers, extra, head, emb + layers + extra + head


print("=" * 78)
for name, c in (("reranker (XLM-R base)", RERANKER), ("bge-small-zh-v1.5", BGE)):
    emb, layers, extra, head, total = params(c)
    print("%s  vocab=%d hidden=%d layers=%d" % (name, c["vocab"], c["hidden"], c["layers"]))
    print("  词表嵌入      %14d  (%.1f%%)" % (emb, 100.0 * emb / total))
    print("  %2d 层        %14d  (%.1f%%)" % (c["layers"], layers, 100.0 * layers / total))
    print("  位置等        %14d  (%.1f%%)" % (extra, 100.0 * extra / total))
    print("  分类头        %14d" % head)
    print("  合计参数      %14d" % total)
    print("  float32 体积  %14.1f MB   (= 参数 x 4 字节)" % (total * 4 / 1024 / 1024))
    print("  int8 体积     %14.1f MB   (= 参数 x 1 字节)" % (total / 1024 / 1024))
    print()

print("=" * 78)
print("盘上实际文件大小：")
for k, v in SIZES.items():
    print("  %-32s %14d B  = %8.1f MB = %.2f GiB" % (k, v, v / 1024 / 1024, v / 1024 ** 3))
print()
tot = sum(SIZES.values())
print("  ONNX 目录合计                  %14d B  = %8.1f MB = %.2f GiB" % (tot, tot / 1024 / 1024, tot / 1024 ** 3))
print("  其中 reranker 占               %13.1f%%" % (100.0 * SIZES["reranker/model.onnx"] / tot))
print()
print("对比：两个模型参数量差 %.1f 倍" % (params(RERANKER)[4] / params(BGE)[4]))
print("      词表嵌入差     %.1f 倍" % (params(RERANKER)[0] / params(BGE)[0]))
print("      词表条数差     %.1f 倍" % (RERANKER["vocab"] / BGE["vocab"]))
