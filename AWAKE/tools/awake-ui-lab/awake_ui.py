#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
AWAKE UI 工作台 · 统一入口（只读）

一句话
    把「几何预览 / 布局审计 / 截图 / C# 流程验证 / 原版取证」串成一条命令，
    产出一份统一报告——人和 Agent 读的是同一份东西。

它不替代任何底层工具：
    ① 几何内核  → preview/preview_prefab_geometry.py（布局 / 审计 / 截图）
    ② 流程验证  → tests/Awake.UiLab.Tests（状态 / E2 / 生命周期契约）
    ③ 原版取证  → 原版官方模块的 XML（绑定 / 命令 / Brush 引用）
它做的是「接线」：一个入口、一份报告、一条闭环。

三条线的接线状态写在报告的 lines 字段里（geometry / flow / native）：
    wired      跑通了
    error      尝试了但失败（原因写在 flow.error / native 的 notes 里）
    not-wired  被显式关掉（--no-flow / --no-native）

诚实边界
    报告里的几何是**推导几何**，不是渲染结果。颜色 / 字体 / 贴图不还原。

用法
    python awake_ui.py check                       # 全套：几何 + 审计 + 截图 + 流程 + 取证
    python awake_ui.py check --prefab NpcDialogue.xml
    python awake_ui.py check --no-shot             # 不截图（更快）
    python awake_ui.py check --no-flow             # 不跑 dotnet（最快）
    python awake_ui.py check --no-native           # 不做原版取证
    python awake_ui.py check --diff                # 顺带和上次快照比一比
    python awake_ui.py check --json                # 只把报告打到 stdout
    python awake_ui.py diff                        # 与上一次快照比，看这次改了什么
