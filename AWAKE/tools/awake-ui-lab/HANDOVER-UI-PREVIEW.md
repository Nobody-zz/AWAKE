# AWAKE UI 预览程序 · 交接说明

> 给「UI 资产生成」会话的对接文档。
> 配套设计规格书见同目录 `UI-ART-SPEC-20260913.md`（资产清单 / 命名 / 接入路径都写在那里）。

---

## 1. 这是什么

`awake-ui-lab` 是 AWAKE 模组的 **UI 离线预览与审计工具**。它把 `GUI/Prefabs/*.xml` 按真实的
Gauntlet 布局语义解析一遍，然后：

1. **几何推导** —— 算出每个控件的实际矩形（含 Margin / SizePolicy / ListPanel 堆叠）
2. **原版贴图铺图** —— 从游戏 SpriteData + 图集里取真实 sprite，按控件尺寸铺上去
3. **审计** —— 报「魔数 / 非 5 倍数网格 / Brush 未命中」等问题
4. **出图** —— 渲染成 PNG 截图 + HTML 预览 + 统一报告

**它不启动游戏。** 用它可以秒级看到「改完的界面大概长什么样」。

---

## 2. 环境要求

| 项 | 要求 |
|---|---|
| Python | 本机 `C:/Users/26811/.workbuddy/binaries/python/versions/3.13.12/python.exe` |
| 截图 | 需要本机装了 Chrome 或 Edge（脚本会自动探测；也可用 `--chrome` 指定） |
| 游戏 | 默认从 `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord` 取原版 sprite/Brush |
| 可选依赖 | `Pillow`（PIL）。缺了不崩，只是 sprite 铺图与九宫格合成会退化 |

---

## 3. 怎么跑

**本包可脱离仓库独立运行**：解压到任意目录，`cd` 进你解压出来的那一层即可。
（Prefab 默认取 `AWAKE/GUI/Prefabs`；该目录不存在时**自动退回包内** `samples/prefabs/`，
所以开箱就能 `check`，不用手填路径。）

在仓库里开发时，则 `cd` 到 `tools/awake-ui-lab/`：

```bash
# 解压后的独立副本（路径换成你自己的）
cd <你解压的目录>

# 仓库内开发
cd D:/AWAKE-Dev/AWAKE/tools/awake-ui-lab

# 只看我们自己的两个窗口（最快）
"C:/Users/26811/.workbuddy/binaries/python/versions/3.13.12/python.exe" awake_ui.py check \
    --prefab AwakeMessenger.xml --prefab NpcDialogue.xml

# 全部 Prefab + 跑 C# 接线自测（较慢，几分钟）
"C:/Users/26811/.workbuddy/binaries/python/versions/3.13.12/python.exe" awake_ui.py check

# 与上一次快照比较（看这次改了什么）
"C:/Users/26811/.workbuddy/binaries/python/versions/3.13.12/python.exe" awake_ui.py diff
```

### 常用参数

| 参数 | 作用 |
|---|---|
| `--prefab 文件名` | 只处理指定 Prefab，可重复 |
| `--prefab-dir 目录` | 换个 Prefab 来源目录（默认 `AWAKE/GUI/Prefabs`） |
| `--out 目录` | 输出目录（默认 `out/`） |
| `--no-flow` | 跳过 C# 接线自测，只做几何 + 审计 + 截图（快很多） |
| `--no-native` | 不做原版 Brush 取证 |
| `--no-shot` | 不截图 |
| `--json` | 结果以 JSON 打到 stdout |
| `--strict` | 有 warn/error 时退出码 1 |

### 输出在哪

| 产物 | 路径 |
|---|---|
| 截图 | `out/shot/<Prefab 名>.png` |
| HTML 预览（单窗口） | `out/shot/<Prefab 名>.html` |
| 统一报告 | `out/index.html` |
| 结构化结果 | `out/ui-report.v1.json` |

---

## 4. ★ 怎么预览你自己产出的贴图

这是交接的重点。**你不需要改任何 Python 代码，也不需要重新编译索引。**

把 PNG 按 Prefab 里写的 **sprite 名**丢进这个目录即可：

```
tools/awake-ui-lab/out/atlas/custom/<sprite 名>.png
```

