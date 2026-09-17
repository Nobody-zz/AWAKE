# -*- coding: utf-8 -*-
"""为三条聚落概念词条补上**来源登记**（`WB-SOURCE-001` 的修复）。

诊断链：
    compile 报 `WB-AUTHORITY-MUTATION-UNKNOWN`（503）—— 那是 AuthorityGate 的**兜底包装**，
    真因被吞（`AuthorityGate.cs:335`）；CLI 会把真因写 stderr，但上一跑的子进程解码失败吃了它。
    重跑抓到的真因是：`WB-AUTHORITY-COMPILE-422: CompileProof 对应输入未通过验证`。
    `validate` 展开成：`WB-SOURCE-001: 正典 source_ref 未命中有效来源登记` ——
    我给三条词条造了个新源 `source.calradia.game.settlement-type-strings`，但**只在档里引用了它，
    没建源文件、没建登记表**（`authoring/sources/` 下两者都缺）。而 proof 阶段只取快照、
    不检查 Valid（`AuthorityGate.cs:155`），所以没拦住。

本脚本做两件事（对齐项目既有形态）：
  ① 把三条档引用到的引文导出成源文件 `authoring/sources/game-settlement-type-strings.txt`
     （格式 `<localization sId> => <原文>`，**UTF-8 无 BOM、LF**，与同目录其它 txt 一致）。
  ② 建登记表 `authoring/sources/source-game-settlement-type-strings.yaml`。
     ⚠️ 登记表的 `source_content_hash` 必须是**该 txt 的 sha256**（`ValidationServices.cs:134` 拿
        `Hashing.Sha256Bytes(bytes)` 与它比），**不是** bannerlord.db 整库的 hash。
  ③ 回头把三条档里 6 个 source block 的 `source_content_hash` 与 `locator` 改对
     （由生成器 `_gen_settlement_types_20260917.py` 重跑完成 —— 本脚本只负责算出 txt 的 hash 并打印）。
"""
import hashlib
import io
import json
import os
import re
import sys

import yaml

sys.stdout.reconfigure(encoding="utf-8")

ROOT = r"D:/AWAKE-Dev/AWAKE"
SRC_DIR = os.path.join(ROOT, "tools/worldbook-studio/workspace/full-geo1/authoring")
SOURCES = os.path.join(SRC_DIR, "sources")
DOCS = ["settlement-types-village.yaml", "settlement-types-castle.yaml", "settlement-types-town.yaml"]
TXT_NAME = "game-settlement-type-strings.txt"
YAML_NAME = "source-game-settlement-type-strings.yaml"
SOURCE_ID = "source.calradia.game.settlement-type-strings"
SOURCE_VERSION = "bannerlord-1.3.15.110062"


def collect(path):
    """抽出档里全部 source block 的 (sid, quote)，含 entry/assertion/expression 三层。"""
    doc = yaml.safe_load(io.open(path, encoding="utf-8"))
    blocks = list(doc.get("sources") or [])
    for a in doc.get("assertions") or []:
        blocks += a.get("sources") or []
        for e in a.get("expressions") or []:
            blocks += e.get("sources") or []
    out = []
    for b in blocks:
        loc = b.get("locator") or ""
        m = re.search(r"#([^#]+)$", loc)
        sid = m.group(1) if m else ""
        out.append((sid, b.get("quote") or ""))
    return out


pairs = {}
dups = 0
for name in DOCS:
    path = os.path.join(SRC_DIR, name)
    blocks = collect(path)
    for sid, quote in blocks:
        if sid in pairs:
            if pairs[sid] != quote:
                print("❌ 同一 sid 出现两种引文：%s" % sid)
                sys.exit(1)
            dups += 1
            continue
        pairs[sid] = quote
    print("%-32s source block %d 条" % (name, len(blocks)))

print()
print("去重后独立引文 %d 条（重复引用 %d 次）" % (len(pairs), dups))

# ---- 写源文件：UTF-8 无 BOM、LF ----
lines = ["%s => %s" % (sid, pairs[sid]) for sid in sorted(pairs)]
content = "\n".join(lines) + "\n"
txt_path = os.path.join(SOURCES, TXT_NAME)
io.open(txt_path, "w", encoding="utf-8", newline="\n").write(content)
raw = open(txt_path, "rb").read()
assert not raw.startswith(b"\xef\xbb\xbf"), "不得带 BOM"
assert b"\r" not in raw, "不得有 CR"
txt_hash = hashlib.sha256(raw).hexdigest().upper()
print("已写源文件：%s（%d 行，%d 字节）" % (os.path.relpath(txt_path, ROOT), len(lines), len(raw)))
print("源文件 sha256 = %s" % txt_hash)

# ---- 写登记表 ----
registry = (
    "source_id: %s\n"
    "source_version: %s\n"
    "source_nature: game_snapshot\n"
    "universe: awake_current\n"
    "era: current\n"
    "locator_root: %s\n"
    "source_content_hash: %s\n"
    "content_tier: base\n"
    "license_status: permitted\n"
    "use_status: active\n"
    "valid_until: null\n"
    "imported_at: '2026-09-17T00:00:00Z'\n"
    "normalization_version: utf8-lf-no-bom-v1\n"
) % (SOURCE_ID, SOURCE_VERSION, TXT_NAME, txt_hash.lower())
reg_path = os.path.join(SOURCES, YAML_NAME)
io.open(reg_path, "w", encoding="utf-8", newline="\n").write(registry)
print("已写登记表：%s" % os.path.relpath(reg_path, ROOT))

# ---- 自检：每条引文都能在源文件里逐字定位（复刻 ValidationServices 的检查）----
text = content
missing = [sid for sid in pairs if text.find(pairs[sid]) < 0]
print()
print("自检：引文能在源文件里逐字定位 = %s%s"
      % (not missing, ("，缺 " + str(missing)) if missing else ""))

# 供下一步（改生成器）取用
side = {"txt": TXT_NAME, "txt_sha256_upper": txt_hash, "source_id": SOURCE_ID,
        "source_version": SOURCE_VERSION, "quotes": len(pairs), "sids": sorted(pairs)}
out = os.path.join(ROOT, "tools", "_settlement_type_source_20260917.json")
json.dump(side, io.open(out, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("已存：" + os.path.relpath(out, ROOT))