"""

import argparse
import datetime
import io
import json
import math
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ENGINE_DIR = os.path.join(HERE, "preview")
if ENGINE_DIR not in sys.path:
    sys.path.insert(0, ENGINE_DIR)

import preview_prefab_geometry as geo  # noqa: E402

SCHEMA = "awake-ui-report.v1"
GENERATOR = "awake-ui 1"
DEFAULT_OUT = os.path.join(HERE, "out")
DEFAULT_PREFAB_DIR = os.path.normpath(os.path.join(HERE, "..", "..", "GUI", "Prefabs"))
# 独立分发包（脱离仓库解压）时，主目录不存在则退回包内自带的样例 Prefab，
# 保证开箱即可 `awake_ui.py check`，无需手动指定 --prefab-dir。
_SAMPLES_PREFAB_DIR = os.path.join(HERE, "samples", "prefabs")
if not os.path.isdir(DEFAULT_PREFAB_DIR) and os.path.isdir(_SAMPLES_PREFAB_DIR):
    DEFAULT_PREFAB_DIR = _SAMPLES_PREFAB_DIR

# 「原版基准」＝ TaleWorlds 官方模块，不含任何第三方 Mod。
# 实测本机官方模块的 Prefab 数：Native 148 + SandBox 177 + Multiplayer 98
# + SandBoxCore 5 + StoryMode 2 ≈ 430（与 docs 里 436 的口径一致）。
NATIVE_MODULES = ("Native", "SandBox", "SandBoxCore", "StoryMode", "Multiplayer")

# Modules 目录探测（只读；找不到就退化成 not-wired，不算失败）
BANNERLORD_MODULES_CANDIDATES = (
    "D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules",
    "C:/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules",
    "C:/Program Files/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules",
    "E:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules",
    "F:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules",
)


def find_native_roots(explicit=None):
    """定位原版 Prefab / Brush 目录，返回 (prefab_roots, brush_roots)。

    explicit 可以给 Modules 目录（自动展开官方模块），也可以直接给某个
    Prefabs 目录（单根，Brushes 由上溯 Modules 推得）。都找不到时返回 ([], [])。
    """
    modules_dir = None
    if explicit:
        p = os.path.normpath(explicit)
        if os.path.basename(p).lower() == "prefabs":
            return [p], [_sibling_brushes(p)]
        modules_dir = p if os.path.isdir(p) else None
    if modules_dir is None:
        env = os.environ.get("AWAKE_UILAB_NATIVE")
        if env and os.path.isdir(env):
            modules_dir = os.path.normpath(env)
        else:
            for c in BANNERLORD_MODULES_CANDIDATES:
                if os.path.isdir(c):
                    modules_dir = c
                    break
    if not modules_dir:
        return [], []

    prefab_roots, brush_roots = [], []
    for m in NATIVE_MODULES:
        pf = os.path.join(modules_dir, m, "GUI", "Prefabs")
        br = os.path.join(modules_dir, m, "GUI", "Brushes")
        if os.path.isdir(pf):
            prefab_roots.append(pf)
        if os.path.isdir(br):
            brush_roots.append(br)
    return prefab_roots, brush_roots


def _sibling_brushes(prefabs_dir):
    parent = os.path.dirname(os.path.normpath(prefabs_dir))
    cand = os.path.join(parent, "Brushes")
    return cand if os.path.isdir(cand) else os.path.join(parent, "Brushes")


def _own_brush_names(prefabs_dir):
    """扫描 Prefabs 同级 Brushes 目录，返回**本模组自有** Brush 名集合。

    为什么需要：报告里「Brush 未命中」原先只拿原版 brush 库比对 ⇒ 自有的
    `Awake.*` 一律被算成未命中，看着像坏了。分开统计后，「未命中」才是真坏引用。
    """
    out = set()
    d = _sibling_brushes(prefabs_dir)
    if not d or not os.path.isdir(d):
        return out
    for dirpath, _dn, filenames in os.walk(d):
        for fn in filenames:
            if not fn.lower().endswith(".xml"):
                continue
            try:
                with io.open(os.path.join(dirpath, fn), "r",
                             encoding="utf-8", errors="replace") as fh:
                    txt = fh.read()
            except Exception:  # noqa: BLE001
                continue
            for m in RE_BRUSH_DEF.finditer(txt):
                out.add(m.group(1))
    return out


# ---------------------------------------------------------------- 小工具

def _now():
    return datetime.datetime.now().astimezone().replace(microsecond=0).isoformat()


def _rel(path, base):
    return os.path.relpath(path, base).replace("\\", "/")


def _stamp(iso):
    return iso.replace(":", "").replace("-", "")


def _digest(prefab_report):
    """把一堆几何矩形压成可比较的指纹——用于 diff 时判断"结构变没变"。"""
    geo_list = prefab_report.get("geometry") or []
    by_type, by_width = {}, {}
    max_depth = 0
    for g in geo_list:
        t = g.get("tag", "?")
        by_type[t] = by_type.get(t, 0) + 1
        max_depth = max(max_depth, int(g.get("depth", 0)))
        w = int(round(g.get("w", 0)))
        by_width[w] = by_width.get(w, 0) + 1
    top_types = dict(sorted(by_type.items(), key=lambda kv: (-kv[1], kv[0]))[:10])
    top_widths = dict(sorted(by_width.items(), key=lambda kv: (-kv[1], kv[0]))[:10])
    layout = sorted("%s@%d:%g,%g,%g,%g" % (
        g.get("tag", "?"), int(g.get("depth", 0)),
        round(g.get("x", 0), 1), round(g.get("y", 0), 1),
        round(g.get("w", 0), 1), round(g.get("h", 0), 1)) for g in geo_list)
    return {
        "widget_count": len(geo_list),
        "max_depth": max_depth,
        "by_type": top_types,
        "width_histogram": dict((str(k), v) for k, v in top_widths.items()),
        "layout": layout,
    }


# ---------------------------------------------------------------- native 取证
#
# 从 Prefab XML 里抽「绑定 / 命令 / 节点 Id」三样事实，再拿原版对应面板做对照。
# 纯文本正则，不加载 Gauntlet、不启动游戏——和 C# 侧 UiLabPrefabAudit 同一套口径，
# 只是这里跑在 Python 侧，好让统一报告一次拿全。

RE_BINDING_BRACE = re.compile(r"\{([A-Za-z_][A-Za-z0-9_]*)\}")
RE_BINDING_AT = re.compile(r"@([A-Za-z_][A-Za-z0-9_]*)")
RE_COMMAND = re.compile(r"Command\.[A-Za-z_][A-Za-z0-9_]*\s*=\s*\"([A-Za-z_][A-Za-z0-9_]*)\"")
RE_NODE_ID = re.compile(r"\bId\s*=\s*\"([^\"]+)\"")
RE_BRUSH_REF = re.compile(r"\bBrush=\"([A-Za-z_][A-Za-z0-9_.]*)\"")
RE_BRUSH_DEF = re.compile(r"<Brush\b[^>]*\bName=\"([^\"]+)\"")


def extract_contract(xml_text):
    """从 Prefab XML 文本抽绑定/命令/Id。绑定含 {Name} 与 @Name 两种写法。"""
    def uniq(seq):
        out, seen = [], set()
        for v in seq:
            if v not in seen:
                seen.add(v)
                out.append(v)
        return out

    bindings = uniq(RE_BINDING_BRACE.findall(xml_text) + RE_BINDING_AT.findall(xml_text))
    return {
        "bindings": bindings,
        "commands": uniq(RE_COMMAND.findall(xml_text)),
        "node_ids": uniq(RE_NODE_ID.findall(xml_text)),
    }


def _jaccard(a, b):
    sa, sb = set(a), set(b)
    if not sa and not sb:
        return 0.0
    return len(sa & sb) / float(len(sa | sb))


class NativeIndex(object):
    """原版 Prefab / Brush 的只读索引：按文件名查找 + 按绑定相似度找最近邻。"""

    def __init__(self, prefab_roots, brush_roots=None):
        self.prefab_roots = list(prefab_roots)
        self.brush_roots = list(brush_roots or [])
        self.by_name = {}
        self.by_rel = {}
        self.brush_defs = {}
        self.base = ""
        self._contract_cache = {}
        self._df = {}
        self._doc_count = 1
        self._scanned = False

    def scan(self):
        if self._scanned:
            return self
        try:
            self.base = os.path.commonpath(self.prefab_roots + self.brush_roots)
        except ValueError:  # 跨盘符等
            self.base = ""
        for root in self.prefab_roots:
            for dirpath, _dirnames, filenames in os.walk(root):
                for fn in filenames:
                    if not fn.lower().endswith(".xml"):
                        continue
                    full = os.path.join(dirpath, fn)
                    self.by_name.setdefault(fn, []).append(full)
                    self.by_rel[self._rel(full)] = full
        for root in self.brush_roots:
            for dirpath, _dirnames, filenames in os.walk(root):
                for fn in filenames:
                    if not fn.lower().endswith(".xml"):
                        continue
                    full = os.path.join(dirpath, fn)
                    try:
                        with io.open(full, "r", encoding="utf-8", errors="replace") as fh:
                            txt = fh.read()
                    except Exception:  # noqa
                        continue
                    for m in RE_BRUSH_DEF.finditer(txt):
                        self.brush_defs.setdefault(m.group(1), self._rel(full))
        # 文档频率：用于给「到处都在用的通用命令」降权（ExecuteClose 之类）
        self._df = {}
        for rel, path in self.by_rel.items():
            c = self.contract_of(path)
            for t in set(list(c["commands"]) + list(c["bindings"])):
                self._df[t] = self._df.get(t, 0) + 1
        self._doc_count = max(1, len(self.by_rel))
        self._scanned = True
        return self

    def _weight(self, token):
        """IDF 权重：稀有 token 高、遍地都是的通用 token 低。"""
        df = self._df.get(token, 0)
        return math.log((self._doc_count + 1.0) / (df + 1.0)) + 1.0

    def _rel(self, path):
        if self.base:
            return os.path.relpath(path, self.base).replace("\\", "/")
        return os.path.basename(path)

    def contract_of(self, path):
        if path not in self._contract_cache:
            try:
                with io.open(path, "r", encoding="utf-8", errors="replace") as fh:
                    self._contract_cache[path] = extract_contract(fh.read())
            except Exception:  # noqa
                self._contract_cache[path] = {"bindings": [], "commands": [], "node_ids": []}
        return self._contract_cache[path]

    def exact(self, prefab_name):
        hits = self.by_name.get(prefab_name) or []
        if not hits:
            return None
        path = hits[0]
        c = self.contract_of(path)
        return {"found": True, "name": prefab_name, "path": self._rel(path),
                "same_name_count": len(hits),
                "bindings": c["bindings"], "commands": c["commands"],
                "node_ids": c["node_ids"]}

    def nearest(self, contract, limit=3):
        """按「命令 + 绑定」集合找原版里最像的面板。

        两处防噪声：
        - 排序用 **IDF 加权**相似度而非裸 Jaccard——`ExecuteClose` 这种上百个
          面板都在用的通用命令权重极低，不会把小面板顶上来；
        - 同时返回 `shared`（原始共享项数）与 `shared_commands`，让人能自己判断。
        """
        probe = list(contract.get("commands", [])) + list(contract.get("bindings", []))
        if not probe:
            return []
        self.scan()
        pset = set(probe)
        scored = []
        for rel, path in self.by_rel.items():
            c = self.contract_of(path)
            cand = set(list(c["commands"]) + list(c["bindings"]))
            inter = pset & cand
            if not inter:
                continue
            union = pset | cand
            den = sum(self._weight(t) for t in union)
            wscore = (sum(self._weight(t) for t in inter) / den) if den else 0.0
            scored.append((-wscore, rel, c, len(inter)))
        scored.sort()
        out = []
        for _w, rel, c, shared in scored[:limit]:
            out.append({
                "name": os.path.basename(rel), "path": rel,
                "shared": shared,
                "score": round(_jaccard(probe, list(c["commands"]) + list(c["bindings"])), 4),
                "weighted_score": round(-_w, 4),
                "shared_commands": sorted(pset & set(c["commands"])),
                "shared_bindings": sorted(pset & set(c["bindings"])),
                "bindings": c["bindings"], "commands": c["commands"],
            })
        return out

    def resolve_brushes(self, names):
        """把 Brush 引用解析到原版定义文件；返回 (已解析, 未在原版找到)。"""
        self.scan()
        resolved, missing = {}, []
        for n in names:
            p = self.brush_defs.get(n)
            if p:
                resolved[n] = p
            else:
                missing.append(n)
        return resolved, missing


def _set_delta(base, ref):
    """返回 (只在 base 里有的, 只在 ref 里有的)。"""
    sb, sr = set(base), set(ref)
    return sorted(sb - sr), sorted(sr - sb)


def build_native(name, contract, native_index, brush_names=None, own_brush_names=None):
    """组装单个 prefab 的 native 取证块。"""
    brush_names = list(brush_names or [])
    own = set(own_brush_names or ())
    block = {
        "source": "local-xml",
        "awake": contract,
        "exact": None,
        "nearest": [],
        "brushes": {"referenced": brush_names, "own": [], "resolved": {}, "missing": []},
        "notes": [],
    }
    if native_index is None:
        block["notes"].append("未找到原版 Modules 目录 —— 用 --native-dir 指定，"
                              "或设环境变量 AWAKE_UILAB_NATIVE")
        if own:
            block["brushes"]["own"] = sorted(n for n in brush_names if n in own)
        return block

    ex = native_index.exact(name)
    block["exact"] = ex if ex else {"found": False, "name": name}

    near = native_index.nearest(contract, limit=3)
    if near:
        top = near[0]
        only_a_cmd, only_n_cmd = _set_delta(contract["commands"], top["commands"])
        only_a_bind, only_n_bind = _set_delta(contract["bindings"], top["bindings"])
        top["delta"] = {
            "commands_only_awake": only_a_cmd,
            "commands_only_native": only_n_cmd,
            "bindings_only_awake": only_a_bind,
            "bindings_only_native": only_n_bind,
        }
    block["nearest"] = near

    # 自有 Brush 先摘出去：它们不在原版库里，不该算「未命中」
    own_hits = sorted(n for n in brush_names if n in own)
    block["brushes"]["own"] = own_hits
    rest = [n for n in brush_names if n not in own]
    resolved, missing = native_index.resolve_brushes(rest)
    block["brushes"]["resolved"] = resolved
    block["brushes"]["missing"] = sorted(missing)

    if block["exact"].get("found"):
        block["notes"].append("原版存在同名 Prefab —— 可直接对照。")
    elif near:
        if near[0].get("weighted_score", 0) < 0.25:
            block["notes"].append(
                "原版没有绑定/命令重合度高的面板（加权最高 %.2f、仅共享 %d 项）——"
                "AWAKE 用的是自建绑定命名，属预期；nearest 仅供参考，不代表功能对应。"
                % (near[0]["weighted_score"], near[0]["shared"]))
        else:
            block["notes"].append("原版无同名 Prefab；nearest 是按绑定/命令共享项找的功能对应面板。")
    else:
        block["notes"].append("原版没有绑定/命令重合的面板 —— AWAKE 自建面板。")
    if missing:
        block["notes"].append("%d 个 Brush 未在官方 Brushes 中命中（可能由 AWAKE 自己定义，"
                              "或名字拼写不同）：%s" % (len(missing), ", ".join(sorted(missing)[:6])))
    return block


# ---------------------------------------------------------------- diff

def _issue_key(i):
    """问题唯一键，必须带位置。

    只用 widget+attr+value 是不够的：同一文件里两个控件写了同样的越格值会
    互相掩盖——12 改成 13 两边都越格、总数不变，diff 曾因此静默漏检。
    有了 file+path，diff 才能说清「哪一处修好了 / 哪一处新引入」。
    """
    place = i.get("path") or i.get("widget", "")
    return "%s  %s  %s=%s  (%s)" % (i.get("file", ""), place, i.get("attr", ""),
                                    i.get("value", ""), i.get("rule", ""))


def diff_reports(old, new):
    """比较两份报告，输出人和 Agent 都读得懂的差异。"""
    changes = []
    old_by = dict((p["name"], p) for p in old.get("prefabs", []))
    new_by = dict((p["name"], p) for p in new.get("prefabs", []))
    for name in sorted(set(old_by) | set(new_by)):
        o, n = old_by.get(name), new_by.get(name)
        if o is None:
            changes.append({"prefab": name, "kind": "added"})
            continue
        if n is None:
            changes.append({"prefab": name, "kind": "removed"})
            continue
        deltas = []
        if o.get("panel") != n.get("panel"):
            deltas.append({"field": "panel", "from": o.get("panel"), "to": n.get("panel")})
        if o.get("widget_count") != n.get("widget_count"):
            deltas.append({"field": "widget_count",
                           "from": o.get("widget_count"), "to": n.get("widget_count")})
        if o.get("issue_count") != n.get("issue_count"):
            deltas.append({"field": "issue_count",
                           "from": o.get("issue_count"), "to": n.get("issue_count")})
        ol = set(o.get("digest", {}).get("layout", []))
        nl = set(n.get("digest", {}).get("layout", []))
        if ol != nl:
            deltas.append({"field": "layout_changed",
                           "added": sorted(nl - ol)[:6], "removed": sorted(ol - nl)[:6],
                           "added_count": len(nl - ol), "removed_count": len(ol - nl)})
        ok = dict((_issue_key(i), i) for i in o.get("issues", []))
        nk = dict((_issue_key(i), i) for i in n.get("issues", []))
        fixed = [k for k in ok if k not in nk]
        added = [k for k in nk if k not in ok]
        if fixed:
            deltas.append({"field": "issues_fixed", "count": len(fixed), "sample": fixed[:8]})
        if added:
            deltas.append({"field": "issues_new", "count": len(added), "sample": added[:8]})
        if deltas:
            changes.append({"prefab": name, "kind": "changed", "deltas": deltas})
    return changes


def _snapshot_dir(out):
    return os.path.join(out, "snapshots")


def _load_prev_snapshot(snap_dir, current_name):
    if not os.path.isdir(snap_dir):
        return None, None
    names = sorted(f for f in os.listdir(snap_dir)
                   if f.endswith(".json") and f != current_name)
    if not names:
        return None, None
    name = names[-1]
    with io.open(os.path.join(snap_dir, name), "r", encoding="utf-8") as fh:
        return json.load(fh), name


# ---------------------------------------------------------------- flow 接线
#
# 让 C# 侧的本地 Lab（E2 生命周期用例 + 全量 fixture 驱动）把结果写成 JSON，
# 由这里读进统一报告。进程内跑 dotnet；失败不致命，只把 flow 标成 error。

DOTNET_TIMEOUT = 420


def run_flow(lab_root, out_dir, timeout=DOTNET_TIMEOUT):
    """跑一次 C# 本地 Lab，返回 flow 结果 dict（失败也返回 dict，不抛）。"""
    proj = os.path.join(lab_root, "tests", "Awake.UiLab.Tests")
    if not os.path.isdir(proj):
        return {"ok": False, "error": "project_not_found", "path": proj}
    flow_path = os.path.join(out_dir, "flow.json")
    cmd = ["dotnet", "run", "--project", proj, "--", "--flow-json", flow_path]
    try:
        proc = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                              timeout=timeout, cwd=lab_root)
    except FileNotFoundError:
        return {"ok": False, "error": "dotnet_not_found",
                "hint": "本机没装 .NET SDK，或 dotnet 不在 PATH；用 --no-flow 跳过"}
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "timeout", "seconds": timeout}
    text = (proc.stdout or b"").decode("utf-8", "replace")
    if not os.path.isfile(flow_path):
        return {"ok": False, "error": "flow_json_not_written",
                "exit_code": proc.returncode,
                "tail": text.strip().splitlines()[-6:]}
    try:
        with io.open(flow_path, "r", encoding="utf-8") as fh:
            data = json.load(fh)
    except Exception as ex:  # noqa
        return {"ok": False, "error": "flow_json_unreadable: %s" % ex}
    data["exit_code"] = proc.returncode
    return data


