# -*- coding: utf-8 -*-
"""别名收紧 · 分类清理（默认 dry-run；--apply 才写盘；--cls 选类，默认 C）。

三类（判据顺序 A → B → C）：
  A 含游戏实体 id（`castle_B1`／`village_ew1`…）——不是「叫法」，是内部代号
  B 别名 == 别档 title（跨档挂名）——堡档收下辖村名之类，是 09-14 设计，**默认保留**
  C 别名 == 本档 title（冗余）

已证（_alias_redundancy_20260920.py，阳性对照 482/482 全绿）：
  keywords 由 title → aliases → 锚点可读名 依次去重生成 ⇒ C 类别名对编译产物**零贡献**。
实测（_alias_impact_probe_20260920.py）：
  删 C 类 → 命中完全不变（移除 0 个 keywords 串）；删 A 类 → 拿 id 问「无命中」。

用法：python _alias_tighten_20260920.py --cls A          # 干跑
      python _alias_tighten_20260920.py --cls A --apply  # 写盘

schema 约束（awake.worldbook.authoring.v1.schema.json）：
  localized_aliases: minProperties=1，每个语言段必须是 array ⇒
  ① 语言段删空 ⇒ 删该段行；② 两段全空 ⇒ 删整个 aliases 行（aliases 非 required）。
行级删除，不重排 yaml。改动档 revision += 1。
"""
import io
import os
import re
import sys
from collections import Counter, defaultdict

import yaml

WS = r"D:/AWAKE-Dev/AWAKE/tools/worldbook-studio/workspace/full-geo1/authoring"
AO = r"D:/AWAKE-Dev/AWAKE/docs/worldbook-migration/projection/authoring-out"
APPLY = "--apply" in sys.argv
CLS = "C"
for _i, _a in enumerate(sys.argv):
    if _a == "--cls" and _i + 1 < len(sys.argv):
        CLS = sys.argv[_i + 1].upper()

RE_ALIAS_KEY = re.compile(r"^aliases:\s*$")
RE_LANG = re.compile(r"^(\s*)(zh-CN|en):\s*$")
RE_ITEM = re.compile(r"^(\s*-\s*)(.*?)\s*$")
PAT_ID = re.compile(r"(village_|castle_|town_|Settlement\.|Culture\.|Clan\.|Kingdom\.|NPCCharacter\.|Item\.|_EW\d|_ES\d|_B\d+_|_S\d+_|_[A-Z]{2}\d+_\d+)")


def title_vals(d):
    t = d.get("title") or {}
    return set(x.strip() for x in (t.get("zh-CN"), t.get("en")) if isinstance(x, str) and x.strip())


def targets_in(d, title2docs, cls):
    """本档 aliases 里属于 `cls`（A/B/C 任意组合）的值集合"""
    tv = title_vals(d)
    did = d.get("id")
    out = set()
    al = d.get("aliases") or {}
    for lang in ("zh-CN", "en"):
        for v in (al.get(lang) or []):
            if not isinstance(v, str) or not v.strip():
                continue
            s = v.strip()
            if PAT_ID.search(s):
                hit = "A"
            elif s in title2docs and did not in title2docs[s]:
                hit = "B"
            elif s in tv:
                hit = "C"
            else:
                hit = None
            if hit and hit in cls:
                out.add(s)
    return out


def _unquote(raw):
    if len(raw) >= 2 and raw[0] == raw[-1] and raw[0] in "'\"":
        return raw[1:-1]
    return raw


