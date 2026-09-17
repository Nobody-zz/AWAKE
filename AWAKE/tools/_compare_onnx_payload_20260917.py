# -*- coding: utf-8 -*-
"""比对 AnimusForge 那份(外置权重 model.onnx + model.onnx_data) 与 Xenova 官方那份(单文件 model.onnx)。
判据不是"文件 hash 相同"(打包形态不同必然不同),而是:
  A) 外置权重 blob 是否能作为连续片段出现在单文件里 —— 是则说明权重逐字节同源;
  B) 两边的 SHA256 全表登记,供仓库记账。
"""
import hashlib
import os
import sys

GAME = r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
OLD = os.path.join(GAME, r"Modules\AWAKE\bin\Win64_Shipping_Client\Runtime\models\bge-small-zh-v1.5")
NEW = r"D:\AWAKE-Dev\.workbuddy\tmp\hf-official\xenova"

TEXT_FILES = ["config.json", "tokenizer.json", "tokenizer_config.json", "vocab.txt"]


def sha256(path, chunk=1 << 20):
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        while True:
            b = fh.read(chunk)
            if not b:
                break
            h.update(b)
    return h.hexdigest()


def row(label, path):
    if not os.path.exists(path):
        return "%-46s %s" % (label, "(缺失)")
    return "%-46s %12d  %s" % (label, os.path.getsize(path), sha256(path))


print("=" * 100)
print("一、哈希全表")
print("=" * 100)
print("[现有 / 取自 AnimusForge]  " + OLD)
for name in TEXT_FILES:
    print("  " + row(name, os.path.join(OLD, name)))
print("  " + row("model.onnx", os.path.join(OLD, "model.onnx")))
print("  " + row("model.onnx_data", os.path.join(OLD, "model.onnx_data")))
print()
print("[官方下载 / Xenova]  " + NEW)
for name in TEXT_FILES:
    print("  " + row(name, os.path.join(NEW, name)))
print("  " + row("model.onnx", os.path.join(NEW, "model.onnx")))
print("  " + row("model.onnx_data", os.path.join(NEW, "model.onnx_data")))
print()

print("=" * 100)
print("二、权重同源判定：model.onnx_data 是否为单文件 model.onnx 的连续片段")
print("=" * 100)
data = os.path.join(OLD, "model.onnx_data")
single = os.path.join(NEW, "model.onnx")
blob = open(data, "rb").read()
hay = open(single, "rb").read()
print("外置权重 blob 大小 = %d" % len(blob))
print("单文件总大小       = %d" % len(hay))
print("protobuf 开销差    = %d" % (len(hay) - len(blob)))
print()

# 采样若干块，逐块在单文件里找；块足够大(4KB)以排除偶然命中
import random
random.seed(20260917)
offsets = [0, len(blob) // 4, len(blob) // 2, len(blob) * 3 // 4, len(blob) - 4096]
offsets += [random.randrange(0, len(blob) - 4096) for _ in range(5)]
hit = 0
for off in sorted(set(offsets)):
    probe = blob[off:off + 4096]
    pos = hay.find(probe)
    ok = pos >= 0
    hit += 1 if ok else 0
    print("  偏移 %10d  ->  %s" % (off, ("命中于单文件偏移 %d" % pos) if ok else "**未命中**"))
print()
print("采样 %d 块，命中 %d 块" % (len(set(offsets)), hit))

# 整块包含判定（大文件 find 会占内存但可行）
whole = hay.find(blob)
print()
print("整段 model.onnx_data 在单文件中的起点 = %s" % (whole if whole >= 0 else "未整段命中"))
if whole >= 0:
    print("  => 权重逐字节同源：单文件 = [头部 protobuf %d 字节] + [权重 %d 字节] + [尾部 %d 字节]"
          % (whole, len(blob), len(hay) - whole - len(blob)))
