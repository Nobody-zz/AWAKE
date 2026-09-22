# -*- coding: utf-8 -*-
"""验项目 hash 算法：source_content_hash / quote_hash 到底怎么算的。"""
import hashlib, io, os

WS = r"D:\AWAKE-Dev\AWAKE\tools\worldbook-studio\workspace\full-geo1\authoring"

def sha(b):
    if isinstance(b, str): b = b.encode("utf-8")
    return hashlib.sha256(b).hexdigest().upper()

p = os.path.join(WS, "sources", "game-items-poc1.txt")
raw = io.open(p, "rb").read()
print("文件字节数:", len(raw))
print("含 BOM:", raw[:3] == b"\xef\xbb\xbf")
print("含 CRLF:", b"\r\n" in raw)
txt = raw.decode("utf-8-sig")
norm = txt.replace("\r\n", "\n").replace("\r", "\n")
print()
print("原样 SHA256          :", sha(raw))
print("剥 BOM+LF 归一 SHA256:", sha(norm))
print("目标(source_content)  : BAD48C3D287E339B729262826CBE5678F4C1AFB2BEF2D46C1EDDCCECB830CB6C")
print()
q = "盐 | Goods | 基准价 40"
print("quote 原样 SHA256    :", sha(q))
print("quote 目标            : 5FC17486E9E618E1C4AA8256372FA9DFA7F032F2A0EFEEE65B7ED988A7E3A954")
print()
# 找 poc1 里 salt 那一行
for line in norm.split("\n"):
    if "salt" in line:
        print("poc1 里的 salt 行:", line)
        print("  该行 SHA256:", sha(line))
        print("  截到第三段 SHA256:", sha(line.split(" | ")[1] + " | " + line.split(" | ")[2]))