命名规则：sprite 名里的 `\` 和 `/` 替换成 `__`。例：

| Prefab 里写 | 你要放的文件 |
|---|---|
| `Sprite="awake_panel_9"` | `out/atlas/custom/awake_panel_9.png` |
| `Sprite="Awake\Panel\main_9"` | `out/atlas/custom/Awake__Panel__main_9.png` |

优先级：**`custom/` 覆盖 > 游戏原版 sprite**。同名时你的贴图会顶掉原版，方便做加减法对比。

> 如果 Prefab 还没写进你的 sprite 名，改的是别的会话负责的 XML —— 可以先用
> `--prefab-dir` 指向你自己的测试 Prefab 目录，不动主线文件。

### 九宫格（9-slice）

`sprite 名` 以 `_9` 结尾 = 九宫格。四角不拉伸、四边单向拉伸、中心双向拉伸。
**游戏原版 sprite 的九宫格参数**来自 `NativeSpriteData.xml` 的 `<NineRegionSprite>`
（`LeftWidth` / `RightWidth` / `TopHeight` / `BottomHeight`），预览器会自动读取。

⚠️ **已知限制**：`custom/` 里的自定义 `_9` sprite **拿不到九宫格参数**，预览会整体拉伸。
产出时请在交付说明里带上你的 `Extend*` 值（规格书 §5 要求），集成时补进 SpriteData/XML。

---

## 5. 判图前务必知道的 4 条保真度边界

预览**不是**真机渲染。判断前先对照这四条（这也是我们踩过坑才补上的）：

1. **只画几何与贴图，不执行数据绑定。** VM 的 `IsVisible` 状态、列表内容都是夹具占位，
   `@属性名` 绑定在静态图里不会按状态切换 → 该隐藏的节点会常显。
2. **字体是近似。** 文本用系统字体（微软雅黑等）度量，不是游戏里的 Galahad/FiraSans，
   字宽会有偏差，**中文字形尤其不可信**。
3. **`Color` 是逐通道相乘染色**，格式 `#RRGGBBAA`（末两位是 alpha）。
   **不是 8 位的值会被静默丢弃**（例如写成 10 位 `#FF0D0A07FF` → 该控件不上色）。
4. **中文排版细节**（换行、截断、行高）不可依赖预览判断，必须真机确认。

---

## 6. 目录结构

```
tools/awake-ui-lab/
├─ awake_ui.py                     入口（子命令 check / diff）
├─ README.md                       工具总说明（更详细）
├─ HANDOVER-UI-PREVIEW.md          本文件
├─ UI-ART-SPEC-20260913.md         设计规格书（资产清单/命名/接入路径）
├─ preview/
│  ├─ preview_prefab_geometry.py   几何推导 + SVG/HTML 渲染（核心，78KB）
│  ├─ ui_sprite_atlas.py           sprite 索引 + 图集裁切 + 染色 + 九宫格合成
│  ├─ ui_text_metrics.py           字体度量 + Brush 解析
│  └─ README.md                    预览层说明
├─ assets/                         基线数据
├─ fixtures/                       ListPanel 夹具
├─ src/  tests/                    C# 接线自测（--flow 用）
└─ out/                            产物目录（截图/报告/图集缓存，可再生）
   ├─ shot/                        截图与 HTML
   └─ atlas/
      ├─ *.png                     游戏原始图集（大，可从游戏重新解码）
      ├─ sprites/                  已裁出的单 sprite
      └─ custom/                   ★ 你自己的贴图放这里
```

---

## 7. 现在这个界面的状态（你接手时的基线）

- **骨架**：已重设计并提交（`4b3da30`）—— 三栏弹性布局、单行 Tab、气泡左右分侧、发送为唯一主按钮
- **皮肤**：**失败，未提交**。当前工作树里的换装用了错误的资产（把只作 `OverlaySprite` 用的
  `stone_texture_overlay` 当面板主底），渲染出来是「近黑盒子 + 色块」
- **你要做的**：按规格书产出资产 → 丢进 `out/atlas/custom/` 预览 → 交付 sprite 名 + Extend 值

改动**不要**直接动 `src/`、不要动本地化文件；Prefab 的集成由 UI 侧会话（本线）负责。

---

## 8. 常见问题

**Q：截图是空白的 / 只有线框？**
A：正常降级。检查 `Pillow` 是否装了、游戏路径是否存在、`out/atlas/*.png` 是否在。
脚本不报错、自动退回分类色是设计行为。

**Q：我改了 PNG，图没变？**
A：**先确认你的 `custom/` 文件名和 Prefab 里写的 sprite 名一模一样**（`\` `/` 换成 `__`）。
名称不对会静默回落到原版 sprite——不报错。
若名称没问题，看是否用了**九宫格**（`_9` 结尾）：合成结果缓存在 `out/atlas/sprites/__nine_*`。

> ⚠️ **2026-09-13 修过一个与此相关的 bug**：合成缓存的键原先只有「文件名+尺寸」，
> 不看源图有没有变 ⇒ 你把新图丢进 `custom/` 后重跑，会命中**旧合成图**、预览静默不变。
> 现已改成：**`custom/` 里的图每次现算**；其余缓存按 `mtime+size` 判新鲜度。
> 如果你的工具是从旧包里解出来的，这条就是坑。见契约 §6。


**Q：报告里的「问题数」是什么？**
A：审计项 —— 非 5 倍数网格的原版栅格偏差、未定义的 Brush、布局魔数等。
**目标是 0**。当前两个窗口都是 0。

**Q：能预览别的模组的界面吗？**
A：可以，`--prefab-dir` 指向那个目录即可，`--no-native` 可跳过取证。
