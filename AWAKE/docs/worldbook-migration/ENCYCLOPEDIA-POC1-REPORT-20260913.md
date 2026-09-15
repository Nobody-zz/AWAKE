# 百科化 PoC 验收报告（物品批 10 件）

> 2026-09-13 · 依据 `WORLDBOOK-ENCYCLOPEDIA-CHARTER-20260913.md` v0.3 §七 验收标准执行。

## 结论：通过

| 验收标准（宪章 §七） | 结果 |
|---|---|
| 10 档全部过六步编译 0 错误 | ✅ geo1-full8（manifest `b37c5c81…`，audit 0 错误，50 档全量） |
| 矩阵新增探针全 PASS | ✅ IT1–IT10 全过；总矩阵 **74/74**（旧 64 行回归无破坏） |
| 自检 0 违规 | ✅ 50 档 0 违规；物品档 **0 警告**（余 18 警告全为地理批挂账） |
| 单条人工成本可计量 | ✅ L2 文案 10 条×3 段一次写定（生成器内嵌数据字典）；底座层零人工 |
| DB 描述文覆盖率出实数 | ✅ 物品叙述描述文 **≈0%**、官方译名 **≈100%**（已回写宪章 §三4） |

## 交付清单

- **10 档**：`item-{salt,fur,velvet,spice,silver,saddle_horse,mule,pack_camel,cow,sheep}.yaml`
  （货物×5 → economy/goods；马×3＋牲畜×2 → economy/items）
- **来源三件套**：`sources/game-items-poc1.txt`（快照 hash `BAD48C3D…`）＋ `sources/source-game-items-poc1.yaml` ＋ 档内 sources 引用（hash 三处一致，quote 全部子串定位）
- **生成器**：`_ency_poc_gen_20260913.py`（DB→快照→登记→yaml 全自动；红线全守：ignore_aliases／slug 不进文档体／六身份显式／min_detail==layer）
- **矩阵**：spec 74 行＋verify 预期（IT 系列）；工具 `_add_matrix_items_20260913.py`
- **编译产物**：`compiled/geo1-full8/`（gitignore 内不入库）

## 权限验证（IT 探针语义）

| 身份 | 层 | 结果 |
|---|---|---|
| villager / townsfolk | rumor | partial（低于默认请求层，语义正确——村民只配拿到传闻） |
| merchant / soldier / headman / noble | detail | known，正文含"基准价 140"等**属性成文** |
| 乱词"钢剑" | — | not_found（对照行） |

意外发现（设计内行为）：IT7"毛皮"同词同时命中旧档 der-furs（德里亚特毛皮生计）与新物品档——多域机制按设计返回双档。

## 过程坑（2 笔，皆当场修复）

1. 六步脚本 OPBASE 复用上批值 → WB-AUTHORITY-OPERATION-409；编译器 response 无 `ok` 键再次误判失败（manifest_hash 才是判据）。
2. 物品探针命名 I1–I10 与帕拉汶德旧探针前缀相撞 → 改 IT- 前缀（矩阵 spec 命名空间需要前缀登记约定，后续批用 `IT-/EQ-/CN-` 等按品类领前缀）。

## 下一步（PoC2 候选）

- 武器/护甲类：items 投影表未收录（只有 crafting_pieces 与 mp_ 占位），需从 `SandBoxCore/ModuleData/items/*.xml` 建第二条提取通道。
- 译名 token 快照化：当前快照行已带 token，可进一步把 alias 扩展挂官方复数形等变体。
- 规模化：生成器已验证"底座零人工＋观感批量写"，可按品类铺开。