def flow_state(flow):
    """把 flow 结果压成一个状态词，给 lines.flow 用。"""
    if not flow:
        return "not-wired"
    return "wired" if flow.get("ok") else "error"


# ---------------------------------------------------------------- check

def cmd_check(args):
    out = os.path.abspath(args.out)
    os.makedirs(out, exist_ok=True)
    # 本模组自有 Brush（Prefabs 同级 Brushes/）：与「原版命中」分开统计
    own_brushes = _own_brush_names(args.prefab_dir)

    # ---- native 取证索引（只读；找不到原版目录就退化成 not-wired）
    if getattr(args, "no_native", False):
        native_roots, brush_roots = [], []
    else:
        native_roots, brush_roots = find_native_roots(getattr(args, "native_dir", None))
    native_index = NativeIndex(native_roots, brush_roots) if native_roots else None

    collect = {}
    files, report, index = geo.build(args.prefab_dir, out, args.rows,
                                     args.prefab, render=True, collect=collect)
    if not files:
        sys.stderr.write("没有匹配的 Prefab：%s\n" % (args.prefab or "*"))
        return 2

    # ---- 截图（可选；找不到浏览器不算失败）
    shots, shot_note = {}, None
    if args.shot:
        browser = geo.find_browser(args.chrome)
        if not browser:
            shot_note = ("找不到 Chrome / Edge —— 用 --chrome <路径> 指定，"
                         "或设环境变量 AWAKE_UILAB_BROWSER")
        else:
            shot_dir = os.path.join(out, "render" if args.render else "shot")
            os.makedirs(shot_dir, exist_ok=True)
            for r in report:
                fn = r["prefab"]
                if "error" in r or fn not in collect:
                    continue
                stem = os.path.splitext(os.path.basename(fn))[0]
                png = os.path.join(shot_dir, stem + ".png")
                ok, html_path, _err = geo.render_shot(
                    collect[fn]["entries"], collect[fn], fn, png, browser,
                    args.shot_label, args.shot_scale, r,
                    render=args.render, render_bg=args.render_bg)
                if ok:
                    shots[fn] = {"png": _rel(png, out), "html": _rel(html_path, out)}

    # ---- 组装统一报告
    prefabs = []
    for r in report:
        if "error" in r:
            prefabs.append({"name": r["prefab"], "ok": False, "error": r["error"]})
            continue
        ppath = os.path.join(os.path.abspath(args.prefab_dir), r["prefab"])
        try:
            with io.open(ppath, "r", encoding="utf-8", errors="replace") as fh:
                xml_text = fh.read()
        except Exception:  # noqa
            xml_text = ""
        contract = extract_contract(xml_text)
        brush_names = sorted(set(RE_BRUSH_REF.findall(xml_text)))
        prefabs.append({
            "name": r["prefab"],
            "ok": r["issue_count"] == 0,
            "panel": r["panel_box"],
            "widget_count": r["widget_count"],
            "issue_count": r["issue_count"],
            "issue_summary": r["issue_summary"],
            "issues": r["issues"],
            "digest": _digest(r),
            "shots": shots.get(r["prefab"]),
            # ③ 原版取证（绑定 / 命令 / Brush）已接线；
            # ② 流程验证是整机级的（不按面板拆分），见报告顶层 flow。
            "native": build_native(r["prefab"], contract, native_index, brush_names,
                                   own_brush_names=own_brushes),
        })

    all_issues = [i for r in report for i in r.get("issues", [])]
    summary = geo.summarize_issues(all_issues)

    # ---- flow 接线（②）：跑 C# 本地 Lab。耗时较长，失败不致命。
    flow = None
    if getattr(args, "flow", True):
        flow = run_flow(HERE, out, getattr(args, "flow_timeout", DOTNET_TIMEOUT))

    now = _now()
    if native_index:
        native_index.scan()
    payload = {
        "schema": SCHEMA,
        "generator": GENERATOR,
        "generated_at": now,
        "read_only": True,
        "prefab_dir": os.path.abspath(args.prefab_dir),
        "canvas": {"width": geo.CANVAS_W, "height": geo.CANVAS_H},
        "baseline": getattr(geo, "BASELINE", None),
        "html": _rel(index, out) if index else None,
        "prefabs": prefabs,
        "summary": {
            "prefab_count": len(prefabs),
            "widget_total": sum(p.get("widget_count", 0) for p in prefabs),
            "issue_total": summary["total"],
            "by_rule": summary["by_rule"],
            "by_severity": summary["by_severity"],
            "by_value": summary["by_value"],
        },
        "lines": {
            "geometry": "wired",
            "flow": flow_state(flow),
            "native": "wired" if native_index else "not-wired",
        },
        "flow": flow,
        "native_index": ({
            "roots": native_roots,
            "brush_roots": brush_roots,
            "modules": list(NATIVE_MODULES),
            "mode": "local-xml",
            "prefab_count": len(native_index.by_rel),
            "brush_defs": len(native_index.brush_defs),
        } if native_index else None),
    }

    report_path = os.path.join(out, "ui-report.v1.json")
    with io.open(report_path, "w", encoding="utf-8") as fh:
        fh.write(json.dumps(payload, ensure_ascii=False, indent=2))

    # ---- 快照（为 diff 攒基线）
    snap_dir = _snapshot_dir(out)
    os.makedirs(snap_dir, exist_ok=True)
    snap_name = _stamp(now) + ".json"
    with io.open(os.path.join(snap_dir, snap_name), "w", encoding="utf-8") as fh:
        fh.write(json.dumps(payload, ensure_ascii=False, indent=2))

    changes = []
    if args.diff:
        prev, _prev_name = _load_prev_snapshot(snap_dir, snap_name)
        if prev:
            changes = diff_reports(prev, payload)

    if args.json:
        print(json.dumps({"report_path": report_path, "report": payload,
                          "changes": changes}, ensure_ascii=False, indent=2))
    else:
        _print_check(payload, out, report_path, shot_note, changes)

    sev = summary["by_severity"]
    if args.strict and (sev.get("error") or sev.get("warn")):
        return 1
    return 0


