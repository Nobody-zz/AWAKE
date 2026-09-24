# 派工单（续）· 主干线 · 存储一致性 P1-02 → P1-06（2026-09-24）

> **这份文件整份就是提示词，全选复制给主干线会话即可。**
> 前单：`AWAKE/docs/HANDOFF-STORAGE-20260922-MAIN.md`（仍在，两项已交，剩四项）。本单是它的续做。

---

## 【你已经交的两件，我核过】

| 单 | 提交 | 我的复核 |
|---|---|---|
| P1-05 信件半提交 | `637d648` | ✅ 编排摘出 ＋ 半提交留痕，测试与读数都带上了 |
| token 统计（另单） | `dab478a` | ✅ **比我给的方案好** |

**token 单特别记一笔**：我建议"给两处各补一个 case"，你改成**放在分支之前统一记一行** ——
理由（两处形状不同、分别补就会分别漏）比我原方案硬。你另发现的
「多次用量事件是**从头累计**、不是增量 ⇒ 取最大不能累加」也已成立，我没意见。
你**把"离线压不住的两个调用点"标出来、按未验记账** —— 这个态度继续保持。

## 【本单：接着做剩下四项】

顺序就按前单的，**做完一条交一条，不必等全做完**：

1. **P1-02 坏分片恢复** —— 纯离线（读代码 ＋ 夹具），先做。
2. **P1-01 写盘假成功** —— 已实证，要做的是修法落地，**但先解决夹具卡点**（见下）。
3. **P1-03 revision 写放大** —— **只交数字，不写判据、不改实现**。
4. **P1-06 unbound campaign** —— 先判"能不能离线证"，很可能只能交给主控去真机。

⚠️ **P1-04（Overlay 半提交）不在此列** —— 它属世界书线，人家已经做完、卡在提交裁决。别碰 `WorldKnowledge*.cs`。

## 【行号已替你复核过（09-24）】

`WorldStateStore.cs` 与 `AwakeFileStorageService.cs` **自 09-15（`437bedd`）起没动过**，
所以前单里的行号**现在仍然准确**。动手前仍请自行 spot-check。

**P1-02 的关键位置**（原文在前单，这里给全，省你翻）：

| 行 | 是什么 |
|---|---|
| `WorldStateStore.cs:3671` | 只在 root 读成 `Corrupt` 时进隔离分支 |
| `:3678` | 隔离后当作 `Missing`，facts 从空开始 |
| `:3707` | 随后用新 revision 重建 chunks（key 含 revision ⇒ 新 key 与旧的不同） |
| `:3771` | 隔离只复制 root（`key = RootKey + ".quarantine"`），**不动 chunks** |
| `:920/934/937` | chunk 侧三种坏法：`chunk_missing`／`chunk_key_mismatch`／`chunk_json_invalid` |

要回答三问：
- **(a)** **root 好、某一个 chunk 坏**时：读返回什么？写会发生什么？隔离分支会不会被触发？
  （`:3671` 只看 root 状态 ⇒ 推测**不触发**，但那是推测，**去测**。）
- **(b)** 坏 root 被隔离、新 revision 写入后，**旧的健康 chunks 还在存储里吗**？
  如果在、又没人再引用它们 ⇒ 是永不可达的孤儿。**读一次，说出来。**
- **(c)** 连续两次坏根，第一次的 `.quarantine` 还在不在？（`:3756-3758` 注释说同名覆盖 —— 去验。）

**P1-01 的关键位置**（修法约束在下面「红线」）：

```
AwakeFileStorageService.cs:116      JsonFileKeyValueStore 是私有嵌套类（夹具卡点）
AwakeFileStorageService.cs:140      GetAsync 走 Load()
AwakeFileStorageService.cs:163      SetAsync 拿到 current = Load() ⇒ current 就是缓存本身
AwakeFileStorageService.cs:168      catch 只写日志 + 返回失败，不恢复 values
AwakeFileStorageService.cs:209      Load() 惰性加载，values != null 就永远返回同一实例
AwakeFileStorageService.cs:245      Save() = temp 写入 + File.Replace ⇒ Windows 上可能"已替换成功然后才抛"
```

## 【一条新的提交约束（重要，别踩）】

**`AWAKE.Tests/Program.cs` 现在工作区里有他线（世界书线）163 行在途。**
你 09-23 已经有过一次成功经验（`_split_patch_20260923.py`）。本轮若还要动这个文件：

1. 按 hunk 挑出**只有你的**段落 → `git apply --cached --recount`；
2. **`git commit -F <msg>` 不带 pathspec**（带 pathspec 提交的是工作区整份，是反的）；
3. 三件套复核：`git show HEAD:<文件> | grep -c <你的符号>` ＋ 行数差 ＋ 工作区 diff 里你的符号应为 0。

**别引用"两天前的核对结论"。** 世界书线 09-22 核过这个文件是干净的，09-23 就被你动过 —— 用之前重核。

## 【第一原则（不变）】

**先让判据红，再改实现让它绿。** 现在全绿（工作区三笔新用例叠加后 **67** 例 ——
09-20 基线 64 → P1-05 +1 → overlay +1 → token +1；两线各自跑的数也对得上），
说明这几条**没有一条被判据覆盖** ——
现在修等于"修完还是全绿"，证明不了修好了。修完仍要做**变异检验**，证明判据还能 FAIL。
**别拿"编译不过"当红的读数**（那不算判据有分辨力）。

## 【红线】

- **P1-01 是框架契约变更，不是局部补丁 —— 先出方案，别直接动手。**
  已知的错误修法（已有共识）：`SetAsync` 里加 `if (!rootStored) return` ——
  这会把"已写但报错"的合法结果误报成失败。
  目标契约是**三态**：`definitely_not_written`／`written`／`unknown`；只有原子替换完成之后才发布新缓存；
  `unknown` 时用**绕过实例缓存的全新磁盘读回**确认。
  → 影响面：`IKeyValueStore` 是框架侧公开契约（`framework/MarcusAwakeFramework/src/StorageAndRagApi.cs:48-53`），
  **三个方法是 `GetAsync → OperationResult<string>`、`SetAsync`／`DeleteAsync → OperationResult<bool>`**。
  要区分"必未写"与"结果未知"，**`bool` 这个返回类型承载不了** ⇒ 要么扩契约、要么约定错误码语义。
  ⚠️ 前单 `HANDOFF-STORAGE-20260922-MAIN.md` 的 (m) 那段把三个方法都写成返回 `OperationResult<bool>`，
  **那是错的，以本单为准**（原单已就地更正）。
  **把方案与影响面写出来再谈动手。**
- **P1-03 只交数字**，别给它写红绿。（容量目标甲方还没给，改不改由主控据此定夺。）
- 不宽 pathspec 提交；不碰 `WorldKnowledge*.cs`；不碰 `framework/`（除你已交的）；**不排期**。

## 【出处】

- 前单：`AWAKE/docs/HANDOFF-STORAGE-20260922-MAIN.md`
- 缺陷来源：`AWAKE/docs/AUDIT-SYSTEM-CODEX-20260922-R2.md`（P1-01 已实证；P1-02/03/06 为未证风险）
- P1-01 复现探针（带阳性对照，已入库）：`AWAKE/tools/_verify_p1_01_20260922.ps1` ＋ `.txt`
- 全线状态：`AWAKE/docs/REVIEW-ALL-LINES-20260924.md`

---

*—— 全局主控线 · 2026-09-24*
