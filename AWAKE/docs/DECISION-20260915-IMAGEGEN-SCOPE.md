# 甲方口径记录 · 生图（F-024 / F-050）定级覆盖

> **性质**：跨线**甲方决策记录**。不是修订矩阵，不是新的能力分级——是给两份 08-24 契约文档挂上「口径已变」的指针。
> **硬要求**：留痕必须写清**时间、谁拍的、原话**；三个月后必须还能查出这是谁的决定。

---

## 一、决策本身

| 项 | 内容 |
|---|---|
| 时间 | **2026-09-15** |
| 谁拍的 | **甲方 Max** |
| 原话 | **「我建议做回去」**（指把生图适配器恢复进框架） |
| 覆盖对象 | `F-024 Player2 Adapter`、`F-050 Image Generation` 的**定级与优先级** |
| 归口 | 路线图 **v0.4「有脸」**——甲方口径本来就是"NPC 有脸"，不是"做生图" |

## 二、被覆盖的原文（**一个字都不改**）

- `MARCUS-AWAKE-CAPABILITY-CARRYOVER-MATRIX-v1-20260824.md`：生图列为 **A2「后续继承、不是第一版核心」**，
  夹具名直接叫 `ImageGenerationUnavailableFixture` / `Player2UnavailableFixture`。
- `MARCUS-AWAKE-CAPABILITY-OWNERSHIP-MAP-v1-20260824.md`：`F-024` / `F-050` 状态均为 **`deferred`**。

⇒ 因此框架里那个 `UnavailableMediaService` 空壳**不是谁偷懒**，是契约当时明文要求"先老实报不可用、给稳定失败码"。
**改原文才是篡改历史**；正确做法是原文保留 ＋ 挂指针。

## 三、覆盖后的口径

1. **定级仍记作 A2 / `deferred`**（原判断有效），但**已被甲方口径覆盖**，不再作为"不做"的依据。
2. **优先级提到最前**（甲方 09-15 明确），与 `F-063…F-066` 并列进入当前视野。
3. **验收归口**：进 v0.4「有脸」的过版判据（判据原文见 `docs/AWAKE-ROADMAP.md` 版本阶梯表）。

## 四、附带取证提醒（**是"搬回原位置"，不是"重建"**）

**原版 Marcus 框架源码已在手上**（已获授权；本机 `%TEMP%\marcus_repo`，77 个 `.cs` / 22,100 行，
含 `src/MarcusAIFramework.Companion/AssetEngine.cs`（728 行，资产 CAS）、`ProviderRouter.Operations.cs`、`Core/GovernedServices.cs`）。
⇒ 「做回去」＝ **port：把原版生图那几件搬回它自己原来的位置**——不是按契约文档重建一条管线，也不是"取消注释"。

- **已落地**：**片 1**（`MarcusAwakeProvider` 单模块：Player2 ＋ OpenAI 兼容适配器 ＋ 离线判据，提交 `8a947ce`）。
- **未落地**：片 2 打通线（Transport media 消息 / `RuntimeServiceHost` 分派 / `HostApi` 换真实现）、片 3 资产 CAS、片 4 调用方切换。
- **方案出处**：`docs/PLAN-IMAGE-PORT-TO-FRAMEWORK-20260915.md`（09-15 09:28 提交 `b1c5075`，12:47 `ce3896b` 补片 1 记录）。

**仍然成立的一条**：本地化版里 `ProviderKind` 只剩 3 类、无 media 消息、无 CAS ⇒ **不是"框架没有生图"，是"本地化这一版没有"**。
另外，任何"生图已经能用了"的说法，都得拿**游戏内实际出图**当证据。

> ⚠️ **本节的更正留痕**：本节初稿（09-15 17:40 提交 `f5e91f4`）写的是
> 「**原始媒体实现源码不在手上** ⇒ 做回去＝按契约＋设计文档重建一条管线」——**该表述作废**。
> 它与同日上午已入库的 `PLAN-IMAGE-PORT-TO-FRAMEWORK-20260915.md` 直接冲突（该方案明文：「原版源码现在拿到了」）。
> 现状以本节为准；作废表述只作留痕，不得再引用。

## 五、留痕落点（三处指针，正文全部一字不动）

| # | 落点 | 形态 |
|---|---|---|
| 1 | `MARCUS-AWAKE-CAPABILITY-CARRYOVER-MATRIX-v1-20260824.md` 抬头 | 加一条不可删的「口径变更」注，指向本文件 |
| 2 | `MARCUS-AWAKE-CAPABILITY-OWNERSHIP-MAP-v1-20260824.md` 的 `F-024` / `F-050` 两行 | 状态列加指针；`deferred` 字样**保留** |
| 3 | `docs/AWAKE-ROADMAP.md` 版本阶梯 v0.4 行 | 把生图写进过版判据（本主控线维护） |

**裁决出处**：`docs/RULING-CODE-LINE-HANDOVER-20260915.md` §四。