# ---------------------------------------------------------------- diff 命令

def cmd_diff(args):
    out = os.path.abspath(args.out)
    snap_dir = _snapshot_dir(out)
    if not os.path.isdir(snap_dir):
        sys.stderr.write("还没有快照。先跑一次：awake_ui.py check\n")
        return 2
    names = sorted(f for f in os.listdir(snap_dir) if f.endswith(".json"))
    if len(names) < 2:
        sys.stderr.write("只攒到 1 份快照，再跑一次 check 才有可比的两份。\n")
        return 2

    def load(n):
        with io.open(os.path.join(snap_dir, n), "r", encoding="utf-8") as fh:
            return json.load(fh)

    old, new = load(names[-2]), load(names[-1])
    changes = diff_reports(old, new)
    if args.json:
        print(json.dumps({"from": names[-2], "to": names[-1], "changes": changes},
                         ensure_ascii=False, indent=2))
    else:
        _print_diff(names[-2], names[-1], changes)
    return 0


# ---------------------------------------------------------------- 输出

def _print_check(payload, out, report_path, shot_note, changes):
    c, s = payload["canvas"], payload["summary"]
    sev = s["by_severity"]
    print("AWAKE UI 工作台 · check（只读）")
    print("  画布    %d×%d · Prefab %d 个 · 控件 %d · 问题 %d"
          % (c["width"], c["height"], s["prefab_count"], s["widget_total"], s["issue_total"]))
    print("  严重度  error %d · warn %d · info %d"
          % (sev.get("error", 0), sev.get("warn", 0), sev.get("info", 0)))
    print()
    for p in payload["prefabs"]:
        if "error" in p:
            print("  [ERR ] %-28s %s" % (p["name"], p["error"]))
            continue
        mark = "WARN" if p["issue_count"] else "OK  "
        print("  [%s] %-28s 控件 %3d · 面板 %4d×%-4d · 问题 %2d"
              % (mark, p["name"], p["widget_count"],
                 p["panel"]["w"], p["panel"]["h"], p["issue_count"]))
        if p.get("shots"):
            print("           图 → %s" % p["shots"]["png"])
        nat = p.get("native") or {}
        near = nat.get("nearest") or []
        if near:
            top = near[0]
            sh = top.get("delta") or {}
            extra = ""
            if sh.get("commands_only_native"):
                extra = " · 原版另有命令 %d 个" % len(sh["commands_only_native"])
            print("           原版 → %s（共享 %d 项 · 加权 %.2f%s）"
                  % (top["path"], top.get("shared", 0), top.get("weighted_score", 0), extra))
        br = nat.get("brushes") or {}
        if br.get("referenced"):
            print("           Brush  %d 个引用 · 自有命中 %d · 原版命中 %d · 未命中 %d"
                  % (len(br["referenced"]), len(br.get("own", [])),
                     len(br.get("resolved", {})), len(br.get("missing", []))))
    if shot_note:
        print()
        print("  截图跳过：%s" % shot_note)
    print()
    print("  报告    %s" % report_path)
    if payload.get("html"):
        print("  HTML    %s" % os.path.join(out, payload["html"]))
    ni = payload.get("native_index")
    if ni:
        print("  原版库  %d Prefab · %d Brush 定义（官方模块 %s）"
              % (ni["prefab_count"], ni["brush_defs"], "/".join(ni.get("modules", []))))
    fl = payload.get("flow")
    print()
    if fl is None:
        print("  流程    not-wired（去掉 --no-flow 即可接线）")
    elif "error" in fl:
        print("  流程    error：%s" % fl.get("error"))
    else:
        e2, fx = fl.get("e2", {}), fl.get("fixtures", {})
        print("  流程    E2 用例 %d/%d · fixture %d/%d · %s"
              % (e2.get("passed", 0), e2.get("total", 0),
                 fx.get("built", 0), fx.get("total", 0),
                 "全部通过" if fl.get("ok") else "有失败"))
    if changes:
        print()
        print("  与上次快照相比：")
        _print_changes(changes)


