# full20 `WB-AUTHORITY-CAS-409` 故障说明与 full21 终态（2026-09-14）

> 范围：throne 副题补丁批的编译收官。**不是坏文件、不是数据损坏**——是与另一条线（`awake-prose-qc` 文本质检）并发写入的竞态。
> 终态：`compiled/geo1-full21`，**矩阵 113/113 PASS**，0 error。

## 一、症状

throne 两档改中文副题（`萨涅俄帕·隘口商埠与旧都` / `帕拉汶德·旧帝都的沦陷`）后派生 `geo1-full20`，跑满 24m25s，register/select/approve/proof 四步全绿，**最后一步 compile 失败**：

```json
{"ok": false, "error": "WB-AUTHORITY-CAS-409", "status": 500, "side_effect": "none"}
```

## 二、取证链

| 步 | 动作 | 结论 |
|---|---|---|
| 1 | 定位错误点 `AuthorityGate.cs:433` | compile 把 proof 里每档 `content_hash`/`revision` 与磁盘现读比对，不符即 409 |
| 2 | `_diag_cas409_20260913.py` 逐档比 246 档 | **仅 2 档**不符：`tale-charas-origin`、`peninsula-kachar`；revision 未变（排除行尾/BOM） |
| 3 | 读 `AuthorityGate.cs:186` | proof 的 `items` **克隆自 select/approval** ⇒ `content_hash` ＝**注册时** `InputHash`（文件字节 sha256），**不是编译时现读** |
| 4 | 查磁盘 mtime | **7 档同在 23:53:14 被双写**（authoring-out 与 WS 两侧同秒）：5 村 `mijayit/hamoshawat/ataconia/morenia/saldannis` ＋ `peninsula-kachar` ＋ `tale-charas-origin`。5 村「改后才注册」故一致；2 档「注册后又被改」故分叉 |
| 5 | `_diag_cas409c/d` 用 `document-revisions/<docid>/<rev>.json`（**存全文**）diff | 每档**只改 1 行 `text.zh-CN`**，均为去 AI 腔改写 |

## 三、根因

**另一条线并发落盘**：`awake-prose-qc`（`~/.workbuddy/skills/awake-prose-qc/`，世界书/角色卡用词口径质检）同线的落盘脚本 `docs/evidence/awake-prose-qc-20260913/patch_worldbook.py`，去 `沉积`×5 / `战略`×1 / `考古`×1，**双写**两侧、并同步改我方 3 个 `_rollout_villages*_gen`（防重跑回退），**quote 官方原文一律不动**。它 23:53 落盘，正撞 full20 的 proof→compile 窗口。

> 该技能本身**只出报告不改文件**；落盘是同线一次性脚本。

## 四、处置与关键捷径

**不改回其文本**（属对方合法产出）；改为重跑让快照吃到新正文。

- 先写了 DLL 直调版六步 `_villages3g_sixsteps_20260913.py`（`dotnet run --project` → 直调 `bin/Release/net10.0/worldbook-studio.dll`）。
- 跑到第 136 位发现**register 越跑越慢**（工作区已积 3122 条 operation / journal 3118 行，每次 CLI 调用回放 ⇒ 近二次方）。
- 遂写 `_check_head_vs_disk_20260914.py` 验证 **head↔磁盘 246/246 一致**（两个分叉档恰在第 36、46 位、本轮已重注册）⇒ **掐掉 register，直接跑后半段** `_villages3h_tail_20260914.py`（select→approve→proof→compile），**34 秒出包**。

> **原理**：select/proof 取的是 `authoring-v1/workspace-head.json` 里 register 写的 hash，**不是现读磁盘** ⇒ head 一致即可跳过 register。**每批重跑前先跑这个校验，能省 ~19 分钟。**

## 五、终态核对（full21）

- compile：manifest `97a5cb96219f5611`，validation 21 项，**errors 0**。
- 矩阵探针 `matrix2-spec.json`（113 条）：**PASS 113 / FAIL 0**。
- 入包核对（**编译包 JSON 是 `\uXXXX` 转义，须 `json.load` 后搜，`grep` 匹配不到中文**）：
  - throne 两档新副题 ✓；
  - prose-qc 新句在（`只在传闻里流传`／`用兵之地`）、旧句无（`尚未得到考古`／`战略用途`）✓；
  - 残留 12 处 `沉积` **全在 `quote` 字段**（A 级官方原文，依纪律保留）✓。

## 六、遗留

1. prose-qc 报告仍挂 **6 档软项**待人工确认：`结构`（item-western_plated_helmet）、`系统`（troop-mamluk／weapon-crossbow）、`水系`（village-atrion／village-lavenia）、`技术`（village-deir-hawa／weapon-crossbow）。**若再改世界书正文，须重跑「head 校验 → 后半段」。**
2. 全批（村庄批 3 ＋ 命名规范化 ＋ 本批脚本/报告）**未提交**，待令。
