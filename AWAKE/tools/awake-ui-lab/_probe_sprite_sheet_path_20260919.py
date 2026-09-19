"""_probe_sprite_sheet_path_20260919.py —— 查引擎到底怎么拼「图集文件路径」。

背景：Leverage 把图集从 AssetSources 摆到了 GUI/SpriteSheets/ui_leverage/，
但 17:21 那次启动（17:14 才摆好）日志里**仍然**报 `Cannot find texture: ui_leverage_1`。
⇒ 要么路径规则不是大家以为的那条，要么还缺一步注册。

做法：直接在出货 DLL 的字符串堆里找
  ① 「Cannot find texture」这条报错在哪个程序集里（定位责任者）
  ② 拼路径用的格式串 / 目录名常量（SpriteSheets、.png、_%d 之类）

只读游戏目录，不改任何东西。
"""
import os
import re
import sys

GAME = os.environ.get(
    "AWAKE_GAME",
    r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord")

NEEDLES = [
    "Cannot find texture",
    "SpriteSheets",
    "SpriteSheet",
    "Cannot find sprite",
    "SpriteCategory",
    "AtlasEntity",
    "TextureProvider",
]


def dlls():
    """只挑图形/资源相关的程序集。

    别walk整个 bin —— 那底下有几百个 DLL（含 PhysXGpu 29MB、EOSSDK 18MB），
    逐个读进来会被拖死（第一次跑就是 SIGTERM 收场）。
    """
    bin_dir = os.path.join(GAME, "bin", "Win64_Shipping_Client")
    mod_bin = os.path.join(GAME, "Modules", "Native", "bin", "Win64_Shipping_Client")
    want = (
        "TaleWorlds.TwoDimension.dll",
        "TaleWorlds.TwoDimension.Standalone.dll",
        "TaleWorlds.GauntletUI.dll",
        "TaleWorlds.GauntletUI.PrefabSystem.dll",
        "TaleWorlds.GauntletUI.Data.dll",
        "TaleWorlds.Engine.GauntletUI.dll",
        "TaleWorlds.Engine.dll",
        "TaleWorlds.Library.dll",
        "TaleWorlds.Core.dll",
    )
    out = []
    for d in (bin_dir, mod_bin):
        for n in want:
            p = os.path.join(d, n)
            if os.path.isfile(p):
                out.append(p)
    return out


def hits_in(path, needles):
    """在 DLL 里找 ASCII 与 UTF-16LE 两种编码的字符串。"""
    try:
        b = open(path, "rb").read()
    except Exception:  # noqa: BLE001
        return []
    found = []
    for n in needles:
        a = n.encode("ascii")
        u = n.encode("utf-16-le")
        if a in b or u in b:
            found.append(n)
    return found


def context_utf16(b, n, span=90):
    """把 UTF-16 字符串附近的内容挖出来看（.NET 的字符串常量常连着存放）。"""
    u = n.encode("utf-16-le")
    i = b.find(u)
    if i < 0:
        return []
    chunks = []
    lo, hi = max(0, i - span * 2), min(len(b), i + span * 2)
    seg = b[lo:hi]
    # 粗抽 UTF-16 可打印段
    for m in re.finditer(rb"(?:[\x20-\x7e]\x00){4,}", seg):
        chunks.append(m.group(0).decode("utf-16-le"))
    return chunks


def main():
    print("游戏目录：%s" % GAME)
    ds = dlls()
    print("扫 %d 个 DLL\n" % len(ds))
    hit_any = False
    for p in ds:
        got = hits_in(p, NEEDLES)
        if got:
            hit_any = True
            print("  %-34s %s" % (os.path.basename(p), ", ".join(got)))
    if not hit_any:
        print("  （一个都没命中 —— 先怀疑探针本身，别急着下结论）")

    print("\n=== 'Cannot find texture' 附近字符串 ===")
    for p in ds:
        try:
            b = open(p, "rb").read()
        except Exception:  # noqa: BLE001
            continue
        if b"Cannot find texture" not in b and "Cannot find texture".encode("utf-16-le") not in b:
            continue
        print("  在 %s：" % os.path.basename(p))
        for s in context_utf16(b, "Cannot find texture"):
            print("     %r" % s)
    print("  （一个 DLL 都没有 ⇒ 这条报错在**原生层**，不在托管代码里）")

    print("\n=== TaleWorlds.TwoDimension.dll 里 'SpriteSheet' 附近的字符串 ===")
    for p in ds:
        if os.path.basename(p) != "TaleWorlds.TwoDimension.dll":
            continue
        b = open(p, "rb").read()
        seen = set()
        for n in ("SpriteSheets", "_1", ".png"):
            for s in context_utf16(b, n, span=140):
                if s not in seen:
                    seen.add(s)
                    print("   [%s] %r" % (n, s))
    return 0


if __name__ == "__main__":
    sys.exit(main())