def _print_changes(changes):
    for ch in changes:
        if ch["kind"] == "added":
            print("    [新增] %s" % ch["prefab"])
            continue
        if ch["kind"] == "removed":
            print("    [移除] %s" % ch["prefab"])
            continue
        print("    [变更] %s" % ch["prefab"])
        for d in ch["deltas"]:
            f = d["field"]
            if f == "panel":
                print("           面板   %s×%s → %s×%s"
                      % (d["from"]["w"], d["from"]["h"], d["to"]["w"], d["to"]["h"]))
            elif f == "widget_count":
                print("           控件   %s → %s" % (d["from"], d["to"]))
            elif f == "issue_count":
                print("           问题   %s → %s" % (d["from"], d["to"]))
            elif f == "issues_fixed":
                print("           修好 %d 处 · %s" % (d["count"], ", ".join(d["sample"][:4])))
            elif f == "issues_new":
                print("           新增 %d 处 · %s" % (d["count"], ", ".join(d["sample"][:4])))
            elif f == "layout_changed":
                print("           几何   +%d / -%d 个矩形"
                      % (d["added_count"], d["removed_count"]))
                for s in d["added"][:3]:
                    print("                  + %s" % s)
                for s in d["removed"][:3]:
                    print("                  - %s" % s)


def _print_diff(a, b, changes):
    print("AWAKE UI 工作台 · diff")
    print("  %s  →  %s" % (a, b))
    if not changes:
        print("  没有变化。")
        return
    _print_changes(changes)


