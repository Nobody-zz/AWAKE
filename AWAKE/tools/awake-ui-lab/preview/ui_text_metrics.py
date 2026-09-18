#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
AWAKE UI Lab —— 真实文本度量（可选增强层）

把「文本尺寸」从启发式估算升级为**取自游戏原版数据**的四段拼接：

  ① 字体指标 ← <game>/GUI/GauntletUI/Fonts/<font>/<font>.fnt
                （是 XML，不是标准 BMFont 文本；逐字符 xadvance + common/lineHeight）
  ② 字号/字体 ← <modules>/<Mod>/GUI/Brushes/*.xml 的 Brush 定义
                （<Brush Name=.. Font=..> + <Style Name="Default" FontSize=..>）
  ③ 语言映射 ← <modules>/Native/GUI/Fonts/NativeLanguages.xml
                （简体中文把 Galahad / FiraSans* 全部映射到 simkai）
  ④ 文本内容 ← Prefab 里 Text="@属性名" 是 Gauntlet 的 DataSource 绑定，
                属性实现在 src/*.cs 的 ViewModel，取值多为
                AwakeLocalization.Resolve("id", "回退字面量")；
                id 再查 ModuleData/Languages/**/<file>.xml 的 <string id text>

设计原则
    - **任何一段数据缺失都自动退回调用方的启发式估算**，调用方不必判断可用性。
    - 只读：不写游戏目录、不改任何 Prefab。
    - 所有推断都标「待验证」的地方写进 notes()，不假装精确。

用法
    m = build(prefab_dir=..., native_dir=..., awake_root=...)
    m.measure("发送", "simkai", 17.0, max_w=120)   # -> (w, h)
