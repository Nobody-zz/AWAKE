#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
AWAKE Prefab 本地几何线框预览 + 布局审计（只读 CLI）

目的
    不启动游戏，在本地把 GUI/Prefabs/*.xml 的**布局几何**算出来，
    画成线框并做规则审计——用于检查位置 / 大小 / 间距 / 对齐 / 层级。
    这些是确定性的，不需要渲染。

诚实边界（重要）
    - 本工具算的是**推导几何**，不是 Bannerlord 的真实渲染结果。
    - 文本高度用字符宽度估算，与 Gauntlet 真实排版会有偏差。
    - 颜色 / 字体 / 贴图 / 图标一律不还原（也不该还原）。
    - 只读：不修改任何 Prefab、不碰游戏目录、不启动游戏。

用法（本机 CLI；任何会跑命令的 Agent 都可以直接调用）
    python preview_prefab_geometry.py                     # 生成 HTML 线框预览到 out/
    python preview_prefab_geometry.py --list               # 列出可检查的 Prefab
    python preview_prefab_geometry.py --json                # 几何 + 审计 → JSON（stdout）
    python preview_prefab_geometry.py --audit               # 只审计，输出文本
    python preview_prefab_geometry.py --audit --json        # 只审计，输出 JSON
    python preview_prefab_geometry.py --prefab NpcDialogue.xml --json
    python preview_prefab_geometry.py --audit --strict      # 有 warn/error 时退出码 1

截图（给人和 Agent 看同一张图；用本机 Chrome 无头模式，无需装任何东西）
    python preview_prefab_geometry.py --shot out/NpcDialogue.png --prefab NpcDialogue.xml
    python preview_prefab_geometry.py --shot out/            # 每个面板各一张 PNG
    python preview_prefab_geometry.py --shot out/x.png --prefab X.xml --shot-label none
    --shot-label  none（纯线框）/ compact（只标尺寸，默认）/ full（标类型名）

审计基准
    见 docs/UI-LAYOUT-BASELINE-FROM-NATIVE-20260913.md：
    原版参考分辨率 1920×1080；间距与尺寸以 **5 的倍数**为栅格
    （5/10/15/20/25/30/50/60…，10 最常用），小值 1/2/3 仅用于贴边微调。
    原版还用 <Constants> + !名字 表达尺寸关系；本工具对未常量化的字面大数值给出提示。
"""

import argparse
import html
import io
import json
import math
import os
import subprocess
import sys
import xml.etree.ElementTree as ET

# 真实文本度量（字体 .fnt 指标 / Brush 字号 / @绑定解析）是**可选增强层**。
# 装配成功时文本尺寸取自游戏原版数据；任何一段拿不到就自动退回下面的启发式估算。
try:
    import ui_text_metrics
except Exception:  # noqa: BLE001 —— 增强层缺失不该让工具无法启动
    ui_text_metrics = None

# 原版 Sprite 贴图层（D 线质感）：SpriteData.xml 索引 + 图集 PNG 裁剪，同为可选增强。
try:
    import ui_sprite_atlas
except Exception:  # noqa: BLE001
    ui_sprite_atlas = None

METRICS = None
ATLAS = None


def init_metrics(prefab_dir, native_dir=None, awake_root=None, game_dir=None,
                 language="简体中文", verbose=False):
    """装配真实文本度量层；失败则保持 METRICS=None（退回估算）。可重复调用。"""
    global METRICS
    if ui_text_metrics is None:
        return None
    if game_dir is None:
        game_dir = os.environ.get("AWAKE_UILAB_GAME") or None
    if awake_root is None and prefab_dir:
        awake_root = os.path.normpath(os.path.join(prefab_dir, "..", ".."))
    try:
        METRICS = ui_text_metrics.build(prefab_dir=prefab_dir, native_dir=native_dir,
                                        awake_root=awake_root, game_dir=game_dir,
                                        language=language, verbose=verbose)
    except Exception as e:  # noqa: BLE001
        sys.stderr.write("[metrics] 装配失败，文本退回估算：%s\n" % e)
        METRICS = None
    return METRICS


def init_atlas(game_dir=None, verbose=False):
    """装配原版 Sprite 贴图层；缺索引/工具时自动降级（退回分类色）。"""
    global ATLAS
    if ui_sprite_atlas is None or os.environ.get("AWAKE_UILAB_NO_SPRITES"):
        return None
    if game_dir is None:
        game_dir = os.environ.get("AWAKE_UILAB_GAME") or None
    try:
        ATLAS = ui_sprite_atlas.SpriteAtlas(game=game_dir)
        if verbose:
            for n in ATLAS.notes:
                sys.stderr.write("[atlas] %s\n" % n)
    except Exception as e:  # noqa: BLE001
        sys.stderr.write("[atlas] 装配失败，sprite 退回分类色：%s\n" % e)
        ATLAS = None
    return ATLAS


def metrics_status():
    """度量层的可用性摘要（给报告用）。"""
    if METRICS is None:
        return {"available": False}
    fonts = sorted(k for k, v in (METRICS.fonts.fonts.items() if METRICS.fonts else [])
                   if v)
    return {
        "available": True,
        "language": METRICS.language,
        "fonts_loaded": fonts,
        "brushes": len(METRICS.brushes.index) if METRICS.brushes else 0,
        "strings": len(METRICS.texts.strings),
        "props": len(METRICS.texts.props),
        "notes": list(METRICS.notes),
    }

CANVAS_W = 1920
CANVAS_H = 1080

# 控件分类 → 绘图样式键
# 引用外部标准件（Native/GUI/Prefabs/Standard/*.xml）时，宽度由被引用件决定。
# 这里用原版标准件的实测值兜底；读不到游戏目录时也能给出合理几何。
STD_SIZE = {
    "Standard.VerticalScrollbar": (20.0, None),
    "Standard.HorizontalScrollbar": (None, 20.0),
}

TEXT_TAGS = ("TextWidget", "RichTextWidget")
BUTTON_TAGS = ("ButtonWidget",)
LIST_TAGS = ("ListPanel", "GridWidget")
SCROLL_TAGS = ("ScrollablePanel",)
INPUT_TAGS = ("EditableTextWidget",)

# 逻辑 / 装饰控件：不占布局空间，也不该被审计成「零尺寸问题」
LOGIC_TAGS = ("NavigationScopeTargeter", "DimensionSyncWidget", "HintWidget")
# 全屏容器：名字里带 Button 但不是按钮（按名字判会误判成一个铺满屏幕的红色大按钮）
CONTAINER_OVERRIDES = ("ConversationScreenButtonWidget",)


def classify(tag):
    base = tag.split(".")[-1]
    if base in CONTAINER_OVERRIDES:
        return "container"
    if base in LOGIC_TAGS or base.endswith("Targeter"):
        return "logic"
    if base in TEXT_TAGS or base.endswith("RichTextWidget") or base.endswith("TextWidget"):
        return "text"
    if base in BUTTON_TAGS or (base.endswith("ButtonWidget") and base not in CONTAINER_OVERRIDES):
        return "button"
    if base in LIST_TAGS:
        return "list"
    if base in SCROLL_TAGS:
        return "scroll"
    if base in INPUT_TAGS:
        return "input"
    if base == "VerticalScrollbar" or tag.startswith("Standard."):
        return "standard"
    return "container"


def is_logic(tag):
    return classify(tag) == "logic"


def is_list(tag):
    return classify(tag) == "list"


def is_text(tag):
    return classify(tag) in ("text", "input")


# ---------------------------------------------------------------- 解析

class Node(object):
    def __init__(self, el, line_of=None, src=None):
        self.el = el
        self.tag = el.tag
        self.attrs = dict(el.attrib)
        # 原始写法（内联 *参数 / !常量 前的样子）——审计据此判断「是不是写死的字面值」
        self.orig_attrs = dict(el.attrib)
        self.children = []
        self.item_template = None
        self.parent = None
        # 若本节点是「外部 Prefab 引用」，展开后记下来源文件（用于说明与排错）
        self.prefab_ref = None
        # 出处：源文件 + 行号。line_of 由 load_prefab 用 expat 单独扫出来
        # （ET 不给行号），审计靠它把「哪一行写错了」直接指出来。
        self.src = src
        self.line = (line_of or {}).get(id(el))
        for child in el:
            if child.tag == "Children":
                for g in child:
                    self.children.append(Node(g, line_of, src))
            elif child.tag == "ItemTemplate":
                for g in child:
                    if g.tag not in ("Constants", "Constant"):
                        self.item_template = Node(g, line_of, src)
                        break
            elif child.tag == "Constants":
                pass

    def where(self):
        """人类可读的出处 `文件:行`；取不到行号时只给文件名。"""
        if not self.src:
            return None
        if self.line:
            return "%s:%d" % (os.path.basename(self.src), self.line)
        return os.path.basename(self.src)

    def path(self):
        """节点路径（父子链 + 同级下标）——同一文件内唯一，供精确整改定位。"""
        parts = []
        cur = self
        while cur is not None:
            base = cur.tag.split(".")[-1]
            p = cur.parent
            idx = -1
            if p is not None:
                for i, sib in enumerate(p.children):
                    if sib is cur:
                        idx = i
                        break
            parts.append(base if p is None else "%s[%d]" % (base, idx))
            cur = p
        return "/".join(reversed(parts))

    def raw(self, key, default=None):
        return self.attrs.get(key, default)

    @property
    def id(self):
        return self.attrs.get("Id")


META_TAGS = ("Constants", "Constant", "Parameters", "Parameter")


def _eval_token(raw, params, consts):
    """解析 Prefab 里的数值记号：!名字=常量、*名字=参数、其余按字面数字。"""
    if raw is None:
        return None
    s = str(raw).strip()
    if s == "":
        return None
    if s.startswith("!"):
        return consts.get(s[1:])
    if s.startswith("*"):
        return params.get(s[1:])
    try:
        return float(s)
    except ValueError:
        return None


def _solve_consts(root, params):
    """求解 <Constants>。常量之间可互相引用，多跑几轮收敛。"""
    raw = [(el.attrib.get("Name"), el.attrib) for el in root.iter("Constant")
           if el.attrib.get("Name")]
    consts = {}
    for _ in range(3):
        progress = False
        for nm, at in raw:
            if nm in consts:
                continue
            v = _eval_token(at.get("Value"), params, consts)
            if v is None:
                continue
            mult = at.get("MultiplyResult")
            if mult:
                try:
                    v *= float(mult)
                except ValueError:
                    pass
            consts[nm] = v
            progress = True
        if not progress:
            break
    return consts


def _line_of(root, path):
    """给每个元素配行号：{id(element): 行号}。

    ET 不提供行号，所以用 expat 单独扫同一文件一遍——两者都只报元素、都按
    文档顺序，逐项 zip 即可对齐。数量对不上就整体放弃：宁可不给行号，
    也不给错行号（错行号比没行号更害人）。
    """
    try:
        import xml.parsers.expat as _expat
        lines = []
        parser = _expat.ParserCreate()

        def _start(tag, attrs):
            lines.append(parser.CurrentLineNumber)

        parser.StartElementHandler = _start
        with open(path, "rb") as f:
            parser.ParseFile(f)
        els = list(root.iter())
        if len(lines) != len(els):
            return {}
        return dict((id(el), ln) for el, ln in zip(els, lines))
    except Exception:  # noqa
        return {}


def load_prefab(path):
    """读一个 Prefab，返回 (根控件, 常量表, 参数表)。

    原版有两套记号必须解析，否则尺寸会静默塌成 0：
      *Name → <Parameters><Parameter Name="Name" DefaultValue="..."/>（例：*Item.Width）
      !Name → <Constants><Constant Name="Name" Value=".." MultiplyResult=".."/>
              且 Constant 的 Value 本身可以是 *参数（例：Item.Height = *Item.Width × 0.71）

    另外 <Parameters>/<Constants> 是元数据段，**不是**根控件——早先版本会把
    <Parameters> 误当根控件，导致整个被引 Prefab 展开失败。
    """
    tree = ET.parse(path)
    root = tree.getroot()
    line_of = _line_of(root, path)

    params = {}
    for el in root.iter("Parameter"):
        nm = el.attrib.get("Name")
        if nm:
            v = _eval_token(el.attrib.get("DefaultValue"), {}, {})
            if v is not None:
                params[nm] = v

    consts = _solve_consts(root, params)

    top = None
    for child in root:
        if child.tag == "Window":
            for g in child:
                if g.tag not in META_TAGS:
                    top = Node(g, line_of, path)
                    break
            if top is not None:
                break
        elif child.tag in META_TAGS:
            continue
        else:
            top = Node(child, line_of, path)
            break
    return top, consts, params


# ---------------------------------------------------------------- 外部 Prefab 引用
#
# 原版大量用「Prefab 引用」组合界面：`<SPConversationAggresivePartyItem .../>`
# 这类节点不是控件，而是一个外部 Prefab 的实例。不展开的话，引擎会拿默认的
# StretchToParent 去承接父级可用尺寸，导致 CoverChildren 链一路放大
# （实测原版 SPConversation 的 GridWidget 被推到 77889 高）。
#
# 判定：tag 以 Widget 结尾、或在已知基础控件集合里 → 是控件；
#       否则尝试按 `<tag>.xml` 找 Prefab 并就地展开。

KNOWN_BASE_TAGS = (
    "Widget", "ListPanel", "GridWidget", "ScrollablePanel", "ScrollbarWidget",
    "VerticalScrollbar", "HorizontalScrollbar",
)

# 原版（官方模块）Prefab 目录探测——只用于**展开引用**，不修改任何原版文件。
NATIVE_MODULES = ("Native", "SandBox", "SandBoxCore", "StoryMode", "Multiplayer")
BANNERLORD_MODULES_CANDIDATES = (
    "D:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules",
    "C:/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules",
    "C:/Program Files/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules",
    "E:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules",
    "F:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord/Modules",
)


def detect_native_prefab_dirs(explicit=None):
    """定位原版官方模块的 Prefab 目录。找不到就返回空列表（引用展开自动降级）。"""
    modules = None
    if explicit:
        p = os.path.normpath(explicit)
        if os.path.basename(p).lower() == "prefabs":
            return [p] if os.path.isdir(p) else []
        modules = p if os.path.isdir(p) else None
    if modules is None:
        env = os.environ.get("AWAKE_UILAB_NATIVE")
        if env and os.path.isdir(env):
            modules = os.path.normpath(env)
    if modules is None:
        for c in BANNERLORD_MODULES_CANDIDATES:
            if os.path.isdir(c):
                modules = c
                break
    if not modules:
        return []
    out = []
    for m in NATIVE_MODULES:
        d = os.path.join(modules, m, "GUI", "Prefabs")
        if os.path.isdir(d):
            out.append(d)
    return out


PREFAB_SEARCH_DIRS = []
_PREFAB_INDEX = {}


def set_prefab_search_dirs(dirs):
    """登记 Prefab 搜索目录（本工程 + 原版），建立 basename → path 索引。"""
    global PREFAB_SEARCH_DIRS, _PREFAB_INDEX
    PREFAB_SEARCH_DIRS = [d for d in dirs if d and os.path.isdir(d)]
    _PREFAB_INDEX = {}
    for d in PREFAB_SEARCH_DIRS:
        for dirpath, _dirnames, filenames in os.walk(d):
            for fn in filenames:
                if fn.lower().endswith(".xml"):
                    _PREFAB_INDEX.setdefault(fn, os.path.join(dirpath, fn))


def is_control_tag(tag):
    base = tag.split(".")[-1]
    return base.endswith("Widget") or base in KNOWN_BASE_TAGS


def find_prefab_file(tag):
    return _PREFAB_INDEX.get(tag.split(".")[-1] + ".xml")


def bind_parent(node, parent=None):
    node.parent = parent
    for c in node.children:
        bind_parent(c, node)
    if node.item_template is not None:
        bind_parent(node.item_template, node)


def _inline_params(node, params, consts):
    """把子树里整值为 *参数 / !常量 的属性就地换成字面数字。

    原版同一个 Prefab 会被不同实例用不同参数引用（`Parameter.Item.Width="50"`
    与 `"70"`），参数表是全局的、装不下两份取值，所以只能把值烤进属性里。
    """
    for k in list(node.attrs.keys()):
        v = node.attrs[k]
        if isinstance(v, str) and (v.startswith("*") or v.startswith("!")):
            val = _eval_token(v, params, consts)
            if val is not None:
                node.attrs[k] = ("%g" % val) if val != int(val) else str(int(val))
    for c in node.children:
        _inline_params(c, params, consts)
    if node.item_template is not None:
        _inline_params(node.item_template, params, consts)


def expand_prefab_refs(node, depth=0, seen=None):
    """把外部 Prefab 引用节点就地展开为被引 Prefab 的内容（实例属性/参数优先）。

    `seen` 是**当前递归路径**上的 Prefab（防互相引用的环），不是全局去重——
    同一个 Prefab 被多处引用时必须各自展开，否则后面几处会退化成默认
    StretchToParent 把父级撑爆。
    """
    if depth > 8:
        return
    if seen is None:
        seen = set()

    expanded = None
    if not is_control_tag(node.tag):
        path = find_prefab_file(node.tag)
        if path and path not in seen:
            try:
                sub_top, sub_consts, sub_params = load_prefab(path)
            except Exception:  # noqa
                sub_top, sub_consts, sub_params = None, {}, {}
            if sub_top is not None:
                # 实例传参：Parameter.Item.Width="50" → 覆盖被引 Prefab 的同名参数
                overrides = {}
                for k, v in node.attrs.items():
                    if k.startswith("Parameter."):
                        val = _eval_token(v, sub_params, sub_consts)
                        if val is not None:
                            overrides[k[len("Parameter."):]] = val
                if overrides:
                    sub_params = dict(sub_params)
                    sub_params.update(overrides)
                    try:
                        root = ET.parse(path).getroot()
                        sub_consts = _solve_consts(root, sub_params)
                    except Exception:  # noqa
                        pass

                merged = dict(sub_top.attrs)
                # 实例上写的属性覆盖被引 Prefab 的根；Parameter.* 不是控件属性，剔除
                merged.update(dict((k, v) for k, v in node.attrs.items()
                                   if not k.startswith("Parameter.")))
                node.attrs = dict((k, v) for k, v in merged.items() if v is not None)
                node.children = sub_top.children
                node.item_template = sub_top.item_template
                node.prefab_ref = path
                _inline_params(node, sub_params, sub_consts)
                for k, v in sub_consts.items():
                    CONSTS.setdefault(k, v)
                for k, v in sub_params.items():
                    PARAMS.setdefault(k, v)
                expanded = path
                seen.add(path)

    for c in node.children:
        expand_prefab_refs(c, depth + 1, seen)
    if node.item_template is not None:
        expand_prefab_refs(node.item_template, depth + 1, seen)
    if expanded is not None:
        seen.discard(expanded)


def find_by_id(root, target_id, depth=0):
    if root is None or depth > 40:
        return None
    if root.attrs.get("Id") == target_id:
        return root
    for c in root.children:
        hit = find_by_id(c, target_id, depth + 1)
        if hit is not None:
            return hit
    if root.item_template is not None:
        return find_by_id(root.item_template, target_id, depth + 1)
    return None


def resolve_sync_target(node, path):
    """解析 DimensionSyncWidget 的 WidgetToCopy*From 相对路径。

    形如 `..\\..\\..\\AnswerListContainer`：上跳 N 级，再在那一层子树里按 Id 找。
    原版还有 `..\\.` 这类写法（指向自身父级、无 Id），解析不到就返回 None。
    """
    if not path:
        return None
    parts = [p for p in path.replace("/", "\\").split("\\") if p not in ("", ".")]
    ups, name = 0, None
    for p in parts:
        if p == "..":
            ups += 1
        else:
            name = p
    cur = node
    for _ in range(ups):
        if cur.parent is None:
            return None
        cur = cur.parent
    if not name:
        return None
    return find_by_id(cur, name)


# ---------------------------------------------------------------- 数值 / 常量

CONSTS = {}
PARAMS = {}


def num(v, default=0.0, additive=0.0, mult=1.0):
    """解析属性值：!名字=常量、*名字=Prefab 参数、其余按字面数字。"""
    if v is None:
        return default * mult + additive
    s = str(v).strip()
    if s == "":
        return default * mult + additive
    if s.startswith("!"):
        return CONSTS.get(s[1:], default) * mult + additive
    if s.startswith("*"):
        return PARAMS.get(s[1:], default) * mult + additive
    try:
        return float(s) * mult + additive
    except ValueError:
        return default * mult + additive


def margins(node):
    a = node.attrs
    return (num(a.get("MarginLeft")), num(a.get("MarginRight")),
            num(a.get("MarginTop")), num(a.get("MarginBottom")))


def font_size(node, default=16.0):
    """字号：显式 Brush.FontSize > Brush 定义里的 FontSize > default。

    特例：原版 Prefab 用 DataSource 行模板时会写 Brush.FontSize="*字号"，
    这种带前缀的表达式由 evaluate 之前的分支处理，这里只看字面量。
    """
    if METRICS is not None:
        _font, size = METRICS.font_and_size(node.attrs, default)
        return size
    return num(node.attrs.get("Brush.FontSize"), default)


def display_text(node, strict=False, movie=None):
    """要显示的文本。

    `Text="@属性名"` 是 Gauntlet 的 DataSource 绑定，不是字面量——装配了
    度量层时解成本地化真文本（「发送」/「离开」…）；解不出（运行时才有值的
    动态内容，如联系人名）就原样返回 `@名`，便于报告里看出是哪个绑定。

    strict=True（真渲染用）：只认**可信**绑定，重名的属性名一律不解 ——
    否则「按属性名查本地化」会串到别的面板的字符串上（实测踩过标题串号），
    而渲染图看起来越像真的，这种错越不会被发现。`movie` 传面板名可进一步
    限定只看该面板自己的 ViewModel。
    """
    for key in ("Text", "RealText", "Brush.Text"):
        v = node.attrs.get(key)
        if v is not None and v != "":
            if METRICS is not None and METRICS.texts.looks_bound(v):
                resolved = (METRICS.resolve_text_strict(v, movie) if strict
                            else METRICS.resolve_text(v))
                return resolved if resolved else v
            return v
    return ""


def text_font(node):
    """文本实际使用的字体名（已按当前语言映射；简体中文下多为 simkai）。"""
    if METRICS is None:
        return None
    font, _size = METRICS.font_and_size(node.attrs)
    return font


def is_known_text(txt):
    """文本内容是否可用于度量（未解析的 `@名` 绑定不算）。"""
    return bool(txt) and not txt.startswith("@")


def widget_color(node):
    """控件的上色来源（C 线：Brush 上色）。返回 (hex, alpha, source) 或 None。

    纪律：只上**真色**，不编造——
      source="attr"   控件自己的 Color="#RRGGBBAA"（+ AlphaFactor 乘数）
      source="brush"  其 Brush 的 Default 态 FontColor（文本真色）
    Sprite 类 Brush 记 "sprite" 但**没有真色可信**，这里返回 None（保持分类色）。
    """
    if METRICS is None:
        return None
    a = node.attrs
    raw = a.get("Color")
    if raw:
        got = _rgba_to_hex_local(raw)
        if got:
            hexv, alpha = got
            af = a.get("AlphaFactor")
            if af:
                try:
                    alpha = round(alpha * float(af), 3)
                except ValueError:
                    pass
            return hexv, alpha, "attr"
    b = a.get("Brush")
    if b:
        c = METRICS.color_of(b)
        if isinstance(c, tuple):
            return c[0], c[1], "brush"
    return None


def _rgba_to_hex_local(v):
    """"#RRGGBBAA" -> ("#RRGGBB", alpha)；独立实现避免与度量模块循环依赖。"""
    if not v or not v.startswith("#"):
        return None
    h = v[1:]
    try:
        if len(h) == 8:
            return "#" + h[0:6].upper(), int(h[6:8], 16) / 255.0
        if len(h) == 6:
            return "#" + h[0:6].upper(), 1.0
    except ValueError:
        return None
    return None


def widget_sprite(node):
    """控件的**原版贴图**来源（质感线）。返回 (png 路径, 原宽, 原高, 九宫格|None) 或 None。

    优先级：控件 Sprite= 属性 > 其 Brush 的 Default 态 sprite。
    九宫格来自 Brush 的 ExtendLeft/Top/Right/Bottom（四角原样、边单向拉伸）。
    贴图不可用（索引/图集/工具缺）时返回 None——渲染退回分类色。
    """
    if ATLAS is None:
        return None
    a = node.attrs
    name = a.get("Sprite") or a.get("SpriteName")
    nine = None
    if not name and METRICS is not None and a.get("Brush"):
        name, nine = METRICS.sprite_and_nine_of(a["Brush"])
    if not name:
        return None
    got = ATLAS.sprite_png(name)
    if not got:
        return None
    # 控件 Color= 是逐通道相乘的染色（Gauntlet 语义 #RRGGBBAA）。sprite 也必须上色，
    # 否则深色底/染色块在预览里会显示成原始亮色，判断失真。
    got = ATLAS.tint_png(got, a.get("Color")) or got
    # 控件直接写 Sprite="xxx_9" 时，九宫格参数来自 SpriteData 的 <NineRegionSprite>
    if nine is None:
        nine = ATLAS.nine_of(name)
    info = ATLAS.info(name) or (None, 1, 0, 0, 0, 0)
    return got, info[4], info[5], nine


_REL_BASE = None


def set_rel_base(d):
    """设定"当前正在写的 HTML 所在目录"。

    贴图的 href 一律按它算**相对路径**，而不是 file:/// 绝对地址：
    绝对 file:/// 只在"直接用浏览器打开本地文件"时可用；一旦这份 HTML 被
    经 http（127.0.0.1 预览服务、或任何静态服务器）打开，浏览器会按
    "跨源/本地文件"策略把 file:/// 图片全部拦掉 ⇒ 页面只剩线框、没有质感。
    相对路径两种打开方式都能用。
    """
    global _REL_BASE
    _REL_BASE = os.path.abspath(d) if d else None


def _file_url(p):
    """本地文件 -> 适合写进 SVG <image href> 的地址。

    有 _REL_BASE（正常路径）⇒ 输出相对路径；跨盘符等算不出相对时退回 file:///。
    """
    ap = os.path.abspath(p)
    if _REL_BASE:
        try:
            rel = os.path.relpath(ap, _REL_BASE).replace("\\", "/")
            if not rel.startswith(".." * 8):  # 防止盘符不同的怪相对路径
                return rel
        except ValueError:  # 不同盘符
            pass
    try:
        from pathlib import Path
        return Path(ap).as_uri()
    except Exception:  # noqa: BLE001
        return "file:///" + ap.replace("\\", "/")


# ---------------------------------------------------------------- 尺寸估算

def est_text_metrics(text, fs, max_w, font=None):
    """返回 (宽, 高)。

    优先用**游戏真实字体度量**：.fnt 的逐字 xadvance 累计宽度 + common 的
    lineHeight（都按 fs / 字体原生高 缩放）。以下情况退回启发式估算：
      · 文本是未解析的动态绑定（内容本来就不确定）
      · 字体数据缺失（没装游戏 / 字体名对不上）
    """
    if METRICS is not None:
        if is_known_text(text):
            got = METRICS.measure(text, font, fs, max_w)
            if got:
                return got
        # 内容不可知时，至少把**行高**用真值——这一维不依赖文本内容
        lh = METRICS.line_height(font, fs)
        if lh:
            return 0.0, lh
    # ---- 启发式兜底（原先的估算：CJK 1.0em / 西文 0.55em，行高 1.45em）
    if not text:
        return 0.0, fs * 1.45
    units = 0.0
    for ch in text:
        units += 1.0 if ord(ch) > 0x2E80 else 0.55
    natural = units * fs
    line_h = fs * 1.45
    if max_w and max_w > 0 and natural > max_w:
        lines = int(math.ceil(natural / max_w))
        return float(max_w), lines * line_h
    return natural, line_h


def items_of(node, rows):
    """列表 / 网格的子项：有 ItemTemplate 就用夹具行数复制，否则用真实子节点。"""
    if node.item_template is not None:
        return [node.item_template] * max(1, rows)
    return list(node.children)


def grid_spec(node, rows):
    """若是网格布局，返回 (列数, 格宽, 格高, 格数)；否则 None。

    原版写法：`<GridWidget LayoutImp="GridLayout" ColumnCount="9"
    DefaultCellWidth="60" DefaultCellHeight="70">`。CoverChildren 下
    宽 = 列数 × 格宽、高 = 行数 × 格高——不是让每个格子去继承父级尺寸。
    """
    base = node.tag.split(".")[-1]
    if base != "GridWidget" and node.attrs.get("LayoutImp") != "GridLayout":
        return None
    a = node.attrs
    try:
        cols = int(float(num(a.get("ColumnCount"), 1) or 1))
    except (TypeError, ValueError):
        cols = 1
    cols = max(1, cols)
    cell_w = num(a.get("DefaultCellWidth"), 0.0)
    cell_h = num(a.get("DefaultCellHeight"), 0.0)
    if cell_w <= 0 or cell_h <= 0:
        return None
    return cols, cell_w, cell_h, len(items_of(node, rows))


def _clamp(node, w, h):
    """应用 Min/Max 尺寸约束（原版用它给 CoverChildren 加下限，如 MinHeight="192"）。"""
    a = node.attrs
    mnw, mxw = num(a.get("MinWidth"), 0.0), num(a.get("MaxWidth"), 0.0)
    mnh, mxh = num(a.get("MinHeight"), 0.0), num(a.get("MaxHeight"), 0.0)
    if mnw > 0 and w < mnw:
        w = mnw
    if mxw > 0 and w > mxw:
        w = mxw
    if mnh > 0 and h < mnh:
        h = mnh
    if mxh > 0 and h > mxh:
        h = mxh
    return max(0.0, w), max(0.0, h)


_SYNC_STACK = set()


def _own_extent(node, axis, avail_w, avail_h, rows, depth):
    """按控件**自身的**尺寸策略取某一轴的值（Fixed / StretchToParent / CoverChildren）。

    为什么要单独写：DimensionSyncWidget 只同步 `DimensionToSync` 指的那一轴，
    **另一轴走自身策略**。官方写法即为证——
      Native/GUI/Prefabs/Information/HintTooltip.xml:18
        WidthSizePolicy="Fixed" HeightSizePolicy="Fixed" SuggestedHeight="12"
        DimensionToSync="Horizontal"        ⇒ 同步宽、高取自身的 SuggestedHeight
      Native/GUI/Prefabs/GameMenu/GameMenu.xml:73
        WidthSizePolicy="StretchToParent" HeightSizePolicy="Fixed"
        DimensionToSync="Vertical"          ⇒ 同步高、宽取自身的 StretchToParent
    早先 _sync_size 把未同步轴一律当 0，会让 `DimensionToSync="Vertical"` 的控件
    宽度恒为 0 ⇒ 几何上直接隐形（静默，不报错）。
    """
    a = node.attrs
    ml, mr, mt, mb = margins(node)
    if axis == "w":
        sp = a.get("WidthSizePolicy", "StretchToParent")
        sug, inner = num(a.get("SuggestedWidth"), 0.0), max(0.0, avail_w - ml - mr)
        if sp == "Fixed":
            return sug
        if sp == "StretchToParent":
            return inner if avail_w > 0 else sug
        if sp == "CoverChildren":
            if is_list(node.tag):
                return stack_extent(node, inner, max(0.0, avail_h - mt - mb), rows, "x", depth)
            out = 0.0
            for c in node.children:
                cm = margins(c)
                cw, _ = measure(c, inner - cm[0] - cm[1],
                                max(0.0, avail_h - mt - mb) - cm[2] - cm[3], rows, depth + 1)
                out = max(out, cw + cm[0] + cm[1])
            return out if out > 0 else sug
        return sug
    sp = a.get("HeightSizePolicy", "StretchToParent")
    sug, inner = num(a.get("SuggestedHeight"), 0.0), max(0.0, avail_h - mt - mb)
    if sp == "Fixed":
        return sug
    if sp == "StretchToParent":
        return inner if avail_h > 0 else sug
    if sp == "CoverChildren":
        if is_list(node.tag):
            return stack_extent(node, max(0.0, avail_w - ml - mr), inner, rows, "y", depth)
        if is_text(node.tag):
            _, eh = est_text_metrics(display_text(node), font_size(node),
                                     max(0.0, avail_w - ml - mr))
            return max(eh, sug)
        out = 0.0
        for c in node.children:
            cm = margins(c)
            _, chh = measure(c, max(0.0, avail_w - ml - mr) - cm[0] - cm[1],
                             inner - cm[2] - cm[3], rows, depth + 1)
            out = max(out, chh + cm[2] + cm[3])
        return out if out > 0 else sug
    return sug


def _sync_size(node, avail_w, avail_h, rows, depth):
    """DimensionSyncWidget：把目标控件的尺寸复制过来。

    自身不绘制任何东西，但**必须**把尺寸报上去——原版靠它让
    「对话框高度」跟「选项列表高度」对齐（CoverChildren 取子级最大值）。
    """
    a = node.attrs
    dim = a.get("DimensionToSync", "Vertical")
    key = id(node)
    if key in _SYNC_STACK:  # 防自引用
        return 0.0, 0.0
    _SYNC_STACK.add(key)
    try:
        sync_w = dim in ("Horizontal", "HorizontalAndVertical")
        sync_h = dim in ("Vertical", "HorizontalAndVertical")
        w = h = 0.0
        if sync_w:
            t = resolve_sync_target(node, a.get("WidgetToCopyWidthFrom")
                                    or a.get("WidgetToCopyHeightFrom"))
            if t is not None:
                w = measure(t, avail_w, avail_h, rows, depth + 1)[0]
        if sync_h:
            t = resolve_sync_target(node, a.get("WidgetToCopyHeightFrom")
                                    or a.get("WidgetToCopyWidthFrom"))
            if t is not None:
                h = measure(t, avail_w, avail_h, rows, depth + 1)[1]
        # 未同步的那一轴按自身策略取（见 _own_extent 的官方例证）
        if not sync_w:
            w = _own_extent(node, "w", avail_w, avail_h, rows, depth)
        if not sync_h:
            h = _own_extent(node, "h", avail_w, avail_h, rows, depth)
        return _clamp(node, w, h)
    finally:
        _SYNC_STACK.discard(key)


def measure(node, avail_w, avail_h, rows, depth=0):
    """自底向上算一个控件的 (w, h)。depth 防递归失控。"""
    if depth > 24:
        return 0.0, 0.0

    base = node.tag.split(".")[-1]
    # 尺寸同步器：自身不画东西，但要把目标尺寸报上去，父级 CoverChildren 才知道该多高
    if base == "DimensionSyncWidget":
        return _sync_size(node, avail_w, avail_h, rows, depth)
    # 其余逻辑 / 装饰控件不占布局空间
    if is_logic(node.tag):
        return 0.0, 0.0

    a = node.attrs
    ml, mr, mt, mb = margins(node)
    wsp = a.get("WidthSizePolicy", "StretchToParent")
    hsp = a.get("HeightSizePolicy", "StretchToParent")
    sw = num(a.get("SuggestedWidth"), 0.0)
    sh = num(a.get("SuggestedHeight"), 0.0)

    # 外部标准件引用：未显式声明尺寸策略时，用被引用件的已知尺寸
    if node.tag.startswith("Standard."):
        std_w, std_h = STD_SIZE.get(node.tag, (None, None))
        if "WidthSizePolicy" not in a and std_w:
            wsp, sw = "Fixed", std_w
        if "HeightSizePolicy" not in a and std_h:
            hsp, sh = "Fixed", std_h

    inner_w = max(0.0, avail_w - ml - mr)
    inner_h = max(0.0, avail_h - mt - mb)

    # ---- 网格布局（GridWidget / LayoutImp="GridLayout"）
    grid = grid_spec(node, rows)
    if grid is not None:
        cols, cell_w, cell_h, count = grid
        nrows = int(math.ceil(count / float(cols))) if count else 0
        if wsp == "Fixed":
            w = sw
        elif wsp == "CoverChildren":
            w = cols * cell_w
        else:
            w = inner_w if avail_w > 0 else sw
        if hsp == "Fixed":
            h = sh
        elif hsp == "CoverChildren":
            h = nrows * cell_h
        else:
            h = inner_h if avail_h > 0 else sh
        return _clamp(node, w, h)

    # ---- 宽
    if wsp == "Fixed":
        w = sw
    elif wsp == "StretchToParent":
        w = inner_w if avail_w > 0 else sw
    elif wsp == "CoverChildren":
        if is_list(node.tag):
            w = stack_extent(node, inner_w, inner_h, rows, "x", depth)
        else:
            w = 0.0
            for c in node.children:
                cm = margins(c)
                cw, _ = measure(c, inner_w - cm[0] - cm[1], inner_h - cm[2] - cm[3], rows, depth + 1)
                w = max(w, cw + cm[0] + cm[1])
            if w <= 0:
                w = sw
    else:
        w = sw

    # ---- 高
    if hsp == "Fixed":
        h = sh
    elif hsp == "StretchToParent":
        h = inner_h if avail_h > 0 else sh
    elif hsp == "CoverChildren":
        if is_list(node.tag):
            h = stack_extent(node, inner_w, inner_h, rows, "y", depth)
        elif is_text(node.tag):
            txt = display_text(node)
            _, eh = est_text_metrics(txt, font_size(node), inner_w)
            h = max(eh, sh)
        else:
            h = 0.0
            for c in node.children:
                cm = margins(c)
                _, chh = measure(c, inner_w - cm[0] - cm[1], inner_h - cm[2] - cm[3], rows, depth + 1)
                h = max(h, chh + cm[2] + cm[3])
            if h <= 0:
                h = sh
    else:
        h = sh

    # 文本控件若被撑满且高度来源不明，保底给一行
    if is_text(node.tag) and h <= 0.5:
        h = max(sh, font_size(node) * 1.45)

    return _clamp(node, max(0.0, w), max(0.0, h))


def stack_extent(node, inner_w, inner_h, rows, axis, depth):
    """算 ListPanel 在 CoverChildren 下沿 axis('x'/'y') 的尺寸。"""
    items = []
    if node.item_template is not None:
        items = [node.item_template] * max(1, rows)
    else:
        items = list(node.children)
    if not items:
        return 0.0

    spacing = num(node.attrs.get("Spacing"), 0.0)
    method = node.attrs.get("StackLayout.LayoutMethod", "VerticalTopToBottom")
    horizontal = method.startswith("Horizontal")

    total = 0.0
    cross = 0.0
    for it in items:
        im = margins(it)
        cw, ch = measure(it, inner_w - im[0] - im[1], inner_h - im[2] - im[3], rows, depth + 1)
        along = cw if horizontal else ch
        across = ch if horizontal else cw
        total += along + (im[0] + im[1] if horizontal else im[2] + im[3])
        cross = max(cross, across + (im[2] + im[3] if horizontal else im[0] + im[1]))
    total += spacing * (len(items) - 1)
    return total if (axis == "x") == horizontal else cross


# ---------------------------------------------------------------- 定位

class Entry(object):
    def __init__(self, node, rect, depth):
        self.node = node
        self.rect = rect
        self.depth = depth

    @property
    def kind(self):
        return classify(self.node.tag)


def align_offset(halign, valign, pw, ph, w, h, ml, mr, mt, mb):
    if halign == "Left":
        x = ml
    elif halign == "Right":
        x = pw - w - mr
    elif halign in ("Center", "CenterHorizontal"):
        x = (pw - w) / 2.0 + (ml - mr) / 2.0
    else:
        x = ml

    if valign == "Top":
        y = mt
    elif valign == "Bottom":
        y = ph - h - mb
    elif valign in ("Center", "CenterVertical"):
        y = (ph - h) / 2.0 + (mt - mb) / 2.0
    else:
        y = mt
    return x, y


def layout(node, rect, out, rows, depth=0):
    if depth > 24:
        return
    out.append(Entry(node, rect, depth))
    x, y, w, h = rect
    # rect 已经是该控件「按自身 margin 相对父级内容区定位」之后占据的实际区域，
    # 所以它同时就是对子级而言的内容区；子级各自的 margin 在定位时才生效。
    cx, cy, cw, ch = x, y, w, h

    # ---- 网格：把子项按 列×格 摆进格子里
    grid = grid_spec(node, rows)
    if grid is not None:
        cols, cell_w, cell_h, _count = grid
        for idx, it in enumerate(items_of(node, rows)):
            im = margins(it)
            ix = cx + (idx % cols) * cell_w
            iy = cy + (idx // cols) * cell_h
            iw, ih = measure(it, max(0.0, cell_w - im[0] - im[1]),
                             max(0.0, cell_h - im[2] - im[3]), rows, depth + 1)
            pw_ = max(0.0, cell_w - im[0] - im[1]) \
                if it.attrs.get("WidthSizePolicy") == "StretchToParent" else iw
            ph_ = max(0.0, cell_h - im[2] - im[3]) \
                if it.attrs.get("HeightSizePolicy") == "StretchToParent" else ih
            layout(it, (ix + im[0], iy + im[2], pw_, ph_), out, rows, depth + 1)
        return

    if is_list(node.tag):
        items = []
        if node.item_template is not None:
            items = [node.item_template] * max(1, rows)
        else:
            items = list(node.children)
        spacing = num(node.attrs.get("Spacing"), 0.0)
        method = node.attrs.get("StackLayout.LayoutMethod", "VerticalTopToBottom")
        horizontal = method.startswith("Horizontal")
        reverse = method.endswith(("RightToLeft", "BottomToTop"))

        sizes = [measure(it, cw, ch, rows, depth + 1) for it in items]
        if horizontal:
            total = sum(s[0] + margins(it)[0] + margins(it)[1]
                        for it, s in zip(items, sizes)) + spacing * max(0, len(items) - 1)
        else:
            total = sum(s[1] + margins(it)[2] + margins(it)[3]
                        for it, s in zip(items, sizes)) + spacing * max(0, len(items) - 1)

        cursor = cx + (cw if (reverse and horizontal) else 0.0)
        cursor_y = cy + (ch if (reverse and not horizontal) else 0.0)

        for it, (iw, ih) in zip(items, sizes):
            im = margins(it)
            pw_ = max(0.0, cw - im[0] - im[1]) if it.attrs.get("WidthSizePolicy") == "StretchToParent" else iw
            ph_ = max(0.0, ch - im[2] - im[3]) if it.attrs.get("HeightSizePolicy") == "StretchToParent" else ih
            if horizontal:
                if reverse:
                    cursor -= (im[1] + iw)
                    px = cursor
                    cursor -= (im[0] + spacing)
                else:
                    px = cursor + im[0]
                    cursor += (im[0] + iw + im[1] + spacing)
                _, oy = align_offset("Left", it.attrs.get("VerticalAlignment", "Top"),
                                     cw, ch, pw_, ph_, 0, 0, im[2], im[3])
                layout(it, (px, cy + oy, pw_, ph_), out, rows, depth + 1)
            else:
                if reverse:
                    cursor_y -= (im[3] + ih)
                    py = cursor_y
                    cursor_y -= (im[2] + spacing)
                else:
                    py = cursor_y + im[2]
                    cursor_y += (im[2] + ih + im[3] + spacing)
                ox, _ = align_offset(it.attrs.get("HorizontalAlignment", "Left"), "Top",
                                     cw, ch, pw_, ph_, im[0], im[1], 0, 0)
                layout(it, (cx + ox, py, pw_, ph_), out, rows, depth + 1)
    else:
        for c in node.children:
            im = margins(c)
            iw, ih = measure(c, cw, ch, rows, depth + 1)
            ox, oy = align_offset(c.attrs.get("HorizontalAlignment", "Left"),
                                  c.attrs.get("VerticalAlignment", "Top"),
                                  cw, ch, iw, ih, im[0], im[1], im[2], im[3])
            layout(c, (cx + ox, cy + oy, iw, ih), out, rows, depth + 1)


# ---------------------------------------------------------------- 渲染

PALETTE = {
    "mask": ("#000000", 0.06, "#5f6368"),
    "container": ("#ffffff", 0.0, "#9aa0a6"),
    "text": ("#e8f0fe", 1.0, "#4285f4"),
    "button": ("#fce8e6", 1.0, "#d93025"),
    "list": ("#e6f4ea", 1.0, "#188038"),
    "scroll": ("#f3e8fd", 1.0, "#8430ce"),
    "input": ("#fef7e0", 1.0, "#f9ab00"),
    "standard": ("#e0f7fa", 1.0, "#00838f"),
}

# ---------------------------------------------------------------- 真渲染模式
#
# 与「推导几何」的区别：几何图是**分析视图**（分类色底 ＋ 描边 ＋ 尺寸标注 ＋ 网格），
# 真渲染是**外表视图**——只画真机会画出来的东西：真贴图（已按 Color 染色）、真底色、
# 真文字。**不画**分类色、不画描边、不画标注、不画网格。
#
# 纪律：渲染模式下「什么也不画的容器」就当它透明（真机里就是透明），
# 不拿分类色去补，否则又是分析图。

RENDER_BG = "#2B2825"   # 深灰褐底：面板在游戏里是浮在 3D 场景上的，给个中性暗底便于判读
RENDER_TEXT_FALLBACK = "#FFFFFF"   # 取不到 Brush.FontColor 时的字色（不编造别的颜色）

# 游戏字体名 -> 本机可用的等价字体族。
# 关键一条：游戏简中字体就叫 simkai（楷体），而 Windows 自带 simkai.ttf（族名 KaiTi）
# ⇒ 中文字形几乎同源，不需要从 .tpac 里抠 8192×8192 的字形图集。
# 用字体族名让浏览器自己找（file:// 与 http 都能解析），不复制字体文件。
FONT_STACK = {
    "simkai": 'KaiTi,STKaiti,"楷体",serif',
    "galahad": '"Segoe UI",Tahoma,sans-serif',
    "firasans": '"Segoe UI",Tahoma,sans-serif',
    "nanumgothic": '"Malgun Gothic",sans-serif',
    "sourcehansans": '"Yu Gothic UI","Microsoft YaHei",sans-serif',
}
FONT_STACK_DEFAULT = 'KaiTi,STKaiti,"楷体","Microsoft YaHei",serif'

_ALIGN_ANCHOR = {"Left": "start", "Center": "middle", "CenterHorizontal": "middle",
                 "Right": "end"}


def font_stack(name):
    """游戏字体名（如 "simkai" / "Galahad"）-> SVG font-family 串。"""
    if name:
        key = name.lower().replace(" ", "").replace("-", "").replace("_", "")
        for k, v in FONT_STACK.items():
            if k in key:
                return v
    return FONT_STACK_DEFAULT


def is_mask(node):
    a = node.attrs
    return (a.get("WidthSizePolicy") == "StretchToParent"
            and a.get("HeightSizePolicy") == "StretchToParent"
            and not a.get("SuggestedWidth") and not a.get("SuggestedHeight")
            and classify(node.tag) == "container"
            and num(a.get("AlphaFactor"), 1.0) < 1.0)


def esc(s):
    return html.escape(str(s), quote=True)


KEY_ATTRS = ("WidthSizePolicy", "HeightSizePolicy", "SuggestedWidth", "SuggestedHeight",
             "MarginLeft", "MarginRight", "MarginTop", "MarginBottom", "Spacing",
             "HorizontalAlignment", "VerticalAlignment", "StackLayout.LayoutMethod",
             "Text", "RealText", "IsVisible", "Command.Click", "Command.TextEntered")


def info_text(e):
    x, y, w, h = [round(v, 1) for v in e.rect]
    head = "%s  %g×%g  @(%g, %g)" % (e.node.tag, w, h, x, y)
    bits = []
    for k in KEY_ATTRS:
        v = e.node.attrs.get(k)
        if v:
            bits.append("%s=%s" % (k, v))
    col = widget_color(e.node)
    if col:
        bits.append("色=%s(%s)" % (col[0], col[2]))
    elif e.node.attrs.get("Sprite"):
        bits.append("色=sprite贴图(无真色)")
    return head + ("  |  " + "  ".join(bits) if bits else "")


def sprite_tag(e, x, y, w, h):
    """控件的真贴图 -> SVG <image> 标签串（无贴图返回 None）。

    九宫格 Brush 先按 Extend* 预合成目标尺寸的 PNG（四角原样、边单向拉伸），
    其余（图标/背景/程序块）整体拉伸铺满。贴图已按控件 Color 染过色
    （见 widget_sprite -> ATLAS.tint_png），所以这里不再叠任何颜色。
    """
    spr = widget_sprite(e.node)
    if spr is None:
        return None
    spath, sw, sh, nine = spr
    alpha_attr = e.node.attrs.get("AlphaFactor")
    opa = ""
    if alpha_attr:
        try:
            opa = ' opacity="%s"' % max(0.0, min(1.0, float(alpha_attr)))
        except ValueError:
            pass
    if nine:
        composed = ATLAS.nine_png(spath, nine, w, h)
        if composed:
            spath, opa = composed, ""  # 合成时不透明度由像素本身携带
    return ('<image pointer-events="none" x="%s" y="%s" width="%s" height="%s" '
            'href="%s" preserveAspectRatio="none"%s/>'
            % (x, y, w, h, esc(_file_url(spath)), opa))


def text_layer(entries, vx, vy, vw, vh, movie=None):
    """真文字层：按**游戏真字体**（简中=楷体）、真字号、真字色、真对齐画。

    只画**能解出内容**的文本；`Text="@绑定名"` 那种运行时才有值的（联系人名之类）
    解不出就不画 —— 渲染图里不留假字，也不留占位符。
    """
    out = []
    for e in entries:
        if not is_text(e.node.tag):
            continue
        txt = display_text(e.node, strict=True, movie=movie)
        if not is_known_text(txt):
            continue
        x, y, w, h = [round(v, 1) for v in e.rect]
        if w <= 0 or h <= 0:
            continue
        if x + w < vx or y + h < vy or x > vx + vw or y > vy + vh:
            continue
        fs = font_size(e.node, 16.0)
        if fs <= 0:
            continue
        col = widget_color(e.node)
        fill = col[0] if col else RENDER_TEXT_FALLBACK
        alpha = col[1] if col else 1.0
        op = ' fill-opacity="%g"' % round(alpha, 3) if alpha < 1 else ""

        a = e.node.attrs
        anchor = _ALIGN_ANCHOR.get(a.get("HorizontalAlignment") or "", "start")
        if anchor == "middle":
            tx = x + w / 2.0
        elif anchor == "end":
            tx = x + w
        else:
            tx = x
        valign = a.get("VerticalAlignment") or "Center"
        lh = fs * 1.45
        if METRICS is not None:
            try:
                lh = METRICS.line_height(text_font(e.node), fs) or lh
            except Exception:  # noqa: BLE001
                pass
        asc = lh * 0.78          # .fnt 的 base/lineHeight ≈ 30/41
        if valign == "Top":
            ty = y + asc
        elif valign == "Bottom":
            ty = y + h - (lh - asc)
        else:
            ty = y + (h - lh) / 2.0 + asc
        out.append('<text pointer-events="none" x="%s" y="%s" font-family="%s" '
                   'font-size="%g" fill="%s"%s text-anchor="%s">%s</text>'
                   % (round(tx, 1), round(ty, 1), font_stack(text_font(e.node)),
                      round(fs, 2), fill, op, anchor, esc(txt)))
    return out


def svg_for(entries, panel_name, scale_note, view=None, label_mode="full",
            fixed_size=None, show_note=True, render=False, render_bg=None):
    vx, vy, vw, vh = view if view else (0.0, 0.0, float(CANVAS_W), float(CANVAS_H))
    if fixed_size:
        size_attr = 'width="%d" height="%d"' % (int(fixed_size[0]), int(fixed_size[1]))
    else:
        size_attr = 'width="100%%"'
    parts = []
    parts.append('<svg xmlns="http://www.w3.org/2000/svg" viewBox="%s %s %s %s" '
                 '%s preserveAspectRatio="xMidYMid meet" '
                 'font-family="-apple-system,Segoe UI,Microsoft YaHei,sans-serif">'
                 % (round(vx, 1), round(vy, 1), round(vw, 1), round(vh, 1), size_attr))
    parts.append('<rect x="%s" y="%s" width="%s" height="%s" fill="%s" stroke="%s"/>'
                 % (round(vx, 1), round(vy, 1), round(vw, 1), round(vh, 1),
                    (render_bg or RENDER_BG) if render else "#fafafa",
                    "none" if render else "#dadce0"))

    if not render:
        # 50px 网格（仅作视觉参考，不对应原版栅格）——渲染模式下不要，它不属于画面
        step = 50
        gx = math.ceil(vx / step) * step
        while gx < vx + vw:
            parts.append('<line x1="%d" y1="%s" x2="%d" y2="%s" stroke="#eeeeee" stroke-width="1"/>'
                         % (gx, round(vy, 1), gx, round(vy + vh, 1)))
            gx += step
        gy = math.ceil(vy / step) * step
        while gy < vy + vh:
            parts.append('<line x1="%s" y1="%d" x2="%s" y2="%d" stroke="#eeeeee" stroke-width="1"/>'
                         % (round(vx, 1), gy, round(vx + vw, 1), gy))
            gy += step

    # 两段式：先画全部分类色矩形，再统一画贴图。
    # 为什么：XML 里后出现的兄弟容器（如消息列表 ScrollablePanel）的分类色底是
    # **可视化假底**，若按文档序画会把先画好的真贴图盖掉（真机里那底是透明的）。
    imgs = []
    for e in entries:
        x, y, w, h = [round(v, 1) for v in e.rect]
        if w <= 0 or h <= 0:
            continue
        if x + w < vx or y + h < vy or x > vx + vw or y > vy + vh:
            continue
        if render:
            # 真渲染：文字留给后面的文字层（要用真字号/对齐）；其余只画真会画出来的东西。
            if is_text(e.node.tag):
                continue
            tag = sprite_tag(e, x, y, w, h)
            if tag is not None:
                imgs.append(tag)
                continue
            col = widget_color(e.node)
            # 只有控件自己的 Color= 才是「底色」；Brush 的 FontColor 是字色，
            # 拿去填容器会把透明容器画成一块字色板 —— 那又是编造。
            if col is not None and col[2] == "attr":
                hexv, alpha = col[0], col[1]
                op = ' fill-opacity="%s"' % alpha if alpha < 1 else ""
                parts.append('<rect x="%s" y="%s" width="%s" height="%s" fill="%s"%s/>'
                             % (x, y, w, h, hexv, op))
            # 三样都没有 ⇒ 真机里它什么都不画，这里也不画（不拿分类色补）
            continue

        kind = "mask" if is_mask(e.node) else e.kind
        fill, opacity, stroke = PALETTE.get(kind, PALETTE["container"])
        dash = ' stroke-dasharray="4 3"' if kind in ("list", "scroll") else ""
        op = ' fill-opacity="%s"' % opacity if opacity < 1 else ""
        # C 线：真色上色（不编造）。attr=控件自身 Color；brush=文本 FontColor。
        col = widget_color(e.node)
        if col is not None:
            hexv, alpha, src = col
            if src == "attr":
                # 容器真底色（如侧栏 AlphaFactor 0.08 白）：fill 用真值
                fill = hexv
                opacity = alpha
                op = ' fill-opacity="%s"' % opacity if opacity < 1 else ""
            else:
                # 文本真字色：SVG 里文本控件只有矩形，把**边框**染成字色最直观
                stroke = hexv
        parts.append('<rect class="w" data-info="%s" x="%s" y="%s" width="%s" height="%s" '
                     'fill="%s"%s stroke="%s" stroke-width="%s"%s/>'
                     % (esc(info_text(e)), x, y, w, h, fill, op, stroke,
                        1.6 if e.depth <= 2 else 1.0, dash))
        # 质感线：有原版贴图就铺上。
        tag = sprite_tag(e, x, y, w, h)
        if tag is not None:
            imgs.append(tag)
    parts.extend(imgs)

    if render:
        # 面板名（去掉 .xml）= Gauntlet 的电影名 = 找它自己 ViewModel 的钥匙
        movie = os.path.splitext(os.path.basename(panel_name or ""))[0] or None
        parts.extend(text_layer(entries, vx, vy, vw, vh, movie))

    # 标签层（后画，保证在最上；不吃鼠标事件）。渲染模式下整层不要。
    if label_mode != "none" and not render:
        for e in entries:
            x, y, w, h = [round(v, 1) for v in e.rect]
            if x + w < vx or y + h < vy or x > vx + vw or y > vy + vh:
                continue
            if label_mode == "compact":
                # 只标尺寸：小控件不标，避免标签互相盖住（截图用）
                if w < 30 or h < 14:
                    continue
                fs = 11 if h >= 20 else 9
                parts.append('<text pointer-events="none" x="%s" y="%s" font-size="%d" '
                             'fill="#3c4043" stroke="#ffffff" stroke-width="3" '
                             'paint-order="stroke">%g×%g</text>'
                             % (round(x + 4, 1), round(y + fs + 2, 1), fs, w, h))
                continue
            if w < 26 or h < 13:
                continue
            base = e.node.tag.split(".")[-1]
            txt = display_text(e.node)
            if is_mask(e.node):
                label = "遮罩 StretchToParent"
            elif txt:
                short = txt if len(txt) <= 16 else txt[:15] + "…"
                label = "%s" % short
            else:
                label = "%s %gx%g" % (base, w, h)
            fs = 12 if h >= 18 else 10
            ty = y + fs + 2
            parts.append('<text pointer-events="none" x="%s" y="%s" font-size="%d" fill="#202124" '
                         'stroke="#ffffff" stroke-width="3" paint-order="stroke">%s</text>'
                         % (round(x + 4, 1), round(ty, 1), fs, esc(label)))
            if h >= 30 and not txt:
                parts.append('<text pointer-events="none" x="%s" y="%s" font-size="10" fill="#5f6368" '
                             'stroke="#ffffff" stroke-width="3" paint-order="stroke">%g×%g</text>'
                             % (round(x + 4, 1), round(y + fs + 15, 1), w, h))

    if show_note:
        parts.append('<text pointer-events="none" x="%s" y="%s" font-size="13" fill="#5f6368">%s</text>'
                     % (round(vx + 10, 1), round(vy + vh - 10, 1), esc(scale_note)))
    parts.append("</svg>")
    return "\n".join(parts)


def legend_html():
    order = [("container", "容器 Widget"), ("text", "文本"), ("button", "按钮"),
             ("input", "输入框"), ("list", "列表 ListPanel"), ("scroll", "滚动区"),
             ("standard", "标准件引用"), ("mask", "全屏遮罩")]
    chips = []
    for kind, name in order:
        fill, op, stroke = PALETTE[kind]
        chips.append('<span class="chip"><i style="background:%s;border-color:%s"></i>%s</span>'
                     % (fill, stroke, name))
    return '<div class="legend">%s</div>' % "".join(chips)


# ---------------------------------------------------------------- 审计规则

# 基准来自对骑砍原版 430 个 Prefab / 11026 控件的只读扫描，
# 结论落在 docs/UI-LAYOUT-BASELINE-FROM-NATIVE-20260913.md。
BASELINE = {
    "name": "native-prefab-grid-20260913",
    "source": "docs/UI-LAYOUT-BASELINE-FROM-NATIVE-20260913.md",
    "canvas": [CANVAS_W, CANVAS_H],
    "grid_step": 5,
    "allow_small": [1, 2, 3],
    "note": ("原版参考分辨率 1920×1080；间距与尺寸以 5 的倍数为栅格"
             "（5/10/15/20/25/30/50/60…），小值 1/2/3 仅用于贴边微调。"),
}

# 参与 5 系栅格检查的属性，拆成两类：
#   间距类（margin / spacing）→ warn，直接决定「排布的节奏」
#   尺寸类（suggested size）→ info，控件尺寸可能受字体/内容约束，参考性更强
GRID_ATTRS_SPACING = ("MarginLeft", "MarginRight", "MarginTop", "MarginBottom", "Spacing")
GRID_ATTRS_SIZE = ("SuggestedWidth", "SuggestedHeight")
# 参与「未常量化的大数值」检查的属性
LITERAL_ATTRS = ("MarginLeft", "MarginRight", "MarginTop", "MarginBottom")

MAGIC_LITERAL_THRESHOLD = 100


def _is_symbolic(raw):
    """值是 !常量 或 *参数 表达式 —— 归原版的常量体系管，不按字面栅格评判。"""
    return str(raw).strip().startswith(("!", "*"))


def _is_literal_number(raw):
    s = str(raw).strip()
    if s == "" or s.startswith("!") or s.startswith("*"):
        return False
    try:
        float(s)
        return True
    except ValueError:
        return False


def _nearest_grid(v, step=5):
    return int(round(v / float(step))) * step


def audit_prefab(entries):
    """按原版基准审计单个 Prefab，返回问题列表（每条含 rule/severity/位置/建议）。

    注意：entries 里同一个 Node 会因 ListPanel 夹具行数出现多次（同一个对象），
    审计按控件去重，避免同一处问题被报 N 遍。
    """
    issues = []
    cur = [None]  # 当前控件：add() 靠它把出处（文件 / 行号 / 节点路径）一并带上

    def add(rule, severity, widget, attr, value, message, suggest=None):
        item = {"rule": rule, "severity": severity, "widget": widget,
                "attr": attr, "value": round(float(value), 3), "message": message}
        node = cur[0]
        if node is not None:
            if node.src:
                item["file"] = os.path.basename(node.src)
            if node.line:
                item["line"] = node.line
            item["path"] = node.path()
            if node.id:
                item["id"] = node.id
        if suggest is not None:
            item["suggest"] = suggest
        issues.append(item)

    def check_colors(attrs, widget, e):
        """规则 5 + 6：颜色位宽与**位序**。

        规则 5（位宽）：颜色必须是 #RRGGBBAA（8 位十六进制）。
          原版五个官方模块的 Prefab 里 Color= 出现 729 处，**全部是 8 位**，零例外 ⇒ 可严格判定。
          10 位值会被引擎静默丢弃（本项目已犯两次，表现为控件渲染成全白/无色）；
          其它长度（6 位缺 alpha 等）行为未经验证，一并提示，不臆断具体后果。

        规则 6（位序）：**末字节才是 alpha**（一手判定见 docs/UI-REF-ALICEMM-20260915.md §3）。
          判据**不能**写成「首位是 FF 就错」—— 官方 160 处「FF 开头且末字节非 FF」里有 157 处是
          「R 通道恰好=FF 的纯色」（#FF0000CC / #FFFFFF55 这种，两种读法都通），照抄会大面积假阳性。
          本库（AWAKE）的颜色一律是「不透明 RGB ＋ AlphaFactor 管透明度」⇒ 判据很干净：
          **8 位色的末字节不是 FF 就是写错了**（已对的 13 处全部符合：#000000FF / #FFFFFFFF / #FFD700FF）。
          一手依据：官方 Prefab 里半透明黑写成 #000000XX 有 105 处、写成 #XX000000 有 **0** 处。
        """
        for ckey in ("Color", "Brush.FontColor"):
            craw = attrs.get(ckey)
            if craw is None or _is_symbolic(craw):
                continue
            cs = str(craw).strip()
            if not cs.startswith("#"):
                continue
            if len(cs) != 9:
                add("color_format", "warn", widget, ckey, 0.0,
                    "%s=%s 是 %d 位十六进制，不是 #RRGGBBAA 的 8 位（原版 729 处全为 8 位）；"
                    "10 位会被引擎静默丢弃并渲染成全白/无色，其它长度行为未验"
                    % (ckey, cs, len(cs) - 1))
                continue
            try:
                alpha = int(cs[7:9], 16)
                cr, cg, cb = (int(cs[1:3], 16), int(cs[3:5], 16), int(cs[5:7], 16))
            except ValueError:
                continue
            if alpha == 255:
                continue
            af = num(e.node.attrs.get("AlphaFactor"), 1.0)
            eff = (alpha / 255.0) * af
            # 「应为」按本库口径算：本库第一位 FF 一直是"不透明"前缀，真颜色是**后六位**。
            # ⚠️ 不能拿 cr/cg/cb 拼（那是引擎实际读出来的 RGB），那会给出 #FF0000FF 这种错建议。
            body = cs[3:9].upper()
            note = "⇒ 整块**完全不可见**！" if alpha == 0 else "⇒ 比设计稿淡"
            add("color_channel_order", "warn", widget, ckey, 0.0,
                "%s=%s 按官方 #RRGGBBAA 读 = RGB(%d,%d,%d) ＋ alpha %d%%（再乘 AlphaFactor=%g 后约 %d%%）"
                "—— 首位 FF 不是「不透明」，它被当成了红通道。本库颜色一律写 #RRGGBBFF、"
                "透明度交给 AlphaFactor ⇒ 这里应为 #%sFF。%s"
                % (ckey, cs, cr, cg, cb, round(alpha / 255.0 * 100), af,
                   round(eff * 100), body, note))

    seen = set()
    for e in entries:
        nid = id(e.node)
        if nid in seen:
            continue
        seen.add(nid)
        cur[0] = e.node
        # 看**原始写法**：内联 *参数/!常量 后的数字是算出来的，不代表作者写死了一个越格值
        a = getattr(e.node, "orig_attrs", None) or e.node.attrs
        widget = e.node.tag.split(".")[-1]

        # 颜色规则（5/6）**必须排在 is_logic 跳过之前**：
        #   「逻辑控件不参与布局」不等于「不参与绘制」—— 挂了 Sprite/Color 照样会画出来。
        #   本项目那条"最新一条"的 2px 金条就挂在 DimensionSyncWidget 上
        #   （NpcDialogue.xml:180 Color="#FFD9A953"），按老写法会整条漏掉。
        check_colors(a, widget, e)

        # 逻辑 / 装饰控件（导航目标器、尺寸同步器、提示挂载）不参与布局，也不该报布局问题
        if is_logic(e.node.tag):
            continue

        # 规则 1a：间距类 5 系栅格（主规则）——治「同一层级间距不成体系」
        for key in GRID_ATTRS_SPACING:
            raw = a.get(key)
            if raw is None or _is_symbolic(raw):
                continue
            fv = num(raw, 0.0)
            if fv == 0 or fv % BASELINE["grid_step"] == 0 or fv in BASELINE["allow_small"]:
                continue
            sug = _nearest_grid(fv)
            add("grid_off_5", "warn", widget, key, fv,
                "%s=%g 不在 5 系栅格上（原版习惯是 5 的倍数），建议 %d" % (key, fv, sug), sug)

        # 规则 1b：尺寸类 5 系栅格（参考值）
        for key in GRID_ATTRS_SIZE:
            raw = a.get(key)
            if raw is None or _is_symbolic(raw):
                continue
            fv = num(raw, 0.0)
            if fv == 0 or fv % BASELINE["grid_step"] == 0 or fv in BASELINE["allow_small"]:
                continue
            sug = _nearest_grid(fv)
            add("grid_off_5_size", "info", widget, key, fv,
                "%s=%g 不在 5 系栅格上，建议 %d（控件尺寸，可能受字体/内容约束，仅供参考）"
                % (key, fv, sug), sug)

        # 规则 2：未常量化的字面大数值——原版会用 <Constants> + !名字 表达关系
        for key in LITERAL_ATTRS:
            raw = a.get(key)
            if raw is None or not _is_literal_number(raw):
                continue
            fv = float(str(raw).strip())
            if fv < MAGIC_LITERAL_THRESHOLD:
                continue
            add("magic_large_literal", "info", widget, key, fv,
                "%s=%g 是写死的字面数值；原版把它定义在 <Constants> 里、用 !名字 引用，"
                "改一处即全局生效（本项多为按钮宽度之和这类咬合链）" % (key, fv))

        # 规则 3：声明 Fixed 却没给尺寸——推导尺寸会塌成 0
        sw_raw, sh_raw = a.get("SuggestedWidth"), a.get("SuggestedHeight")
        if (a.get("WidthSizePolicy") == "Fixed"
                and not _is_symbolic(sw_raw) and num(sw_raw, 0.0) <= 0):
            add("fixed_without_size", "error", widget, "SuggestedWidth", 0.0,
                "WidthSizePolicy=Fixed 但 SuggestedWidth 缺失或为 0，推导宽度为 0")
        if (a.get("HeightSizePolicy") == "Fixed"
                and not _is_symbolic(sh_raw) and num(sh_raw, 0.0) <= 0):
            add("fixed_without_size", "error", widget, "SuggestedHeight", 0.0,
                "HeightSizePolicy=Fixed 但 SuggestedHeight 缺失或为 0，推导高度为 0")

        # 规则 4：推导宽高为 0 且可见——真机上要么不可见要么塌陷
        if str(a.get("IsVisible", "true")).lower() != "false" and (e.rect[2] <= 0 or e.rect[3] <= 0):
            add("zero_size_widget", "warn", widget, "layout", 0.0,
                "推导宽高为 %g×%g，可能是尺寸策略与父级不匹配" % (e.rect[2], e.rect[3]))

        # 规则 5 / 6（颜色位宽与位序）已在循环开头交给 check_colors() 处理，
        # 因为它必须对**逻辑控件**也生效（挂 Sprite+Color 的 DimensionSyncWidget 会画出来）。

    return issues


def summarize_issues(issues):
    by_rule = {}
    by_severity = {}
    by_value = {}
    for i in issues:
        by_rule[i["rule"]] = by_rule.get(i["rule"], 0) + 1
        by_severity[i["severity"]] = by_severity.get(i["severity"], 0) + 1
        key = "%s=%g" % (i["attr"], i["value"])
        rec = by_value.get(key)
        if rec is None:
            rec = {"attr": i["attr"], "value": i["value"], "sample": i["widget"],
                   "suggest": i.get("suggest"), "count": 0}
            by_value[key] = rec
        rec["count"] += 1
    ranked = sorted(by_value.values(), key=lambda r: (-r["count"], r["attr"]))
    return {"total": len(issues), "by_rule": by_rule, "by_severity": by_severity,
            "by_value": ranked}


def list_prefabs(prefab_dir):
    if not os.path.isdir(prefab_dir):
        return []
    return sorted(f for f in os.listdir(prefab_dir) if f.lower().endswith(".xml"))


# ---------------------------------------------------------------- 主流程

def build(prefab_dir, out_dir, rows, only=None, render=True, collect=None, native_dirs=None):
    if render:
        os.makedirs(out_dir, exist_ok=True)

    # Prefab 搜索路径：本工程目录 + 原版（用于展开外部 Prefab 引用）
    search = [prefab_dir]
    extra = detect_native_prefab_dirs() if native_dirs is None else native_dirs
    for d in extra:
        if d and d not in search:
            search.append(d)
    set_prefab_search_dirs(search)

    # 文本度量层：首次 build 时按需装配（调用方可提前 init_metrics 覆盖，
    # 或用环境变量 AWAKE_UILAB_NO_METRICS=1 显式关掉）
    if METRICS is None and not os.environ.get("AWAKE_UILAB_NO_METRICS"):
        _mods = detect_native_prefab_dirs()
        init_metrics(prefab_dir, native_dir=(_mods[0] if _mods else None))
    # Sprite 贴图层：同按需装配（AWAKE_UILAB_NO_SPRITES=1 关）
    if ATLAS is None and not os.environ.get("AWAKE_UILAB_NO_SPRITES"):
        init_atlas()

    files = list_prefabs(prefab_dir)
    if only:
        wanted = set(only)
        files = [f for f in files if f in wanted]
    report = []
    sections = []

    for fn in files:
        path = os.path.join(prefab_dir, fn)
        try:
            top, consts, params = load_prefab(path)
        except Exception as ex:  # noqa
            report.append({"prefab": fn, "error": str(ex)})
            continue
        if top is None:
            report.append({"prefab": fn, "error": "no root widget"})
            continue

        global CONSTS, PARAMS
        CONSTS = dict(consts)
        PARAMS = dict(params)
        expand_prefab_refs(top)   # 展开外部 Prefab 引用（拿真实尺寸，别让默认 StretchToParent 撑爆）
        bind_parent(top)          # 建父子链，供 DimensionSyncWidget 解析相对路径

        entries = []
        rw, rh = measure(top, CANVAS_W, CANVAS_H, rows)
        tm_ = margins(top)
        ox, oy = align_offset(top.attrs.get("HorizontalAlignment", "Left"),
                              top.attrs.get("VerticalAlignment", "Top"),
                              CANVAS_W, CANVAS_H, rw, rh, tm_[0], tm_[1], tm_[2], tm_[3])
        layout(top, (ox, oy, rw, rh), entries, rows)
        issues = audit_prefab(entries)
        bad = [(i["widget"], i["attr"], i["value"]) for i in issues if i["rule"] == "grid_off_5"]

        # 面板特写：排除全屏遮罩后的包围盒
        boxes = [e.rect for e in entries
                 if not is_mask(e.node) and e.rect[2] < CANVAS_W - 1 and e.rect[3] < CANVAS_H - 1]
        if boxes:
            pad = 40.0
            px0 = min(b[0] for b in boxes) - pad
            py0 = min(b[1] for b in boxes) - pad
            px1 = max(b[0] + b[2] for b in boxes) + pad
            py1 = max(b[1] + b[3] for b in boxes) + pad
            closeup = (px0, py0, px1 - px0, py1 - py0)
            panel_w, panel_h = round(px1 - px0 - 2 * pad), round(py1 - py0 - 2 * pad)
        else:
            closeup = (0.0, 0.0, float(CANVAS_W), float(CANVAS_H))
            panel_w, panel_h = CANVAS_W, CANVAS_H

        if collect is not None:
            # 供 --shot 复用，避免把几何/特写逻辑再写一遍
            collect[fn] = {"entries": entries, "closeup": closeup,
                           "panel_w": panel_w, "panel_h": panel_h}

        if render:
            set_rel_base(out_dir)   # 贴图按相对 index.html 的路径写，http/file 两种打开都可用
            svg_full = svg_for(entries, fn, "%s · 全画布 %d×%d · 推导几何（非渲染）"
                               % (fn, CANVAS_W, CANVAS_H))
            svg_zoom = svg_for(entries, fn, "%s · 面板特写 · 推导几何（非渲染）" % fn, closeup)

            sec = ['<section id="%s">' % esc(fn.replace(".", "-")),
                   '<h2>%s</h2>' % esc(fn),
                   '<p class="meta">控件 %d 个 · 面板外框 %d×%d · 参考画布 %d×%d · 问题 %d 处（5 系违规 %d）</p>'
                   % (len(entries), panel_w, panel_h, CANVAS_W, CANVAS_H, len(issues), len(bad)),
                   '<div class="frame">%s</div>' % svg_zoom,
                   '<details><summary>全画布位置（看它落在屏幕哪一处）</summary>'
                   '<div class="frame">%s</div></details>' % svg_full]
            if issues:
                rows_html = "".join(
                    "<tr><td>%s</td><td>%s</td><td>%s</td><td>%g</td></tr>"
                    % (esc(i["rule"]), esc(i["widget"]), esc(i["attr"]), i["value"])
                    for i in issues)
                sec.append('<details><summary>布局审计问题（%d 处）</summary>'
                           '<table><thead><tr><th>规则</th><th>控件</th><th>属性</th><th>值</th></tr></thead>'
                           '<tbody>%s</tbody></table></details>' % (len(issues), rows_html))
            sec.append("</section>")
            sections.append("\n".join(sec))

        report.append({
            "prefab": fn,
            "widget_count": len(entries),
            "panel_box": {"w": panel_w, "h": panel_h},
            "grid_violations": len(bad),
            "violations": [{"widget": t, "attr": k, "value": v} for t, k, v in bad],
            "issues": issues,
            "issue_count": len(issues),
            "issue_summary": summarize_issues(issues),
            "geometry": [
                {"tag": e.node.tag, "depth": e.depth,
                 "x": round(e.rect[0], 1), "y": round(e.rect[1], 1),
                 "w": round(e.rect[2], 1), "h": round(e.rect[3], 1),
                 "text": display_text(e.node)}
                for e in entries if e.rect[2] > 0 and e.rect[3] > 0
            ],
        })

    index = None
    if render:
        nav = "".join('<a href="#%s">%s</a>' % (esc(f.replace(".", "-")), esc(f)) for f in files)
        page = PAGE.replace("__NAV__", nav).replace("__LEGEND__", legend_html()).replace("__BODY__", "\n".join(sections))
        index = os.path.join(out_dir, "index.html")
        with io.open(index, "w", encoding="utf-8") as fh:
            fh.write(page)
        with io.open(os.path.join(out_dir, "report.json"), "w", encoding="utf-8") as fh:
            fh.write(json.dumps({"canvas": [CANVAS_W, CANVAS_H], "rows": rows, "prefabs": report},
                                ensure_ascii=False, indent=2))
    return files, report, index


PAGE = """<!DOCTYPE html>
<html lang="zh-CN"><head><meta charset="utf-8"/>
<title>AWAKE Prefab 几何线框预览</title>
<style>
  :root { color-scheme: light; }
  * { box-sizing: border-box; }
  body { margin:0; background:#f1f3f4; color:#202124;
         font-family:-apple-system,"Segoe UI","Microsoft YaHei",sans-serif; }
  header { position:sticky; top:0; background:#fff; border-bottom:1px solid #dadce0;
           padding:14px 22px; z-index:5; }
  h1 { margin:0 0 6px; font-size:17px; }
  .warn { color:#b3261e; font-size:12.5px; }
  nav { margin-top:8px; display:flex; flex-wrap:wrap; gap:6px; }
  nav a { font-size:12.5px; text-decoration:none; color:#1a73e8;
          border:1px solid #dadce0; border-radius:11px; padding:2px 9px; background:#fff; }
  nav a:hover { background:#e8f0fe; }
  .legend { display:flex; flex-wrap:wrap; gap:10px; margin-top:8px; font-size:12px; color:#3c4043; }
  .chip { display:inline-flex; align-items:center; gap:5px; }
  .chip i { width:13px; height:13px; border:1.4px solid #999; border-radius:2px; display:inline-block; }
  main { padding:18px 22px 110px; }
  rect.w { cursor:crosshair; }
  rect.w:hover { stroke:#202124 !important; stroke-width:2.4 !important; }
  #hud { position:fixed; left:0; right:0; bottom:0; background:rgba(32,33,36,.95); color:#e8eaed;
         font:12.5px/1.6 ui-monospace,Consolas,monospace; padding:7px 16px; z-index:20;
         white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
  section { scroll-margin-top:150px; }
  section { background:#fff; border:1px solid #dadce0; border-radius:8px;
            padding:14px 16px 18px; margin-bottom:18px; }
  h2 { margin:0 0 4px; font-size:15px; font-family:ui-monospace,Consolas,monospace; }
  .meta { margin:0 0 12px; color:#5f6368; font-size:12px; }
  .frame { border:1px solid #e0e0e0; border-radius:4px; overflow:hidden; }
  details { margin-top:12px; font-size:12.5px; }
  summary { cursor:pointer; color:#b3261e; }
  table { border-collapse:collapse; margin-top:8px; font-size:12px; }
  th,td { border:1px solid #e0e0e0; padding:3px 10px; text-align:left; }
  th { background:#f8f9fa; }
</style></head>
<body>
<header>
  <h1>AWAKE Prefab 几何线框预览</h1>
  <div class="warn">推导几何，非渲染结果。颜色 / 字体 / 贴图 / 图标不还原；文本高度为估算值。最后验收仍以游戏内真机为准。</div>
  __LEGEND__
  <nav>__NAV__</nav>
</header>
<main>
__BODY__
</main>
<div id="hud">把鼠标移到任意方块上，这里会显示它的真实布局属性（控件类型 / 推导尺寸 / 坐标 / 关键属性）。</div>
<script>
(function () {
  var hud = document.getElementById('hud');
  var hint = hud.textContent;
  document.querySelectorAll('rect.w').forEach(function (el) {
    el.addEventListener('mouseenter', function () { hud.textContent = el.getAttribute('data-info'); });
    el.addEventListener('mouseleave', function () { hud.textContent = hint; });
  });
})();
</script>
</body></html>
"""


# ---------------------------------------------------------------- 截图（Chrome 无头）

CHROME_CANDIDATES = (
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
)


def find_browser(explicit=None):
    """找本机 Chrome / Edge，只用于把本地 HTML 截成 PNG（不启动游戏）。"""
    if explicit:
        return explicit if os.path.isfile(explicit) else None
    env = os.environ.get("AWAKE_UILAB_BROWSER")
    if env and os.path.isfile(env):
        return env
    for p in CHROME_CANDIDATES:
        if os.path.isfile(p):
            return p
    return None


SHOT_PAGE = """<!DOCTYPE html>
<html lang="zh-CN"><head><meta charset="utf-8"/><title>__TITLE__</title>
<style>
  html,body { margin:0; padding:0; background:__PAGEBG__; }
  .cap { height:26px; padding:0 10px; color:#5f6368;
         font:13px/26px -apple-system,"Segoe UI","Microsoft YaHei",sans-serif; }
  .cap b { color:#202124; }
  .foot { height:22px; padding:0 10px; color:#80868b;
          font:12px/22px -apple-system,"Segoe UI","Microsoft YaHei",sans-serif; }
  __DARKCSS__
</style></head>
<body>
<div class="cap"><b>__TITLE__</b> · __CAPTXT__ · 面板 __PW__×__PH__</div>
__SVG__
<div class="foot">控件 __WC__ · 问题 __IC__ 处（5 系违规 __GV__）· 基准：原版 5 的倍数栅格 · 最终验收以游戏内真机为准</div>
</body></html>
"""

CAP_H = 26
FOOT_H = 22

DARK_CSS = ('html,body { background:%s; }\n'
            '  .cap { color:#a8a29a; } .cap b { color:#efe9df; }\n'
            '  .foot { color:#8d8880; }' % RENDER_BG)


def render_shot(entries, meta, fn, out_png, browser, label_mode, scale, stats,
                render=False, render_bg=None):
    """把单个面板画成干净页（无导航 / 无悬停条），再用无头浏览器截成 PNG。

    render=True 时出的是**真渲染**（只画真贴图/真底色/真文字，无网格无标注无分类色），
    render=False（默认）时出的是**推导几何**分析图。
    """
    _, _, vw, vh = meta["closeup"]
    svg_w = int(math.ceil(vw))
    svg_h = int(math.ceil(vh))
    page_w = svg_w
    page_h = svg_h + CAP_H + FOOT_H

    bg = render_bg or RENDER_BG
    html_path = os.path.splitext(out_png)[0] + ".html"
    set_rel_base(os.path.dirname(html_path))   # 贴图相对 shot/*.html 写
    svg = svg_for(entries, fn, "", view=meta["closeup"], label_mode=label_mode,
                  fixed_size=(svg_w, svg_h), show_note=False,
                  render=render, render_bg=bg)
    page = (SHOT_PAGE
            .replace("__PAGEBG__", bg if render else "#ffffff")
            .replace("__DARKCSS__", DARK_CSS if render else "")
            .replace("__CAPTXT__", "真渲染（贴图/底色/字体按游戏口径）" if render
                     else "推导几何（非渲染）")
            .replace("__TITLE__", esc(fn))
            .replace("__PW__", str(meta["panel_w"]))
            .replace("__PH__", str(meta["panel_h"]))
            .replace("__SVG__", svg)
            .replace("__WC__", str(stats.get("widget_count", 0)))
            .replace("__IC__", str(stats.get("issue_count", 0)))
            .replace("__GV__", str(stats.get("grid_violations", 0))))

    with io.open(html_path, "w", encoding="utf-8") as fh:
        fh.write(page)

    if os.path.isfile(out_png):
        os.remove(out_png)
    url = "file:///" + os.path.abspath(html_path).replace("\\", "/")
    cmd = [browser, "--headless=new", "--disable-gpu", "--hide-scrollbars",
           "--force-device-scale-factor=%s" % scale,
           "--window-size=%d,%d" % (page_w, page_h),
           "--screenshot=%s" % os.path.abspath(out_png), url]
    try:
        proc = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                              timeout=120)
        err = (proc.stderr or b"").decode("utf-8", "replace").strip()
    except Exception as ex:  # noqa
        return False, html_path, str(ex)
    ok = os.path.isfile(out_png) and os.path.getsize(out_png) > 0
    return ok, html_path, err


def _native_dirs_from(args):
    """从 CLI 参数得出「用于展开引用」的原版 Prefab 目录列表。"""
    if getattr(args, "no_native_refs", False):
        return []
    return detect_native_prefab_dirs(getattr(args, "native_dir", None))


def _run_shot(args):
    browser = find_browser(args.chrome)
    if not browser:
        sys.stderr.write("找不到 Chrome / Edge。用 --chrome <路径> 指定，"
                         "或设环境变量 AWAKE_UILAB_BROWSER。\n")
        return 2

    as_dir = not args.shot.lower().endswith(".png")
    collect = {}
    files, report, _ = build(args.prefab_dir, args.out, args.rows,
                             args.prefab, render=False, collect=collect,
                             native_dirs=_native_dirs_from(args))
    if not files:
        sys.stderr.write("没有匹配的 Prefab：%s\n" % (args.prefab or "*"))
        return 2
    if as_dir:
        os.makedirs(args.shot, exist_ok=True)
    elif len(files) != 1:
        sys.stderr.write("--shot 指向单个 .png 时只截一个面板；"
                         "请加 --prefab 指定，或让 --shot 指向一个目录。\n")
        return 2

    print("AWAKE Prefab 截图（Chrome 无头，只读）")
    print("  浏览器 : %s" % browser)
    print("  标签   : %s · 缩放 %sx" % (args.shot_label, args.shot_scale))
    done, failed = 0, 0
    for r in report:
        fn = r["prefab"]
        if "error" in r or fn not in collect:
            print("  [ERR ] %-28s %s" % (fn, r.get("error", "no geometry")))
            failed += 1
            continue
        out_png = (os.path.join(args.shot, os.path.splitext(fn)[0] + ".png")
                   if as_dir else args.shot)
        parent = os.path.dirname(os.path.abspath(out_png))
        if parent:
            os.makedirs(parent, exist_ok=True)
        ok, html_path, err = render_shot(collect[fn]["entries"], collect[fn], fn,
                                         out_png, browser, args.shot_label,
                                         args.shot_scale, r,
                                         render=args.render, render_bg=args.render_bg)
        size = os.path.getsize(out_png) if os.path.isfile(out_png) else 0
        if ok:
            done += 1
            print("  [OK  ] %-28s → %s (%d KB)" % (fn, out_png, size // 1024))
        else:
            failed += 1
            print("  [FAIL] %-28s %s" % (fn, (err or "no output")[:300]))
    print("  完成：成功 %d · 失败 %d" % (done, failed))
    return 0 if done and not failed else 1


# ---------------------------------------------------------------- CLI

def _envelope(command, args, extra=None):
    payload = {
        "tool": "awake-ui-lab",
        "tool_version": "1",
        "command": command,
        "read_only": True,
        "prefab_dir": os.path.abspath(args.prefab_dir),
        "canvas": {"width": CANVAS_W, "height": CANVAS_H},
        "rows": args.rows,
        "baseline": BASELINE,
    }
    if extra:
        payload.update(extra)
    return payload


def _emit_json(payload):
    sys.stdout.write(json.dumps(payload, ensure_ascii=False, indent=2) + "\n")


def _print_audit_text(prefab_dir, report):
    print("AWAKE Prefab 布局审计（基准：5 的倍数栅格，来自原版扫描）")
    print("  目录 : %s" % prefab_dir)
    for r in report:
        if "error" in r:
            print("  [ERR] %s: %s" % (r["prefab"], r["error"]))
            continue
        issues = r.get("issues", [])
        s = summarize_issues(issues)
        sev = s["by_severity"]
        print("  [%-4s] %-28s 控件 %3d  问题 %2d（warn %d / error %d / info %d）" % (
            "WARN" if (sev.get("warn") or sev.get("error")) else "OK",
            r["prefab"], r["widget_count"], s["total"],
            sev.get("warn", 0), sev.get("error", 0), sev.get("info", 0)))
        if issues:
            print("        最常见的越格值（按出现次数）：")
            for v in s["by_value"][:8]:
                sug = "→ 建议 %g" % v["suggest"] if v["suggest"] is not None else ""
                print("          %-14s = %-6g ×%-3d 例如 %s  %s"
                      % (v["attr"], v["value"], v["count"], v.get("sample", ""), sug))
            located = [i for i in issues if i.get("line") and i.get("severity") == "warn"][:6]
            if located:
                print("        逐条定位（文件:行 · 节点路径 · 属性 · 建议）：")
                for i in located:
                    path = i.get("path", "")
                    if len(path) > 34:
                        path = "…" + path[-33:]
                    print("          %-22s %-34s %s=%g → %s"
                          % ("%s:%s" % (i.get("file", "?"), i.get("line", "?")), path,
                             i.get("attr"), i.get("value"), i.get("suggest", "—")))
    grand = summarize_issues([i for r in report for i in r.get("issues", [])])
    print("  问题合计 : %d  按规则 %s" % (grand["total"], grand["by_rule"]))


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    default_prefabs = os.path.normpath(os.path.join(here, "..", "..", "..", "GUI", "Prefabs"))
    ap = argparse.ArgumentParser(description="AWAKE Prefab 本地几何预览 + 布局审计（只读）")
    ap.add_argument("--prefab-dir", default=default_prefabs)
    ap.add_argument("--out", default=os.path.join(here, "out"))
    ap.add_argument("--rows", type=int, default=3, help="ListPanel 夹具行数（默认 3）")
    ap.add_argument("--prefab", action="append", help="只处理指定文件名，可重复")
    ap.add_argument("--list", dest="list_only", action="store_true", help="列出可检查的 Prefab")
    ap.add_argument("--json", action="store_true", help="输出 JSON 到 stdout（默认输出 HTML）")
    ap.add_argument("--audit", action="store_true", help="只做布局审计，不写 HTML")
    ap.add_argument("--strict", action="store_true", help="存在 warn/error 时退出码 1（给 CI / Agent 用）")
    ap.add_argument("--shot", metavar="PATH",
                    help="截图输出：给 *.png 路径=只截一个面板；给目录=每个面板一张")
    ap.add_argument("--shot-label", choices=("none", "compact", "full"), default="compact",
                    help="截图标签：none 纯线框 / compact 只标尺寸（默认）/ full 标类型名")
    ap.add_argument("--shot-scale", type=float, default=1.0, help="截图缩放（默认 1.0）")
    ap.add_argument("--render", action="store_true",
                    help="真渲染：只画真贴图/真底色/真文字，无网格无标注无分类色")
    ap.add_argument("--render-bg", default=None,
                    help="真渲染的底板色（默认 %s）" % RENDER_BG)
    ap.add_argument("--chrome", help="Chrome / Edge 可执行文件路径（默认自动探测）")
    ap.add_argument("--native-dir", help="原版 Modules 目录（或某个 Prefabs 目录）；"
                                         "用于展开原版 Prefab 引用，默认自动探测")
    ap.add_argument("--no-native-refs", action="store_true",
                    help="不展开外部 Prefab 引用（只看本工程内的几何）")
    args = ap.parse_args()

    if args.shot:
        return _run_shot(args)

    # --list：只列面板
    if args.list_only:
        found = list_prefabs(args.prefab_dir)
        if args.json:
            _emit_json(_envelope("list", args, {"prefabs": found, "count": len(found)}))
        else:
            for f in found:
                print(f)
        return 0

    render = not (args.json or args.audit)
    files, report, index = build(args.prefab_dir, args.out, args.rows, args.prefab,
                                 render=render, native_dirs=_native_dirs_from(args))

    all_issues = []
    for r in report:
        all_issues.extend(r.get("issues", []))
    summary = summarize_issues(all_issues)

    if args.json:
        prefabs = []
        for r in report:
            if "error" in r:
                prefabs.append({"prefab": r["prefab"], "ok": False, "error": r["error"]})
                continue
            entry = {
                "prefab": r["prefab"],
                "ok": r["issue_count"] == 0,
                "widget_count": r["widget_count"],
                "panel_box": r["panel_box"],
                "issue_count": r["issue_count"],
                "issue_summary": r["issue_summary"],
                "issues": r["issues"],
            }
            if not args.audit:
                entry["geometry"] = r["geometry"]
            prefabs.append(entry)
        _emit_json(_envelope("audit" if args.audit else "geometry", args, {
            "prefabs": prefabs,
            "summary": {
                "prefab_count": len(report),
                "widget_total": sum(r.get("widget_count", 0) for r in report),
                "issue_total": summary["total"],
                "by_rule": summary["by_rule"],
                "by_severity": summary["by_severity"],
                "by_value": summary["by_value"],
            },
            "exit_hint": "存在 error 或（--strict 下）warn 时退出码非 0",
        }))
    elif args.audit:
        _print_audit_text(args.prefab_dir, report)
    else:
        total_v = sum(r.get("grid_violations", 0) for r in report)
        total_i = sum(r.get("issue_count", 0) for r in report)
        total_w = sum(r.get("widget_count", 0) for r in report)
        print("AWAKE Prefab 几何线框预览")
        print("  prefab 目录 : %s" % args.prefab_dir)
        print("  输出        : %s" % index)
        print("  处理文件    : %d 个 · 控件合计 %d · 问题合计 %d（5 系违规 %d）"
              % (len(files), total_w, total_i, total_v))
        for r in report:
            if "error" in r:
                print("  [ERR] %s: %s" % (r["prefab"], r["error"]))
            else:
                print("  [%-4s] %-28s 控件 %3d  问题 %2d（5系 %2d）" % (
                    "WARN" if r.get("issue_count") else "OK",
                    r["prefab"], r["widget_count"], r["issue_count"], r["grid_violations"]))

    if args.strict and (summary["by_severity"].get("error", 0) or summary["by_severity"].get("warn", 0)):
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
