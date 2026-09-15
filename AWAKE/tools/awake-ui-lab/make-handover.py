#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""把 awake-ui-lab 打成一个可独立分发的交接包（给 UI 资产生成会话）。

用法：
    python make-handover.py

产物：out/handover/awake-ui-preview-handover-<日期>.zip

包内容＝工具源码 + 样例 Prefab + 图集缓存 + 截图样例 + 图集原图（可离线跑）。
不打进去：索引器二进制（out/GauntletUI-LSP，可从仓库重建）、历史快照、临时脚本。
"""
import datetime
import os
import zipfile

LAB = os.path.dirname(os.path.abspath(__file__))

TOP_FILES = [
    'awake_ui.py', 'README.md', 'HANDOVER-UI-PREVIEW.md',
    'UI-ART-SPEC-20260913.md', 'import-manifest.v1.json', 'verify-import.ps1',
    'make-handover.py',
]
TOP_DIRS = ['preview', 'assets', 'fixtures', 'src', 'tests', 'samples']

OUT_INCLUDE_FILES = ['index.html', 'ui-report.v1.json']
OUT_INCLUDE_DIRS = [os.path.join('atlas', 'sprites'),
                    os.path.join('atlas', 'custom'),
                    'shot']

SKIP_DIRNAMES = {'__pycache__', 'bin', 'obj', '.git'}
SKIP_SUFFIXES = ('.pyc',)
# 相对 LAB 的路径前缀：陈旧/冗余产物，不进包。
# preview/out/ 是早期版本的输出目录（.gitignore 已排除），与根 out/ 重复且内容更旧，
# 带上会让接收方看到两个 out/ 而分不清哪个是当前产物。
SKIP_REL_PREFIXES = ('preview/out/',)

# out/atlas 下的原始大图集（*.png，不含子目录）：带上即可离线渲染原版 sprite
ATLAS_SHEETS = True

CUSTOM_README = """这是「自定义贴图覆盖目录」。

用法：把 Prefab 里写到的 sprite 名，按 名字里的 \\ 和 / 替换成 __ 之后，
存成 <sprite 名>.png 放进本目录，预览时就会**优先于游戏原版**使用你的贴图。

例：
  Prefab: Sprite="awake_panel_9"          -> 放 awake_panel_9.png
  Prefab: Sprite="Awake\\Panel\\main_9"    -> 放 Awake__Panel__main_9.png

注意：自定义的九宫格 sprite（名以 _9 结尾）目前拿不到 Extend 参数，预览会整体拉伸。
交付时请另附 Extend* 数值。
"""

PACKAGE_INFO = """AWAKE UI 预览程序 · 交接包
入口：awake_ui.py（子命令 check / diff）
先读：HANDOVER-UI-PREVIEW.md
设计规格：UI-ART-SPEC-20260913.md

快速开始：
  python awake_ui.py check                              # 全部 Prefab
  python awake_ui.py check --prefab AwakeMessenger.xml  # 只看一个
  python awake_ui.py diff                               # 与上次快照比较

预览自己的贴图：把 PNG 放进 out/atlas/custom/<sprite 名>.png
"""


def add_file(zf, path, arcname):
    if os.path.isfile(path):
        zf.write(path, arcname)
        return 1
    return 0


def walk_dir(zf, root, count):
    """按「相对 LAB 的路径」写入 zip，保持目录层级。"""
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRNAMES]
        for fn in filenames:
            if fn.endswith(SKIP_SUFFIXES):
                continue
            full = os.path.join(dirpath, fn)
            rel = os.path.relpath(full, LAB).replace(os.sep, '/')
            if rel.startswith(SKIP_REL_PREFIXES):
                continue
            zf.write(full, rel)
            count[0] += 1


def main():
    stamp = datetime.date.today().strftime('%Y%m%d')
    outdir = os.path.join(LAB, 'out', 'handover')
    os.makedirs(outdir, exist_ok=True)
    zip_path = os.path.join(outdir, 'awake-ui-preview-handover-%s.zip' % stamp)

    count = [0]
    with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as zf:
        for f in TOP_FILES:
            count[0] += add_file(zf, os.path.join(LAB, f), f)
        for d in TOP_DIRS:
            walk_dir(zf, os.path.join(LAB, d), count)
        for f in OUT_INCLUDE_FILES:
            count[0] += add_file(zf, os.path.join(LAB, 'out', f), 'out/' + f)
        for d in OUT_INCLUDE_DIRS:
            walk_dir(zf, os.path.join(LAB, 'out', d), count)
        if ATLAS_SHEETS:
            atlas = os.path.join(LAB, 'out', 'atlas')
            if os.path.isdir(atlas):
                for fn in os.listdir(atlas):
                    if fn.lower().endswith('.png'):
                        count[0] += add_file(zf, os.path.join(atlas, fn), 'out/atlas/' + fn)
        zf.writestr('out/atlas/custom/README.txt', CUSTOM_README)
        zf.writestr('PACKAGE-INFO.txt', PACKAGE_INFO)

    print('entries : %d' % count[0])
    print('zip     : %s' % zip_path)
    print('size    : %.1f MB' % (os.path.getsize(zip_path) / 1048576.0))
    return zip_path


if __name__ == '__main__':
    main()
