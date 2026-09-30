# -*- coding: utf-8 -*-
"""AWAKE 模组交付面盘点：仓库源 / dist staging / 游戏目录 三方对照。

只读，不写任何文件。输出模块清单 + 覆盖缺口。
"""
import os
import sys

sys.stdout.reconfigure(encoding='utf-8')

REPO = r"D:\AWAKE-Dev\AWAKE"
GAME = r"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AWAKE"
DIST = os.path.join(REPO, "dist", "Modules", "AWAKE")

# 仓库里不参与交付的开发/构建目录
DEV_DIRS = {
    'docs', '_build_out', 'obj', 'bin', 'dist', 'framework', 'src', 'tools',
    'artifacts', 'release', 'workspace', 'AssetSources', '.git', '.workbuddy-ai',
}


def collect(root, subdirs):
    """收集 root/subdir 下的全部文件，返回 {相对 root 的 posix 路径: size}。"""
    out = {}
    for sd in subdirs:
        base = os.path.join(root, sd)
        if not os.path.isdir(base):
            continue
        for dp, dn, fn in os.walk(base):
            dn[:] = [d for d in dn if d not in DEV_DIRS]
            for f in fn:
                p = os.path.join(dp, f)
                rel = os.path.relpath(p, root).replace('\\', '/')
                out[rel] = os.path.getsize(p)
    return out


def root_files(root):
    out = {}
    for f in os.listdir(root):
        p = os.path.join(root, f)
        if os.path.isfile(p):
            out[f] = os.path.getsize(p)
    return out


SUBDIRS = ['GUI', 'ModuleData']

repo = {}
repo.update(root_files(REPO))
repo.update(collect(REPO, SUBDIRS))

game = {}
game.update(root_files(GAME))
game.update(collect(GAME, SUBDIRS))

dist = {}
dist.update(root_files(DIST))
dist.update(collect(DIST, SUBDIRS))

# 只关心交付候选（排除仓库侧的开发杂物）
REPO_NOISE = {
    'AGENTS.md', 'AWAKE.csproj',
}
repo_deliver = {k: v for k, v in repo.items()
                if not k.startswith('_') and k not in REPO_NOISE}

print("=" * 78)
print("A. 模块清单（按目录分组）")
print("=" * 78)


def group_stats(d, prefix):
    sub = {k: v for k, v in d.items() if k.startswith(prefix)}
    return len(sub), sum(sub.values())


for grp in ['GUI/Prefabs/', 'GUI/Brushes/', 'GUI/SpriteParts/',
            'ModuleData/Languages/', 'ModuleData/Worldbook/',
            'ModuleData/Rules/', 'ModuleData/Knowledge/']:
    rn, rb = group_stats(repo_deliver, grp)
    dn, db = group_stats(dist, grp)
    gn, gb = group_stats(game, grp)
    print(f"{grp:<28} 仓库={rn:>5}  dist={dn:>5}  游戏={gn:>5}")

for f in ['SubModule.xml', 'README_CN.md', 'README_EN.txt',
          'BUILD_VERIFICATION.txt', 'THIRD-PARTY-NOTICES.txt',
          'GUI/AWAKESpriteData.xml']:
    r = '有' if f in repo_deliver else '无'
    d = '有' if f in dist else '无'
    g = '有' if f in game else '无'
    print(f"{f:<28} 仓库={r:>3}  dist={d:>3}  游戏={g:>3}")

print()
print("=" * 78)
print("B. 仓库有、游戏目录【缺失】的交付文件")
print("=" * 78)
missing = sorted(set(repo_deliver) - set(game))
for m in missing:
    print("  MISSING  " + m)
if not missing:
    print("  (无)")

print()
print("=" * 78)
print("C. 游戏目录有、仓库【没有】的文件（前 60，多为玩家数据/历史混入）")
print("=" * 78)
extra = sorted(set(game) - set(repo_deliver))
for e in extra[:60]:
    print("  EXTRA    " + e)
print(f"  ... 共 {len(extra)} 项")

print()
print("=" * 78)
print("D. 仓库有、dist staging【缺失】的文件（投送链会丢什么）")
print("=" * 78)
dist_missing = sorted(set(repo_deliver) - set(dist))
for m in dist_missing:
    print("  DIST-MISS  " + m)
print(f"  共 {len(dist_missing)} 项")
