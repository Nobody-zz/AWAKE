# awake-art-lab · AWAKE UI 美术生产线

> 给 AWAKE 模组的 Gauntlet UI 出**自有贴图**（面板底 / 按钮 / 蜡印关闭键 / 栏头 / 分隔线）。
> 归属：图标 & 插画会话。落盘交付给 UI 管理会话集成。

---

## 1. 这是什么

**不是**一个手绘工作台，是一套**参数化的石质 UI 生成器**。

原版骑砍的 UI 是"凿出来的石头"：切角、厚斜面、K金只做细线。
本项目沿用这个材质语言，但把母题换成 **碑刻 / 铭牌**——一块碑，不同的人读到不同层次
（对应世界书的"中立内核 + 分层观感"）。

```
artkit.py        几何 / 材质 / 图层工具箱（切角多边形、噪声归一化、超采样画布）
parts.py         各部件的画法（碑面 / 凹槽 / 铭牌 / 蜡印 / 横牌 / 金线）
build_m0.py      M0 生产驱动（出图 + 写交付目录 + 写预览覆盖 + 出联络单）
_study_ref.py    原版参照放大图（out/study/ref_*.png）
_measure_ref.py  原版几何与配色实测（量出来，不靠目测）
_map_brushes.py  解「控件 → Brush → 底层 sprite」链，用来定同名覆盖
_inspect.py      放大看 UI Lab 出的截图
_compare.py      出「原版 vs AWAKE 皮肤」对比图
```

---

## 2. 怎么跑

```bash
cd D:/AWAKE-Dev/AWAKE/tools/awake-art-lab
PY="C:/Users/26811/.workbuddy/binaries/python/versions/3.13.12/python.exe"

"$PY" build_m0.py                # 出图 + 写 AWAKE/GUI/SpriteParts/ + 写预览覆盖
"$PY" build_m0.py --no-preview   # 不碰 UI Lab 的 custom/
"$PY" build_m0.py --clear-preview # 只撤掉预览覆盖

"$PY" _measure_ref.py            # 量原版
"$PY" _inspect.py NpcDialogue 4 40,60,240,120   # 放大截图（倍率 + 裁切框）
"$PY" _compare.py                # 原版 vs 皮肤对比图
```

**看效果的正确姿势**（预览覆盖已由 `build_m0.py` 写好）：

```bash
cd D:/AWAKE-Dev/AWAKE/tools/awake-ui-lab
"$PY" awake_ui.py check --no-flow
# 无头环境一样能出图 → out/shot/*.png
```

---

## 3. 产出在哪

| 产物 | 路径 | 进 git？ |
| --- | --- | --- |
| **交付贴图（源图）** | `AWAKE/GUI/SpriteParts/ui_awake_{frame,ornament,button}/` | ✅ |
| 分类声明 | `AWAKE/GUI/SpriteParts/Config.xml`（含 `<AlwaysLoad/>`） | ✅ |
| 集成清单 | `tools/awake-art-lab/out/m0-manifest.json`（名/尺寸/九宫格/顶替谁） | ❌ out/ 已忽略 |
| 联络单 / 细节单 | `out/sheet_m0.png`、`out/sheet_m0_detail.png` | ❌ |
| 前后对比 | `out/compare_before_after.png` | ❌ |
| 预览覆盖 | `tools/awake-ui-lab/out/atlas/custom/`（同名顶掉原版） | ❌ |

---

## 4. 硬规矩（改之前先读）

**画风**
- 金只做线与点，**不做大面积**。金＝`#D9A953`（原版对话面板实测）。
- 受光规则：**凸起 → 光在上/左；凹进 → 光在下/右**。反了立刻变贴纸。
- 四禁忌：拟物（木纹/皮革/金属反光）、科技感、奇幻魔法、大面积金。
- sprite 里**绝不烤文字**（中文 fallback + 多语言会错）。

**技术**
- 尺寸一律取 **5 的倍数**（原版习惯 + AWAKE 4/12/24 栅格）。
- 面板底**取面板原尺寸**出图（如 960×720 就出 960×720）：面板固定尺寸下，
  9-slice 边距相同时各区域 1:1 映射 ⇒ 像素级精确、纹理不被拉伸。
- 九宫格边距**写进 Brushes XML**（`<BrushLayer ExtendLeft/Top/Right/Bottom>`），
  **不能写在 sprite 上**（全库 10 个 `*SpriteData.xml` 里 0 处 Extend）。
- 逐状态出图时，**四个状态的九宫格边距必须一致**，否则按下会跳。

**材质**
- 噪声用 `artkit.textured(delta=...)`：**按目标标准差归一化**，`delta` ＝ 期望峰谷幅度。
  别退回 overlay（暗底会被压平）或固定系数（暗部撞纯黑出硬噪点）——两个坑都踩过。
- 参考强度：碑面 σ≈4.4（原版弹窗底 6.8）｜按钮内衬 σ≈2.3｜禁用态 σ≈0.6。

---

## 5. 待办

- **M1**：侧栏条带、联系人条目、聊天气泡 ×2、列表行、凹槽、小标记键。
- **M2**：头像框、四角饰、标题名石、**14 个白剪影图标**（用户已定要引入，
  但需 UI 侧指定"哪几个文字按钮改成图标按钮"才有落点）。
- ⛔ **管线阻塞**：本机没装 Modding Kit，出图不受影响，但**真机验收做不了**。
- ⚠️ `tools/awake-ui-lab/` 归 UI 线，本工具只往里写 `out/`（gitignore 内）；
  改它的 `preview/` 代码需与该线协调（09-13 修过一个合成缓存键的 bug，已记录）。
