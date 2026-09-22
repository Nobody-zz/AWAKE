# 别名口径（ALIAS-POLICY）

**日期**：2026-09-20　**线**：世界书　**状态**：**C 类已清、A 类已删、B 类保留**（均 09-20 甲方裁定）

---

## 一、别名的定义（收紧后的口径）

**别名 ＝ 同一事物的别称**，进检索面，用于把玩家/NPC 可能说出的**另一种叫法**接到本档。

**判据不是「这个词是不是这一档的名字」，而是「这个词该不该召回这一档」。**
本档 title 本身不算别称（检索面已由 title 覆盖）；别的档的名字不算别称（那是另一个事物）。

## 二、检索面是怎么构成的（一手代码）

`RuntimePackageCompiler.cs:112-122`（K1）：

```
keywords = title(多语言) → aliases(多语言) → entity_ids 的「可读名」   依次加入，Ordinal 去重
```

- `entity_ids` 折入的是**登记表里的可读名**（`display_name_zh`/`english_name`/`family_name_zh`/`aliases`），
  **不是** `entity.settlement.castle_b1` 这个串本身（`ResolveEntityAnchorNames`，同文件 :361-386）。
- 运行时检索面 ＝ `Id + Title + Keywords` 三者上的**双向子串**命中（`OrdinalIgnoreCase`），
  `WorldKnowledgeQueryService.cs:131`；排序按 `MatchQuality`（title 全等 Rank0 → keyword 全等 Rank1
  → title 子串 Rank2 → keyword 子串 Rank3，:449-461）。
- ⇒ **编译产物 `runtime.json` 的 `keywords` 是检索面的唯一真值**。档里写了、`keywords` 里没有的 ＝ 死条。

## 三、三类可疑（机械可核）

脚本：`tools/_alias_audit_20260920.py`（分类）、`tools/_alias_redundancy_20260920.py`（判来源）。

| 类 | 定义 | 条数 | 删了会怎样（**实测**） |
|---|---|---|---|
| **A** | 别名含游戏实体 id（`castle_B1`／`village_EW1`…） | 388 | **拿 id 问就查不到了**（探针：`castle_B3` 由 Rank1 → 无命中） |
| **B** | 别名 == 别档 title（跨档挂名） | 141 真检索词（另 4 冗余） | **从村子跳不到它上头那座堡**（探针：`托·梅利纳` 由 村+堡 → 只剩村）；同名村仍能召回堡档，但**降级**（Rank1→Rank2） |
| **C** | 别名 == 本档 title | **894（100% 冗余）** | **零影响**（探针：移除 0 个 keywords 串，命中逐条不变） |

**阳性对照**（`_alias_redundancy_20260920.py`）：按 K1 规则重算 keywords，与真产物逐档比对
——**482/482 完全相同**。故此表判据成立；清理 C 类后复跑，**仍 482/482 全绿**（独立复证零影响）。

## 四、处置（09-20 甲方裁定）

1. **C 类 —— 已清**。`tools/_alias_tighten_20260920.py --cls C --apply`，双侧 934 档 / 删 1790 条值；
   清空的语言段一并删头（674 个），两段全空则删整个 `aliases`（4 处）——依 schema：
   `localized_aliases.minProperties=1` 且每段必须是 array。改动档 `revision +1`。
2. **A 类 —— 已删**（甲方：`A类删`）。同脚本 `--cls A --apply`，双侧 776 档 / 删 776 条值；
   其中 321 档（单侧）删到 `aliases` 整段归零（村档除本档名与 sid 外本无别名）。
3. **B 类 —— 保留**（甲方：`B类理应留着`）。这是 2026-09-14 `CASTLE-LINKAGE-PILOT` 的设计
   （堡档收下辖村名 ⇒ 问村名同时召回堡档），该批已用「按谁是被问的排前」整改过排序。

**两次操作的校验**：各在操作前后跑一次 studio `validate` —— **均 `Valid: true`、0 诊断、Code 集合相同**。
**两类性质被实测区分开**（同一把尺子，结论相反）：

| 操作 | 按 K1 重算 keywords vs 旧产物 v18 | 含义 |
|---|---|---|
| 删 C 类 | **482/482 全等** | 检索面**没变** ⇒ 纯去重 |
| 删 A 类 | **388 档不等**（94 档仍等） | 检索面**真的变了** ⇒ 拿内部代号问不到了 |

**备份**：`projection/_archive-alias-tighten-20260920/`（`authoring-WS`＝原始、`authoring-WS-afterC`＝清完 C 后）
＋名录 `_archive-WORLDBOOK-NAME-INDEX-20260920-{pre-tighten,pre-A}.xlsx`。

### 4.1 与「语义调取」的关系（别混淆两套机制）

- **别名** ＝ **字面入口**：问什么词命中哪一档，靠**串匹配**（`Id/Title/Keywords` 子串）。
- **语义调取** ＝ **正文互引边**：靠「A 条正文点名了 B 条的名字」成边，见
  `docs/AWAKE-Worldbook-Link-Spec-v1.md`（甲方 09-18 拍定；产物 `docs/mappings/worldbook-should-link/<日期>/should-link.v2.json`）。
  该规范分**双向**（`mutual=true` ⇒ `usableAs=["forward","backward"]`，可反向用）与
  **单向**（`out` ⇒ 只 `forward`；**入边禁反向用**，否则「凡提过它的都算相关」＝噪声换命中）。
- ⇒ **两者不冲突**：B 类保留的是**字面**那条路；语义调取是**另一条**路，且尚未接进运行时召回。

## 五、对生成器的约束（防复发）

C 类的**源头是生成器模板**：`_gen_*` 普遍写成 `aliases.zh-CN = [<本档 title>, …]`
（如 `_gen_troops_cataphract_20260919.py:237`、`_gen_war1_20260913.py:175`）。

⇒ **今后生成器与手写档一律不得把本档 title 放进 aliases**；`aliases` 只写「同事物的另一种叫法」。

## 六、脚本清单

| 脚本 | 用途 |
|---|---|
| `tools/_alias_audit_20260920.py` | 三类可疑的机械分类与影响面 |
| `tools/_alias_liveness_20260920.py` | 别名「在不在编译产物 keywords 里」 |
| `tools/_alias_redundancy_20260920.py` | 逐条判**唯一来源**（title／锚点／别名）＋ 482/482 阳性对照 |
| `tools/_alias_impact_probe_20260920.py` | 三类删除的**检索后果**实测（复刻 Search/MatchQuality） |
| `tools/_alias_tighten_20260920.py` | C 类清理（默认 dry-run，`--apply` 写盘） |
| `tools/_alias_validate_20260920.py` | 清理前后 studio validate 对比 |