"""
import io
import math
import os
import re
import xml.etree.ElementTree as ET

# 简体中文是 AWAKE 的主语言（ModuleData/Languages/CNs/awake_strings-zh-HANS.xml）
DEFAULT_LANGUAGE = "简体中文"
# 语言文件目录（相对 AWAKE 模块根）
LANG_DIRS = (("ModuleData", "Languages"),)


# ---------------------------------------------------------------- ① 字体指标

class FontMetrics(object):
    """一个 .fnt（XML）的度量表。"""

    def __init__(self, name):
        self.name = name
        self.size = 0.0          # info/@size —— 字体原生像素高
        self.custom_scale = 1.0  # info/@customScale
        self.line_height = 0.0   # common/@lineHeight
        self.base = 0.0          # common/@base
        self.advance = {}        # char id -> xadvance
        self.glyph = {}          # char id -> (x, y, w, h)
        self._fallback_adv = 0.0

    @property
    def native(self):
        """原生渲染高（含 customScale）。"""
        return self.size * (self.custom_scale or 1.0)

    def scale_for(self, size):
        if self.native <= 0:
            return 0.0
        return float(size) / self.native

    def advance_of(self, ch):
        a = self.advance.get(ord(ch))
        if a is None:
            return self._fallback_adv
        return a

    @classmethod
    def load(cls, path, name):
        fm = cls(name)
        tree = ET.parse(path)
        root = tree.getroot()
        for el in root:
            if el.tag == "info":
                fm.size = float(el.get("size") or 0)
                fm.custom_scale = float(el.get("customScale") or 1.0)
            elif el.tag == "common":
                fm.line_height = float(el.get("lineHeight") or 0)
                fm.base = float(el.get("base") or 0)
            elif el.tag == "chars":
                for c in el:
                    if c.tag != "char":
                        continue
                    cid = int(c.get("id") or 0)
                    adv = float(c.get("xadvance") or 0)
                    fm.advance[cid] = adv
                    fm.glyph[cid] = (float(c.get("x") or 0), float(c.get("y") or 0),
                                     float(c.get("width") or 0), float(c.get("height") or 0))
        # 缺字回退：优先 id=0（未定义字形），否则取常用字宽
        fm._fallback_adv = fm.advance.get(0) or (fm.size * 0.5)
        return fm


class FontLibrary(object):
    """<game>/GUI/GauntletUI/Fonts/ 下所有字体的度量表。"""

    def __init__(self, fonts_dir):
        self.fonts_dir = fonts_dir
        self.fonts = {}
        self.notes = []

    def get(self, name):
        if name in self.fonts:
            return self.fonts[name]
        if not name or not self.fonts_dir:
            return None
        path = os.path.join(self.fonts_dir, name, name + ".fnt")
        if not os.path.isfile(path):
            self.notes.append("字体缺 .fnt：%s" % name)
            self.fonts[name] = None
            return None
        try:
            self.fonts[name] = FontMetrics.load(path, name)
        except Exception as e:  # noqa: BLE001 —— 坏字体不该拖垮整条管线
            self.notes.append("字体解析失败 %s：%s" % (name, e))
            self.fonts[name] = None
        return self.fonts[name]


# ---------------------------------------------------------------- ② Brush 字号/字体

class BrushIndex(object):
    """<Mod>/GUI/Brushes/*.xml -> {brush 名: (Font, DefaultFontSize)}"""

    def __init__(self, brush_roots):
        self.roots = [r for r in (brush_roots or []) if r and os.path.isdir(r)]
        self.index = {}
        self.loaded = 0
        self._load()

    def _load(self):
        for root in self.roots:
            for dirpath, _dirs, files in os.walk(root):
                for fn in files:
                    if not fn.lower().endswith(".xml"):
                        continue
                    path = os.path.join(dirpath, fn)
                    try:
                        tree = ET.parse(path)
                    except Exception:  # noqa: BLE001
                        continue
                    self.loaded += 1
                    for el in tree.getroot().iter("Brush"):
                        name = el.get("Name")
                        if not name:
                            continue
                        font = el.get("Font")
                        size = None
                        for styles in el:
                            if styles.tag != "Styles":
                                continue
                            for st in styles:
                                if st.tag != "Style":
                                    continue
                                if st.get("Name") in (None, "Default"):
                                    v = st.get("FontSize")
                                    if v:
                                        size = float(v)
                                    break
                        if size is None:
                            v = el.get("FontSize")
                            size = float(v) if v else None
                        # 先到先得：官方模块按 Native→SandBox→… 顺序传入
                        self.index.setdefault(name, {"font": font, "size": size,
                                                     "extends": el.get("Extends")})
                        self._record_colors(name, el)

    def _record_colors(self, name, el):
        """提取 Default 态的颜色：文本 Brush 的 FontColor、Sprite 类的标记。

        颜色纪律（与几何引擎同一原则）：只记**真值**——
          · FontColor="#RRGGBBAA" 是真色；
          · BrushLayer 只带 Sprite 没带颜色的，记 "sprite"（无色可信，不造色）。
        """
        info = self.index.get(name)
        if info is None:
            info = self.index[name] = {"font": None, "size": None, "extends": None}
        if "color" in info:
            return
        has_sprite = False
        sprite_name = None
        nine = None
        color = None
        if el.get("Sprite") or el.get("SpriteName"):
            has_sprite = True
            sprite_name = el.get("Sprite") or el.get("SpriteName")
        for layers in el:
            if layers.tag == "Layers":
                for ly in layers:
                    if ly.tag == "BrushLayer" and (ly.get("Sprite") or ly.get("SpriteName")):
                        has_sprite = True
                        sprite_name = sprite_name or ly.get("Sprite") or ly.get("SpriteName")
                        if sprite_name == (ly.get("Sprite") or ly.get("SpriteName")):
                            nine = _nine_of(ly)
                    cf = ly.get("ColorFactor")
                    if cf is not None:
                        has_sprite = True
        for styles in el:
            if styles.tag != "Styles":
                continue
            for st in styles:
                if st.get("Name") in (None, "Default"):
                    fc = st.get("FontColor")
                    if fc:
                        color = _rgba_to_hex(fc)
                    for ly in st:
                        if ly.tag != "BrushLayer":
                            continue
                        if ly.get("Sprite") or ly.get("SpriteName"):
                            has_sprite = True
                            sprite_name = sprite_name or ly.get("Sprite") or ly.get("SpriteName")
                    break
        if color:
            info["color"] = color
        elif has_sprite:
            info["color"] = "sprite"
        if sprite_name:
            info["sprite"] = sprite_name
            if nine:
                info["nine"] = nine

    def sprite_and_nine_of(self, brush_name):
        """Brush 的 Default 态 (sprite 名, 九宫格参数 dict|None)。沿 Extends 链。"""
        if not self.index or not brush_name:
            return None, None
        seen = set()
        cur = brush_name
        while cur and cur not in seen:
            seen.add(cur)
            b = self.index.get(cur)
            if not b:
                return None, None
            if b.get("sprite"):
                return b["sprite"], b.get("nine")
            cur = b.get("extends")
        return None, None

    def get(self, name):
        b = self.index.get(name)
        if b is None:
            return None
        # 只有 Extends 没自带 Font/Size 时，沿继承链补齐
        seen = set()
        cur = b
        while cur and (not cur.get("font") or not cur.get("size")) and cur.get("extends"):
            nxt = cur["extends"]
            if nxt in seen:
                break
            seen.add(nxt)
            p = self.index.get(nxt)
            if not p:
                break
            cur = {"font": cur.get("font") or p.get("font"),
                   "size": cur.get("size") or p.get("size"),
                   "extends": p.get("extends")}
            self.index[name] = cur
        return cur


# ---------------------------------------------------------------- ③ 语言映射

class LanguageMap(object):
    """NativeLanguages.xml -> 语言 -> {default 字体, From→To 映射}"""

    def __init__(self, native_gui_dir):
        self.lang = {}
        self.path = None
        if native_gui_dir:
            p = os.path.join(native_gui_dir, "Fonts", "NativeLanguages.xml")
            if os.path.isfile(p):
                self.path = p
                self._load(p)

    def _load(self, path):
        try:
            tree = ET.parse(path)
        except Exception:  # noqa: BLE001
            return
        for el in tree.getroot():
            if el.tag != "Language":
                continue
            lid = el.get("id")
            if not lid:
                continue
            self.lang[lid] = {
                "default": el.get("DefaultFont"),
                "map": {m.get("From"): m.get("To") for m in el if m.tag == "Map"},
            }

    def resolve(self, font_name, language):
        """把 Prefab/Brush 里写的字体名，按当前语言映射成实际加载的字体。"""
        info = self.lang.get(language)
        if not info:
            return font_name
        return info["map"].get(font_name, font_name or info.get("default"))


# ---------------------------------------------------------------- ④ 文本内容

def _nine_of(ly):
    """BrushLayer 的九宫格参数（ExtendLeft/Top/Right/Bottom，sprite 像素）。

    游戏渲染语义：四角原样、四边单向拉伸、中心双向拉伸。
    没写 Extend* 的层不是九宫格，返回 None（整体拉伸即可）。
    """
    vals = {}
    for k in ("Left", "Top", "Right", "Bottom"):
        v = ly.get("Extend" + k)
        if v is None:
            return None
        try:
            vals[k.lower()] = float(v)
        except ValueError:
            return None
    return vals


def _rgba_to_hex(v):
    """"#RRGGBBAA" -> ("#RRGGBB", alpha 0..1)。非法输入返回 None。"""
    if not v:
        return None
    v = v.strip()
    if not v.startswith("#"):
        return None
    h = v[1:]
    if len(h) == 8:
        try:
            a = int(h[6:8], 16) / 255.0
            return "#" + h[0:6].upper(), round(a, 3)
        except ValueError:
            return None
    if len(h) == 6:
        return "#" + h[0:6].upper(), 1.0
    return None


_RE_RESOLVE = re.compile(
    r'Resolve\(\s*"(?P<id>[^"]+)"\s*,\s*"(?P<fb>(?:[^"\\]|\\.)*)"')
_RE_STR_ALL = re.compile(r'"((?:[^"\\]|\\.)*)"')


def _split_args(inner):
    """按顶层逗号切分 Resolve(...) 的实参，跳过 () [] {} <> 里的逗号。

    为什么需要：`new Dictionary<string, string>` 的逗号在 <> 里，
    三元表达式里的逗号也可能出现在嵌套调用里。
    """
    parts, buf = [], []
    depth = 0
    for ch in inner:
        if ch in "([{<":
            depth += 1
        elif ch in ")]}>":
            depth -= 1
        if ch == "," and depth <= 0:
            parts.append("".join(buf))
            buf = []
        else:
            buf.append(ch)
    parts.append("".join(buf))
    return [p.strip() for p in parts]


def _resolve_args(expr):
    """从一段含 Resolve( 的表达式里取 (id, fallback)。

    覆盖两种非字面量写法：
      Resolve(cond ? "idA" : "idB", cond ? "回退A" : "回退B")
      Resolve("id", "回退", new Dictionary<...>{...})
    取法：第一个实参里的首个字面量作 id，第二个实参里的首个字面量作回退。
    这只是启发式——真值仍以语言文件里的 id 为准。
    """
    i = expr.find("Resolve(")
    if i < 0:
        return None
    inner = expr[i + len("Resolve("):]
    args = _split_args(inner)
    if not args:
        return None
    m = _RE_STR_ALL.search(args[0])
    if not m:
        return None
    sid = _unescape(m.group(1))
    fb = ""
    if len(args) > 1:
        m2 = _RE_STR_ALL.search(args[1])
        if m2:
            fb = _unescape(m2.group(1))
    return sid, fb
_RE_PROP_ARROW = re.compile(
    r'public\s+string\s+(?P<prop>[A-Za-z_]\w*)\s*=>\s*(?P<expr>[^;]+);')
_RE_PROP_ASSIGN = re.compile(
    r'(?:^|[^\w.])(?P<prop>[A-Za-z_]\w*)\s*=\s*(?P<expr>[^;]+);')
_RE_STR_LIT = re.compile(r'^"(?P<lit>(?:[^"\\]|\\.)*)"$')
_RE_CLASS = re.compile(r'\b(?:class|struct)\s+(?P<cls>[A-Za-z_]\w*)')
_RE_LOADMOVIE = re.compile(r'LoadMovie\(\s*"(?P<movie>[A-Za-z_]\w*)"')
_RE_NEW_VM = re.compile(r'new\s+(?P<cls>[A-Za-z_]\w*VM)\s*\(')


def _unescape(s):
    return s.replace('\\"', '"').replace("\\\\", "\\").replace("\\n", "\n")


class TextResolver(object):
    """@属性名 -> 真实显示文本。

    两个来源，按优先级：
      1. ModuleData/Languages/**/*.xml 里 id 对应的当前语言文本
      2. src/*.cs 里 AwakeLocalization.Resolve("id", "回退字面量") 的回退字面量
    """

    def __init__(self, src_dir, lang_dirs, language):
        self.language = language
        self.props = {}        # 属性名 -> {"id":..., "fallback":...} 或 {"lit":...}（全局，先到先得）
        self.prop_defs = {}    # 属性名 -> {(类名, 来源文件, id 或字面量 或 None)}
        self.vm_of_movie = {}  # 面板名 -> 它的 ViewModel 类名（由 LoadMovie + new XVm 定）
        self.strings = {}      # id -> text（当前语言）
        self.notes = []
        self._load_src(src_dir)
        self._load_langs(lang_dirs)

    # ---- C# ViewModel 属性
    def _load_src(self, src_dir):
        if not src_dir or not os.path.isdir(src_dir):
            self.notes.append("未找到 src 目录，@ 绑定无法解析")
            return
        for dirpath, _dirs, files in os.walk(src_dir):
            for fn in files:
                if not fn.endswith(".cs"):
                    continue
                try:
                    text = io.open(os.path.join(dirpath, fn), encoding="utf-8",
                                   errors="replace").read()
                except Exception:  # noqa: BLE001
                    continue
                self._scan_cs(text, fn)
                self._scan_overlay(text)

    def _scan_overlay(self, text):
        """面板名 -> ViewModel 类：一个 Overlay 文件里 `LoadMovie("X", ds)` 与
        `new YVM(...)` 成对出现。这是**能把属性表按面板切开**的唯一依据 ——
        没有它，同名属性（TitleText / SendButtonText / NoticeText…）只能猜。
        """
        movies = _RE_LOADMOVIE.findall(text)
        vms = _RE_NEW_VM.findall(text)
        if len(movies) == 1 and len(vms) >= 1:
            self.vm_of_movie.setdefault(movies[0], vms[0])

    def _scan_cs(self, text, origin=""):
        # 按类切段：属性必须归属到它所在的类。同一个属性名在不同类里可能是
        # 完全不同的东西（运行时数据 vs 写死标题），不切开就判断不了。
        marks = [(m.start(), m.group("cls")) for m in _RE_CLASS.finditer(text)]
        if not marks:
            self._scan_seg(text, "", origin)
            return
        for i, (pos, cls) in enumerate(marks):
            end = marks[i + 1][0] if i + 1 < len(marks) else len(text)
            self._scan_seg(text[pos:end], cls, origin)

    def _scan_seg(self, seg, cls, origin):
        for m in _RE_PROP_ARROW.finditer(seg):
            self._record(cls, m.group("prop"), m.group("expr"), origin)
        for m in _RE_PROP_ASSIGN.finditer(seg):
            self._record(cls, m.group("prop"), m.group("expr"), origin, weak=True)

    def _record(self, cls, prop, expr, origin="", weak=False):
        if not prop:
            return
        defs = self.prop_defs.setdefault(prop, set())
        r = _RE_RESOLVE.search(expr)
        if r:
            sid = r.group("id")
            defs.add((cls, origin, sid))
            self.props.setdefault(prop, {"id": sid, "fallback": _unescape(r.group("fb"))})
            return
        if "Resolve(" in expr:
            got = _resolve_args(expr)
            if got:
                defs.add((cls, origin, got[0]))
                self.props.setdefault(prop, {"id": got[0], "fallback": got[1]})
                return
        lit = _RE_STR_LIT.match(expr.strip())
        if lit and not weak:
            v = _unescape(lit.group("lit"))
            defs.add((cls, origin, v))
            self.props.setdefault(prop, {"lit": v})
            return
        # 没有 Resolve / 字面量 —— 说明这个属性是**运行时数据**
        #（如 `=> _contact.DisplayName`、`StatusText = evt.Text`）。
        # 也要记一笔，否则"重名"判据看不见它。
        defs.add((cls, origin, None))

    def is_static(self, prop, movie=None):
        """该属性名能不能**按名**安全解析出静态文本。

        判据按优先级：
          1. 知道这个面板的 ViewModel 类（movie -> VM）时，只看**那个类里**的定义：
             有 Resolve/字面量 ⇒ 可解；只有运行时实现或同名多份 ⇒ 不可解。
          2. 不知道时，退回全体：所有定义必须**解出同一串字**才算可信。

        为什么非要这么麻烦：游戏里 `@X` 的值来自运行时 DataSource，本无静态文本；
        这里能解出字，只是因为项目把属性名取成了本地化 id 的同名。这个巧合在重名时
        就崩了 —— 实测把对话面板的标题画成了「醒世 · 开发者检查」（那是别处的字符串）。
        """
        defs = self.prop_defs.get(prop)
        if not defs:
            return False
        cls = self.vm_of_movie.get(movie) if movie else None
        if cls:
            mine = {(c, o, v) for (c, o, v) in defs if c == cls}
            if mine:
                vals = {v for (_c, _o, v) in mine}
                return len(vals) == 1 and None not in vals
        vals = {v for (_c, _o, v) in defs}
        return len(vals) == 1 and None not in vals

    # ---- 语言文件
    def _load_langs(self, lang_dirs):
        found = []
        for d in (lang_dirs or []):
            if not os.path.isdir(d):
                continue
            for dirpath, _dirs, files in os.walk(d):
                for fn in files:
                    if not fn.lower().endswith(".xml"):
                        continue
                    p = os.path.join(dirpath, fn)
                    found.append(p)
        if not found:
            self.notes.append("未找到语言文件，文本将退回 C# 回退字面量")
            return
        # 当前语言优先：文件名带 zh-HANS / 简体的先读，后读的覆盖不了先读的
        ordered = sorted(found, key=self._lang_rank)
        for p in ordered:
            try:
                tree = ET.parse(p)
            except Exception:  # noqa: BLE001
                continue
            for el in tree.getroot().iter("string"):
                sid = el.get("id")
                txt = el.get("text")
                # 先到先得：ordered 已按「当前语言优先」排过序，后读的不能覆盖
                if sid and txt is not None and sid not in self.strings:
                    self.strings[sid] = txt

    def _lang_rank(self, path):
        low = path.lower().replace("\\", "/")
        if self.language == "简体中文":
            return 0 if ("zh-hans" in low or "/cns/" in low) else 1
        if self.language == "English":
            return 0 if "/cns/" not in low and "zh-hans" not in low else 1
        return 1

    # ---- 对外
    def looks_bound(self, raw):
        return bool(raw) and raw.startswith("@")

    def resolve(self, raw):
        """把 Text 属性值变成要显示/度量的文本。解析不了就原样返回。"""
        if not self.looks_bound(raw):
            return raw
        prop = raw[1:]
        info = self.props.get(prop)
        if not info:
            return ""
        if "lit" in info:
            return info["lit"]
        sid = info.get("id")
        if sid and sid in self.strings:
            return self.strings[sid]
        return info.get("fallback", "")

    def resolve_strict(self, raw, movie=None):
        """只解析**可信**的绑定。

        游戏里 `@X` 的值来自运行时 DataSource，本来就没有静态文本；这里之所以能
        解出字来，是因为项目把属性名取成了本地化 id 的同名。这个巧合在**重名**
        （同属性名被多个类定义）时就不成立了 ⇒ 那种一律不解，返回 ""。

        `movie` = 面板名（如 "NpcDialogue"）：给了就能只看**这个面板自己的
        ViewModel**，同名属性不会再串到别的面板上。

        给**真渲染**用：渲染图越像真的，串号越危险（没人会怀疑它）。
        """
        if not self.looks_bound(raw):
            return raw
        if not self.is_static(raw[1:], movie):
            return ""
        return self.resolve(raw)

    def coverage(self, names):
        hit = [n for n in names if n in self.props]
        return len(hit), len(names), [n for n in names if n not in self.props]


# ---------------------------------------------------------------- 门面

class Metrics(object):
    def __init__(self, fonts, brushes, langs, texts, language):
        self.fonts = fonts
        self.brushes = brushes
        self.langs = langs
        self.texts = texts
        self.language = language
        self.notes = []

    # -- 文本
    def resolve_text(self, raw):
        return self.texts.resolve(raw)

    def resolve_text_strict(self, raw, movie=None):
        """只解可信绑定（给真渲染用）。见 TextResolver.resolve_strict。"""
        return self.texts.resolve_strict(raw, movie)

    def is_static_binding(self, prop, movie=None):
        return self.texts.is_static(prop, movie)

    # -- 字体与字号：显式 Brush.FontSize > Brush 定义 FontSize > 默认
    def font_and_size(self, attrs, default=16.0):
        brush = attrs.get("Brush")
        font = None
        size = None
        if brush and self.brushes:
            b = self.brushes.get(brush)
            if b:
                font = b.get("font")
                size = b.get("size")
        explicit = attrs.get("Brush.FontSize")
        if explicit:
            try:
                size = float(explicit)
            except ValueError:
                pass
        own = attrs.get("Font")
        if own:
            font = own
        if self.langs:
            font = self.langs.resolve(font, self.language)
        return font, (size if size else default)

    # -- 度量
    def measure(self, text, font, size, max_w=0.0):
        """返回 (宽, 高)。字体查不到时返回 None，让调用方退回启发式。"""
        if not text:
            return None
        fm = self.fonts.get(font) if self.fonts else None
        if fm is None or fm.native <= 0:
            return None
        scale = fm.scale_for(size)
        natural = sum(fm.advance_of(ch) for ch in text) * scale
        line_h = (fm.line_height or fm.size) * scale
        if max_w and max_w > 0 and natural > max_w:
            lines = int(math.ceil(natural / max_w))
            return float(max_w), lines * line_h
        return natural, line_h

    def note(self, msg):
        self.notes.append(msg)

    def line_height(self, font, size):
        """真实单行行高（文本内容不可知时的最小可用值）。字体不可用返回 None。"""
        fm = self.fonts.get(font) if self.fonts else None
        if fm is None or fm.native <= 0:
            return None
        return (fm.line_height or fm.size) * fm.scale_for(size)

    def color_of(self, brush_name):
        """Brush 的 Default 态颜色。

        返回 (hex, alpha) 或 "sprite"（Sprite 类，无真色可信）或 None（未命中）。
        沿 Extends 继承链补齐——子 Brush 没写颜色时向父找。
        """
        if not self.brushes or not brush_name:
            return None
        seen = set()
        cur = brush_name
        while cur and cur not in seen:
            seen.add(cur)
            b = self.brushes.index.get(cur)
            if not b:
                return None
            c = b.get("color")
            if c is not None:
                return c
            cur = b.get("extends")
        return None

    def sprite_of(self, brush_name):
        """Brush 的 Default 态 Sprite 名（贴图层用）。沿 Extends 链；未命中返回 None。"""
        return self.sprite_and_nine_of(brush_name)[0]

    def sprite_and_nine_of(self, brush_name):
        """Brush 的 Default 态 (sprite 名, 九宫格参数)。委托给 BrushIndex。"""
        if not self.brushes:
            return None, None
        return self.brushes.sprite_and_nine_of(brush_name)


def _parent_modules_dir(path):
    """Prefabs 目录 -> 上溯到 Modules。"""
    p = os.path.normpath(path or "")
    while p and os.path.basename(p).lower() not in ("modules", ""):
        parent = os.path.dirname(p)
        if parent == p:
            break
        p = parent
    return p if os.path.basename(p).lower() == "modules" else None


def _game_dir_from_native(native_dir):
    """Modules 目录 -> 游戏根（Modules 的父级）。"""
    if not native_dir:
        return None
    p = os.path.normpath(native_dir)
    if os.path.basename(p).lower() != "modules":
        p = _parent_modules_dir(p) or p
    if os.path.basename(p).lower() != "modules":
        return None
    return os.path.dirname(p)


def build(prefab_dir=None, native_dir=None, awake_root=None,
          game_dir=None, language=DEFAULT_LANGUAGE, verbose=False):
    """组装度量层。任何一段缺失都不报错——对应能力自动降级。"""
    notes = []

    modules = _parent_modules_dir(native_dir) or (native_dir if native_dir
                                                  and os.path.basename(
                                                      os.path.normpath(native_dir)).lower() == "modules"
                                                  else None)
    if game_dir is None:
        game_dir = _game_dir_from_native(modules)
    if not game_dir:
        notes.append("未定位游戏根目录：字体指标不可用（退回估算）")

    # ① 字体
    fonts = None
    fonts_dir = os.path.join(game_dir, "GUI", "GauntletUI", "Fonts") if game_dir else None
    if fonts_dir and os.path.isdir(fonts_dir):
        fonts = FontLibrary(fonts_dir)
    else:
        notes.append("未找到 GUI/GauntletUI/Fonts：字体指标不可用")

    # ③ 语言映射
    native_gui = os.path.join(modules, "Native", "GUI") if modules else None
    langs = LanguageMap(native_gui) if native_gui and os.path.isdir(native_gui) else None
    if langs is None:
        notes.append("未找到 NativeLanguages.xml：字体名不映射")

    # ② Brush 定义：模组自有 Brushes 排最前（模组覆盖原版），再官方模块
    #    Native→SandBox→SandBoxCore→StoryMode→Multiplayer
    brush_roots = []
    if awake_root:
        d = os.path.join(awake_root, "GUI", "Brushes")
        if os.path.isdir(d):
            brush_roots.append(d)
    if modules:
        for m in ("Native", "SandBox", "SandBoxCore", "StoryMode", "Multiplayer"):
            d = os.path.join(modules, m, "GUI", "Brushes")
            if os.path.isdir(d):
                brush_roots.append(d)
    brushes = BrushIndex(brush_roots) if brush_roots else None
    if not brush_roots:
        notes.append("未找到官方 Brushes 目录：Brush 字号/字体不可用")

    # ④ 文本：src/*.cs + ModuleData/Languages/**
    lang_dirs = []
    if awake_root:
        lang_dirs = [os.path.join(awake_root, *d) for d in LANG_DIRS]
    texts = TextResolver(os.path.join(awake_root, "src") if awake_root else None,
                         lang_dirs, language)

    m = Metrics(fonts, brushes, langs, texts, language)
    m.notes.extend(notes)
    m.notes.extend(fonts.notes if fonts else [])
    m.notes.extend(texts.notes)
    if verbose:
        for n in m.notes:
            print("[metrics] " + n)
    return m
