# 全库档名/ id 规范化验收报告（2026-09-13）

> 目标：统一为「`doc.<域>.<次级分类>-<词条名>`」+ 同名前缀文件名（与 `town-`/`village-`/`item-` 同风格）。
> 终态包：`geo1-full19`（full17/18 为中间批）。留痕：`corrections_20260913/RENAME-IDS-20260913.md`。

## 一、规范定义

- **文件名与档内 id 尾一致**，形如 `<次级分类>-<名>`：`town-charas`、`river-sethys`、`throne-saneopa`、`troop-mamluk`。
- **次级分类词表**：geography→`town`/`village`/`mount`/`mountains`/`plateau`/`peninsula`/`lake`/`desert`/`river`/`sea`/`bay`；culture→`tale`；economy→`item`/`furs`/`mine`；politics→`throne`/`territory`/`clan`；war→`weapon`/`troop`/`military`。
- 附带：档内 `assertion./expr.` 前缀同步；字段顺序与 block 风格对齐主流（`sources` 先于 `authority`）。

## 二、改动规模（38 档）

| 批 | 内容 | 档数 |
|---|---|---|
| A | 聚落类后缀式→前缀式：`charas-town`→`town-charas`、`husn-fulq-town`→`town-husn-fulq`、`lycaron-town`→`town-lycaron`、`varcheg-town`→`town-varcheg`、`der-vill`→`village-deriat` | 5 |
| B | 全库分类前缀：自然地理 12、culture 6、economy 2、politics 7、war 6 | 33 |

- 规范化后首词分布：`town 53 / village 138 / item 20 / tale 6 / territory 4 / military 3 / lake 3 / mount 2 / mountains 2 / troop 2 / throne 2 / river 2`，其余各 1；**全库 246 档无一例外**。
- 无跨档 id 引用（全库已核），改名不牵动其它活档；历史留档（`IMPL-GEO1-PERMISSION-20260912.md` 等）不改，走 corrections 留痕。

## 三、编译与矩阵

| 项 | 结果 |
|---|---|
| geo1-full17（聚落批） | register **246/246**、compile **0 错误** |
| geo1-full18（全库分类批） | register **246/246**、compile **0 错误** |
| geo1-full19（检索面补批，终态） | register **246/246**、compile **0 错误** |
| 全库规模 | **246 档 / 589 表达 / 3343 grants**（不变） |
| 矩阵 | **113/113 PASS** |
| 自检 v2 | 违规 **0**（警告 18 条全旧挂账） |
| 名录 | 246 行；cross_dup **2**（charas 家族批旧挂账） |

## 四、过程要点（一条真教训）

- **title 去副题 × 检索面**：早期 4 城镇档原 title 为「名·副题」（如「沙拉斯·城与港」「吕卡隆·石山要塞」），按新批规范改为纯名后，**副题退出 compiler 的 Keywords**（Keywords=title+aliases+anchor+sourceId），导致矩阵 A1/A2/A8/A10 四条回归探针（检索词正是「城与港」「石山要塞」）转 `not_found`。full18 判定 **109/113**，暴露此问题。
- **补救**：title 保持纯名（格式统一），把原副题全文与拆分词补进 `aliases.zh-CN`（项目纪律「检索全靠 aliases」），full19 重编译后恢复 **113/113**。
- 另记：全库规范化脚本首次运行被外部中断（写新未删旧），改为**幂等续跑**（新已存在则只删旧）后收敛。
