# -*- coding: utf-8 -*-
"""反推 source_content_hash / quote_hash 的口径。
取 source-game-lore-war3.yaml 的 content_hash，与若干候选算法对拍。
"""
import hashlib
import io
import os

S = r"D:\AWAKE-Dev\AWAKE\tools\worldbook-studio\workspace\full-geo1\authoring\sources"
txt = io.open(os.path.join(S, "game-lore-war3.txt"), "rb").read()
want = "4ff820e7299c3d9fc4f55d812ae9d6e5c175d3beb4a3646b88652f22303c0d07"
print("目标 content_hash:", want)
print("原始字节数:", len(txt))

cands = {}
cands["sha256(raw)"] = hashlib.sha256(txt).hexdigest()
s_utf8 = txt.decode("utf-8")
cands["sha256(text)"] = hashlib.sha256(s_utf8.encode("utf-8")).hexdigest()
cands["sha256(text.rstrip())"] = hashlib.sha256(s_utf8.rstrip().encode("utf-8")).hexdigest()
cands["sha256(LF->CRLF)"] = hashlib.sha256(txt.replace(b"\n", b"\r\n")).hexdigest()
cands["sha256(text no-final-nl)"] = hashlib.sha256(s_utf8.rstrip("\n").encode("utf-8")).hexdigest()
# 去掉 BOM
if txt.startswith(b"\xef\xbb\xbf"):
    cands["sha256(noBOM)"] = hashlib.sha256(txt[3:]).hexdigest()

for k, v in cands.items():
    print("  %-26s %s  %s" % (k, v, "  <<< 命中" if v.lower() == want.lower() else ""))

print()
print("=== 若都不中，试 sha1/blake2 ===")
print("  sha1   ", hashlib.sha1(txt).hexdigest())
print("  blake2s", hashlib.blake2s(txt).hexdigest())
print("  长度比对: want=%d hex" % len(want))
