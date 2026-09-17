# -*- coding: utf-8 -*-
"""把游戏目录里的语义模型换成**官方可取、可校验**的那一份。

现状：5 件取自本机第三方模组 AnimusForge 的安装目录（无版本、无校验依据、换台机器就没有）。
新件：Xenova/bge-small-zh-v1.5 @ commit 75c43b069aac4d136ba6bc1122f995fedcfd2781，
      onnx/model.onnx 的 sha256 已与该仓库 LFS oid 逐字节对上。

定式：先备份 → 逐件拷到 .new → 校 sha256 → 原子改名 → 收尾复核。
判据不通过就整批不动。
"""
import hashlib
import os
import shutil
import sys

sys.stdout.reconfigure(encoding="utf-8")

GAME = r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
DST = os.path.join(GAME, r"Modules\AWAKE\bin\Win64_Shipping_Client\Runtime\models\bge-small-zh-v1.5")
SRC = r"D:\AWAKE-Dev\.workbuddy\tmp\hf-official\xenova"
BAK = r"D:\AWAKE-Dev\.workbuddy\tmp\game-model-backup-20260917"

# 官方来源逐件登记：期望 sha256（model.onnx 取自 Xenova 仓库 LFS oid，其余为本地实测）
EXPECT = {
    "model.onnx": "69a0b846f4f116b5e6aabf9546ea6754d02264f3211a13a1bd69b31b8040749a",
    "vocab.txt": "45bbac6b341c319adc98a532532882e91a9cefc0329aa57bac9ae761c27b291c",
    "config.json": "d4193ead3a810fd694fa8a31d7fc72fbaebc0668b603e398734bf2f6538ff42f",
    "tokenizer.json": "48cea5d44424912a6fd1ea647bf4fe50b55ab8b1e5879c3275f80e339e8fae26",
    "tokenizer_config.json": "e6f3b96db926a37d4039995fbf5ad17de158dfb8f6343d607e4dbaad18d75f5a",
}
# 换掉以后多出来的那个件（外置权重分片）—— 单文件版本自带权重，这份就冗余了
OBSOLETE = ["model.onnx_data"]


def sha256(path, chunk=1 << 20):
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        while True:
            b = fh.read(chunk)
            if not b:
                break
            h.update(b)
    return h.hexdigest()


print("=" * 96)
print("第 0 步 · 备份当前目录（含将被淘汰的 model.onnx_data）")
print("=" * 96)
os.makedirs(BAK, exist_ok=True)
for name in sorted(os.listdir(DST)):
    src = os.path.join(DST, name)
    if not os.path.isfile(src):
        continue
    dst = os.path.join(BAK, name)
    if os.path.exists(dst):
        print("  已有备份，跳过  %-28s" % name)
        continue
    shutil.copy2(src, dst)
    print("  备份          %-28s %12d  %s" % (name, os.path.getsize(dst), sha256(dst)[:16] + "..."))

print()
print("=" * 96)
print("第 1 步 · 逐件拷入并校验 sha256（校验不过不落位）")
print("=" * 96)
ok = True
for name, expect in EXPECT.items():
    src = os.path.join(SRC, name)
    if not os.path.exists(src):
        print("  ** 缺源件 **  " + name)
        ok = False
        continue
    got = sha256(src)
    match = got == expect
    ok = ok and match
    print("  %-24s %12d  %s  %s" % (name, os.path.getsize(src), got[:16] + "...", "MATCH" if match else "**MISMATCH**"))
if not ok:
    print()
    print("有件对不上，整批不动。")
    sys.exit(1)

print()
print("=" * 96)
print("第 2 步 · 落位（先写 .new 再原子改名）")
print("=" * 96)
for name in EXPECT:
    staging = os.path.join(DST, name + ".new")
    shutil.copy2(os.path.join(SRC, name), staging)
    if sha256(staging) != EXPECT[name]:
        print("  ** 落位后校验失败 **  " + name)
        sys.exit(1)
    os.replace(staging, os.path.join(DST, name))
    print("  替换          %-28s %12d" % (name, os.path.getsize(os.path.join(DST, name))))

for name in OBSOLETE:
    live = os.path.join(DST, name)
    if os.path.exists(live):
        print("  移出（冗余）  %-28s %12d" % (name, os.path.getsize(live)))
        os.replace(live, os.path.join(BAK, name + ".removed"))

print()
print("=" * 96)
print("第 3 步 · 收尾复核：目录最终状态")
print("=" * 96)
final = sorted(os.listdir(DST))
for name in final:
    p = os.path.join(DST, name)
    if os.path.isfile(p):
        print("  %-28s %12d  %s" % (name, os.path.getsize(p), sha256(p)[:16] + "..."))
    else:
        print("  %-28s %s" % (name, "<目录>"))

leftover = [n for n in final if n.endswith(".new")]
print()
print("残留 .new 件 = %s" % (leftover if leftover else "无"))
print("备份目录 = " + BAK)