# ---------------------------------------------------------------- CLI

def _add_common(ap):
    ap.add_argument("--prefab-dir", default=DEFAULT_PREFAB_DIR,
                    help="Prefab 目录（默认 AWAKE/GUI/Prefabs）")
    ap.add_argument("--out", default=DEFAULT_OUT, help="输出目录（默认 out/）")
    ap.add_argument("--rows", type=int, default=3, help="ListPanel 夹具行数（默认 3）")
    ap.add_argument("--prefab", action="append", help="只处理指定文件名，可重复")
    ap.add_argument("--json", action="store_true", help="结果以 JSON 打到 stdout")
    ap.add_argument("--native-dir", help="原版 Prefab 根目录（默认自动探测 Steam 路径）")
    ap.add_argument("--no-native", action="store_true",
                    help="不做原版取证（报告 native 字段留空）")


def main():
    ap = argparse.ArgumentParser(
        prog="awake_ui.py",
        description="AWAKE UI 工作台 · 统一入口（只读）：把几何/审计/截图串成一条命令")
    sub = ap.add_subparsers(dest="cmd")

    c = sub.add_parser("check", help="几何 + 审计 + 截图 + 统一报告")
    _add_common(c)
    c.add_argument("--no-shot", dest="shot", action="store_false",
                   help="不截图（默认截图）")
    c.add_argument("--shot-label", choices=("none", "compact", "full"), default="compact")
    c.add_argument("--shot-scale", type=float, default=1.0)
    c.add_argument("--render", action="store_true",
                   help="出**真渲染**图（只画真贴图/真底色/真文字）到 out/render/，"
                        "与几何图分开放，互不覆盖")
    c.add_argument("--render-bg", default=None,
                   help="真渲染的底板色（默认 %s）" % geo.RENDER_BG)
    c.add_argument("--chrome", help="Chrome / Edge 可执行文件路径（默认自动探测）")
    c.add_argument("--flow", dest="flow", action="store_true", default=True,
                   help="跑 C# 本地 Lab（E2 + 全量 fixture）并写进报告 flow 字段（默认开）")
    c.add_argument("--no-flow", dest="flow", action="store_false",
                   help="跳过 flow 接线（更快，只做几何 + 审计 + 截图）")
    c.add_argument("--flow-timeout", type=int, default=DOTNET_TIMEOUT,
                   help="flow 超时秒数（默认 %d）" % DOTNET_TIMEOUT)
    c.add_argument("--diff", action="store_true", help="顺带与上次快照比较")
    c.add_argument("--strict", action="store_true", help="有 warn/error 时退出码 1")
    c.set_defaults(shot=True, func=cmd_check)

    d = sub.add_parser("diff", help="与上一次快照比较，看这次改了什么")
    _add_common(d)
    d.set_defaults(func=cmd_diff)

    args = ap.parse_args()
    if not getattr(args, "func", None):
        ap.print_help()
        return 2
    return args.func(args)


if __name__ == "__main__":
    sys.exit(main())