def strip_aliases(text, drop):
    """返回 (新文本, 删了几条值, 是否删了语言段头, 是否删了 aliases 行)"""
    lines = text.split("\n")
    i = None
    for k, ln in enumerate(lines):
        if RE_ALIAS_KEY.match(ln):
            i = k
            break
    if i is None:
        return text, 0, 0, 0
    j = i + 1
    while j < len(lines) and lines[j].startswith(" "):
        j += 1
    body = lines[i + 1:j]

    removed = 0
    out_body = []
    cur_lang = None
    for ln in body:
        m_lang = RE_LANG.match(ln)
        if m_lang:
            cur_lang = m_lang.group(2)
            out_body.append(ln)
            continue
        m_item = RE_ITEM.match(ln)
        if m_item and cur_lang:
            if _unquote(m_item.group(2)) in drop:
                removed += 1
                continue
        out_body.append(ln)

    # 删空语言段头
    lang_idx = [k for k, ln in enumerate(out_body) if RE_LANG.match(ln)]
    final, n_head = [], 0
    for k, ln in enumerate(out_body):
        if RE_LANG.match(ln):
            nxt = next((x for x in lang_idx if x > k), len(out_body))
            seg = out_body[k + 1:nxt]
            if not any(RE_ITEM.match(s) for s in seg):
                n_head += 1
                continue
        final.append(ln)

    if not final:
        return "\n".join(lines[:i] + lines[j:]), removed, n_head, 1
    return "\n".join(lines[:i + 1] + final + lines[j:]), removed, n_head, 0


def main():
    stat = Counter()
    dom = Counter()
    per_doc = []
    files = []
    for root in (WS, AO):
        for f in sorted(os.listdir(root)):
            if f.endswith(".yaml"):
                files.append((root, f))
    # 先建 title2docs（判 B 类需要全库视野）
    title2docs = defaultdict(list)
    for root, f in files:
        try:
            d = yaml.safe_load(io.open(os.path.join(root, f), encoding="utf-8"))
        except Exception:
            continue
        if isinstance(d, dict) and str(d.get("id", "")).startswith("doc."):
            t = (d.get("title") or {}).get("zh-CN", "")
            if t:
                title2docs[t].append(d["id"])
    print("title2docs 建好：%d 个标题（扫 %d 个文件）" % (len(title2docs), len(files)))
    written = []
    for root, f in files:
        path = os.path.join(root, f)
        try:
            text = io.open(path, encoding="utf-8").read()
            d = yaml.safe_load(text)
        except Exception:
            stat["读失败"] += 1
            continue
        if not isinstance(d, dict) or not str(d.get("id", "")).startswith("doc."):
            continue
        drop = targets_in(d, title2docs, CLS)
        if not drop:
            continue
        new_text, n, n_head, n_all = strip_aliases(text, drop)
        if n == 0:
            stat["解析到但未删到"] += 1
            continue
        if APPLY:
            new_text = re.sub(r"^revision:\s*(\d+)\s*$",
                              lambda m: "revision: %d" % (int(m.group(1)) + 1),
                              new_text, count=1, flags=re.M)
            # 写前自检：能解析、aliases 合法
            try:
                chk = yaml.safe_load(new_text)
                al = chk.get("aliases")
                if al is not None:
                    assert isinstance(al, dict) and len(al) >= 1, "aliases 段非法"
                    for lang, arr in al.items():
                        assert isinstance(arr, list) and len(arr) >= 1, "语言段非法/空 %s" % lang
            except Exception as ex:
                stat["写前自检失败"] += 1
                print("  !! 跳过 %s：%s" % (d.get("id"), ex))
                continue
            io.open(path, "w", encoding="utf-8", newline="\n").write(new_text)
            written.append(d["id"])
        stat["命中档"] += 1
        stat["删除值"] += n
        stat["删语言段头"] += n_head
        stat["删整个aliases"] += n_all
        dom[str(d.get("id")).split(".")[1]] += 1
        per_doc.append((d["id"], n, sorted(drop)))

    print("模式：%s　类别：%s" % ("APPLY（已写盘）" if APPLY else "DRY-RUN（未写盘）", CLS))
    print("命中档 %d（双侧合计）；删除值 %d；删语言段头 %d；删整个 aliases 段 %d" % (
        stat["命中档"], stat["删除值"], stat["删语言段头"], stat["删整个aliases"]))
    if APPLY:
        print("实际写盘档 %d；写前自检失败 %d" % (len(written), stat["写前自检失败"]))
    print("按域：%s" % dict(dom.most_common()))
    print("=" * 76)
    for did, n, vals in sorted(per_doc, key=lambda x: -x[1])[:12]:
        print("  %-46s %d  %s" % (did, n, vals[:4]))


main()
