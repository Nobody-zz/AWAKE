#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
AWAKE UI Lab —— 原版 Sprite 贴图层（可选增强，C 线质感升级）

把线框图里的 Sprite 类控件从「无真色可信」升级为**游戏原版贴图**：

  sprite 索引   <Modules>/<Mod>/GUI/*SpriteData.xml
                （sprite 名 -> category / sheetID / SheetX / SheetY / Width / Height）
  图集本体      <Modules>/Native/AssetPackages/gauntlet_ui.tpac 等（TPAC v2，DXT1/DXT5/BC7）
                由外部工具 schema-indexer --extract-sheet 解码为 PNG（kerema14/GauntletUI-LSP
                的 TpacTool fork，见 out/GauntletUI-LSP，gitignore 内可重建）
  单 sprite     从图集 PNG 按矩形裁剪，存 out/atlas/sprites/<名>.png 供 SVG <image> 引用

降级原则（与 ui_text_metrics 一致）：索引/图集/工具任一缺失 → 自动退回分类色，不报错。
"""
import glob
import os
import subprocess
import xml.etree.ElementTree as ET


def _default_paths():
    game = None
    for cand in (r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord",):
        if os.path.isdir(cand):
            game = cand
            break
    lab = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    atlas_dir = os.path.join(lab, "out", "atlas")
    indexer = os.path.join(lab, "out", "GauntletUI-LSP", "schema-indexer",
                           "bin", "Debug", "net8.0", "schema-indexer.exe")
    return game, atlas_dir, indexer


class SpriteAtlas(object):
    def __init__(self, game=None, atlas_dir=None, indexer=None,
                 modules=("Native", "SandBox", "SandBoxCore", "StoryMode", "Multiplayer")):
        if game is None or atlas_dir is None or indexer is None:
            dg, da, di = _default_paths()
            game = game or dg
            atlas_dir = atlas_dir or da
            indexer = indexer or di
        self.game = game
        self.atlas_dir = atlas_dir
        self.indexer = indexer if os.path.isfile(indexer) else None
        self.sprites_dir = os.path.join(atlas_dir, "sprites")
        self.index = {}
        # 九宫格延展值：{sprite 名: {"left","right","top","bottom"}}
        # 来源＝SpriteData 的 <NineRegionSprite>（直接写 Sprite="xxx_9" 时用得上）
        self.nine = {}
        self.notes = []
        self._available = None
        # 模组自绘贴图的**源文件**目录（一图一 sprite：GUI/SpriteParts/<分类>/<名>.png）。
        # 这条不依赖图集、不依赖 SpriteData、也不依赖 Modding Kit ⇒ 美术出了图就能本地预览。
        # 顺序：仓库内 AWAKE → 游戏内已安装的 AWAKE → 游戏内其它模块（兜底）。
        self.sprite_parts_roots = []
        _lab = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
        _repo_awake = os.path.dirname(os.path.dirname(_lab))
        _cand = [os.path.join(_repo_awake, "GUI", "SpriteParts")]
        if game:
            _cand.append(os.path.join(game, "Modules", "AWAKE", "GUI", "SpriteParts"))
            _cand.extend(sorted(glob.glob(os.path.join(game, "Modules", "*",
                                                      "GUI", "SpriteParts"))))
        for _d in _cand:
            if _d and os.path.isdir(_d) and _d not in self.sprite_parts_roots:
                self.sprite_parts_roots.append(_d)
        if game and os.path.isdir(os.path.join(game, "Modules")):
            self._load_index(modules)
        else:
            self.notes.append("未定位游戏目录：sprite 贴图不可用")

    # ---- 索引
    def _load_index(self, modules):
        n0 = len(self.index)
        alias = {}  # 别名 / 九宫格名 -> 底层 SpritePart 名
        for m in modules:
            for p in glob.glob(os.path.join(self.game, "Modules", m, "GUI", "*SpriteData.xml")):
                try:
                    root = ET.parse(p).getroot()
                except Exception:  # noqa: BLE001
                    continue
                for part in root.iter("SpritePart"):
                    nm = part.get("Name") or part.findtext("Name")
                    if not nm:
                        continue
                    self.index[nm] = (part.findtext("CategoryName"),
                                      int(part.findtext("SheetID") or 1),
                                      int(part.findtext("SheetX") or 0),
                                      int(part.findtext("SheetY") or 0),
                                      int(part.findtext("Width") or 0),
                                      int(part.findtext("Height") or 0))
                # 九宫格声明：Name 如 npc_dialogue_panel_9 -> SpritePartName 指向原始裁切
                for ns in root.iter("NineRegionSprite"):
                    nm = ns.get("Name") or ns.findtext("Name")
                    base = ns.get("SpritePartName") or ns.findtext("SpritePartName")
                    if not nm or not base:
                        continue
                    alias[nm] = base
                    self.nine[nm] = {
                        "left": float(ns.findtext("LeftWidth") or 0),
                        "right": float(ns.findtext("RightWidth") or 0),
                        "top": float(ns.findtext("TopHeight") or 0),
                        "bottom": float(ns.findtext("BottomHeight") or 0),
                    }
                # 通用别名
                for gs in root.iter("GenericSprite"):
                    nm = gs.get("Name") or gs.findtext("Name")
                    base = gs.get("SpritePartName") or gs.findtext("SpritePartName")
                    if nm and base:
                        alias.setdefault(nm, base)
        # 别名 -> 复制底层裁切信息（别名可能指向别名，迭代到稳定）
        for _ in range(4):
            pending = {k: v for k, v in alias.items() if k not in self.index}
            if not pending:
                break
            for nm, base in pending.items():
                info = self.index.get(base)
                if info is not None:
                    self.index[nm] = info
        if len(self.index) == n0:
            self.notes.append("SpriteData.xml 未命中：sprite 索引为空")

    def nine_of(self, name):
        """直接以 Sprite="xxx_9" 引用时，返回九宫格延展值；非九宫格返回 None。"""
        return self.nine.get(name)

    # ---- 合成缓存键
    # 缓存必须认「源图变没变」：只按「文件名+尺寸」做键时，美术把新图丢进 custom/
    # 后重跑，会命中旧合成图而**静默不变**（09-13 实测：面板底换了但预览还是原版）。
    def _is_custom(self, p):
        try:
            return os.path.dirname(os.path.abspath(p)) == os.path.abspath(
                os.path.join(self.atlas_dir, "custom"))
        except Exception:  # noqa: BLE001
            return False

    @staticmethod
    def _stamp(p):
        try:
            st = os.stat(p)
            return "%d_%d" % (int(st.st_mtime), st.st_size)
        except OSError:
            return "0_0"

    def _cache_hit(self, out, base_png):
        """custom/ 里的图每次现算（美术在改它）；其余按 mtime+size 认新鲜度。"""
        return (not self._is_custom(base_png)) and os.path.isfile(out)

    @property
    def available(self):
        return bool(self.index) and self.indexer is not None

    # ---- 图集
    def _sheet_path(self, cat, sid):
        return os.path.join(self.atlas_dir, "%s_%d.png" % (cat, sid))

    def _ensure_sheet(self, cat, sid):
        out = self._sheet_path(cat, sid)
        if os.path.isfile(out):
            return out
        if self.indexer is None:
            return None
        os.makedirs(self.atlas_dir, exist_ok=True)
        try:
            subprocess.run(
                [self.indexer,
                 "--game-bin-path", os.path.join(self.game, "bin", "Win64_Shipping_Client"),
                 "--resource-path", os.path.join(self.game, "Modules"),
                 "--extract-sheet", "%s_%d" % (cat, sid), "--output", out],
                capture_output=True, text=True, encoding="utf-8", errors="replace",
                timeout=300)
        except Exception as ex:  # noqa: BLE001
            self.notes.append("抽图集失败 %s_%d：%s" % (cat, sid, ex))
            return None
        return out if os.path.isfile(out) else None

    # ---- 单 sprite
    # 程序化 sprite（不在任何 SpriteData 里）：游戏运行时由引擎生成，这里给等价替身。
    _PROCEDURAL = {
        # 纯白块：游戏里靠 Color/AlphaFactor 染色，贴图本身就是全白
        "BlankWhite": (16, 16, (255, 255, 255, 255)),
        "BlankWhiteSquare_9": (16, 16, (255, 255, 255, 255)),
    }

    def sprite_png(self, name):
        """sprite 名 -> 本地 PNG 路径；拿不到返回 None。"""
        if not name:
            return None
        proc = self._PROCEDURAL.get(name)
        if proc is not None:
            w, h, rgba = proc
            out = os.path.join(self.sprites_dir,
                               name.replace("\\", "__").replace("/", "__") + ".png")
            if not os.path.isfile(out):
                try:
                    from PIL import Image
                    os.makedirs(self.sprites_dir, exist_ok=True)
                    Image.new("RGBA", (w, h), rgba).save(out)
                except Exception:  # noqa: BLE001
                    return None
            return out
        # 自定义美术覆盖：<atlas>/custom/<sprite 名>.png 优先于游戏原版。
        # 美术会话产出新贴图后，按 Prefab 里写的 sprite 名丢进这个目录即可预览，无需重编索引。
        custom = os.path.join(self.atlas_dir, "custom",
                              name.replace("\\", "__").replace("/", "__") + ".png")
        if os.path.isfile(custom):
            return custom
        # 模组自绘贴图源文件：GUI/SpriteParts/<分类>/<名>.png（子目录会成为 sprite 名的一部分）。
        # 放在索引之前：源图是作者手放的真相，且它不需要图集/索引器/Modding Kit 就已可渲染。
        for root in self.sprite_parts_roots:
            pat = os.path.join(root, "**", *name.replace("\\", "/").split("/")) + ".png"
            hits = glob.glob(pat, recursive=True)
            if hits:
                return hits[0]
        # 已裁切缓存：不依赖索引器（独立分发/离线场景只要带上 out/atlas/sprites 就能渲染）
        out = os.path.join(self.sprites_dir,
                           name.replace("\\", "__").replace("/", "__") + ".png")
        if os.path.isfile(out):
            return out
        # 不再在此处以 `available` 提前退出：索引器缺失但图集已在磁盘时仍应按需裁切。
        info = self.index.get(name)
        if info is None:
            return None
        cat, sid, x, y, w, h = info
        if w <= 0 or h <= 0:
            return None
        sheet = self._ensure_sheet(cat, sid)
        if not sheet:
            return None
        try:
            from PIL import Image
            os.makedirs(self.sprites_dir, exist_ok=True)
            Image.open(sheet).crop((x, y, x + w, y + h)).save(out)
        except Exception as ex:  # noqa: BLE001
            self.notes.append("裁剪失败 %s：%s" % (name, ex))
            return None
        return out

    def info(self, name):
        return self.index.get(name)

    def tint_png(self, base_png, rgba):
        """按 Gauntlet 语义给 sprite 染色：Color 是**逐通道相乘**（#RRGGBBAA）。

        预览原先只对矩形上色、sprite 直接铺图 ⇒ 带 Color 的贴图（如深色面板底）
        会被渲染成原色，严重失真。返回染色后的缓存路径。
        """
        if not base_png or not rgba:
            return base_png
        s = str(rgba).lstrip("#")
        if len(s) != 8:
            return base_png
        try:
            tr, tg, tb, ta = (int(s[i:i + 2], 16) for i in (0, 2, 4, 6))
        except ValueError:
            return base_png
        if (tr, tg, tb, ta) == (255, 255, 255, 255):
            return base_png
        key = "%s__t%02x%02x%02x%02x_%s.png" % (
            os.path.splitext(os.path.basename(base_png))[0], tr, tg, tb, ta,
            self._stamp(base_png))
        out = os.path.join(self.sprites_dir, "__tint_" + key)
        if self._cache_hit(out, base_png):
            return out
        try:
            from PIL import Image
            im = Image.open(base_png).convert("RGBA")
            r, g, b, al = im.split()
            r = r.point(lambda v: int(v * tr / 255))
            g = g.point(lambda v: int(v * tg / 255))
            b = b.point(lambda v: int(v * tb / 255))
            al = al.point(lambda v: int(v * ta / 255))
            os.makedirs(self.sprites_dir, exist_ok=True)
            Image.merge("RGBA", (r, g, b, al)).save(out)
            return out
        except Exception as ex:  # noqa: BLE001
            self.notes.append("染色失败 %s：%s" % (key, ex))
            return base_png

    # ---- 九宫格预合成
    def nine_png(self, base_png, nine, dest_w, dest_h):
        """把九宫格 sprite 合成**目标尺寸**的单张 PNG，返回路径；失败返回 None。

        游戏渲染语义：四角原样、四边单向拉伸、中心双向拉伸（ExtendLeft/Top/...）。
        预合成比 SVG 嵌套切片稳：无头截图 / 浏览器悬停都走同一条普通 <image> 路径。
        """
        if not base_png or not nine:
            return None
        L = max(0, int(round(nine["left"])))
        t = max(0, int(round(nine["top"])))
        r = max(0, int(round(nine["right"])))
        b = max(0, int(round(nine["bottom"])))
        W = max(1, int(round(dest_w)))
        H = max(1, int(round(dest_h)))
        if W < L + r or H < t + b:
            return None
        key = "%s_%dx%d_%s.png" % (os.path.splitext(os.path.basename(base_png))[0],
                                   W, H, self._stamp(base_png))
        out = os.path.join(self.sprites_dir, "__nine_" + key)
        if self._cache_hit(out, base_png):
            return out
        try:
            from PIL import Image
            im = Image.open(base_png).convert("RGBA")
            sw, sh = im.size
            if sw <= L + r or sh <= t + b:
                return None
            canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
            mid_w, mid_h = W - L - r, H - t - b
            src_mid_w, src_mid_h = sw - L - r, sh - t - b

            def piece(sx, sy, spw, sph, dx, dy, dpw, dph):
                if spw <= 0 or sph <= 0 or dpw <= 0 or dph <= 0:
                    return
                part = im.crop((int(sx), int(sy), int(sx + spw), int(sy + sph)))
                if (int(dpw), int(dph)) != part.size:
                    part = part.resize((int(dpw), int(dph)), Image.NEAREST)
                canvas.alpha_composite(part, (int(dx), int(dy)))

            xs = [(0, L, 0, L), (L, mid_w, L, src_mid_w), (W - r, r, sw - r, r)]
            ys = [(0, t, 0, t), (t, mid_h, t, src_mid_h), (H - b, b, sh - b, b)]
            # 元组顺序：(目标x, 目标宽, 源x, 源宽) / (目标y, 目标高, 源y, 源高)
            for (dx, dpx, sxx, spw) in xs:
                for (dy, dpy, syy, sph) in ys:
                    piece(sxx, syy, spw, sph, dx, dy, dpx, dpy)
            os.makedirs(self.sprites_dir, exist_ok=True)
            canvas.save(out)
            return out
        except Exception as ex:  # noqa: BLE001
            self.notes.append("九宫格合成失败 %s：%s" % (key, ex))
            return None
