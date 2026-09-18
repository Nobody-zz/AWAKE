# id / 文件名规范化留痕（2026-09-13）

> 目的：全库档名与档内 id 统一为「`doc.<域>.<次级分类>-<词条名>`」式（与 `town-`/`village-`/`item-` 同风格）。
> 落点：`projection/authoring-out/` ＋ `workspace/full-geo1/authoring/`（双写）。
> 影响：**历史留档不改**（下列文档仍含旧 id，仅作历史引用，非活档）：`IMPL-GEO1-PERMISSION-20260912.md`、`GAME-DATA-ACCESS-AND-ANCHOR-DISCREPANCY-20260912.md`、`STATUS-20260912.md`、`PLAN-*`、`docs/evidence/*.json`、`docs/review-state/*.json`、`projection/*.json`（ANCHOR-BINDING-PLAN / CLUSTER-REFERRAL-MAP / LORE-ENTITY-REGISTER）。
> 结论：**无跨档 id 引用**（已全库核验），改名不影响其它活档；六步链重编译重建 register/referral。

## 一、聚落类（后缀式 → 前缀式，5 档）

| 旧文件名 / 旧 id | 新文件名 / 新 id |
|---|---|
| `charas-town.yaml` / `doc.geography.charas-town` | `town-charas.yaml` / `doc.geography.town-charas` |
| `husn-fulq-town.yaml` / `doc.geography.husn-fulq-town` | `town-husn-fulq.yaml` / `doc.geography.town-husn-fulq` |
| `lycaron-town.yaml` / `doc.geography.lycaron-town` | `town-lycaron.yaml` / `doc.geography.town-lycaron` |
| `varcheg-town.yaml` / `doc.geography.varcheg-town` | `town-varcheg.yaml` / `doc.geography.town-varcheg` |
| `der-vill.yaml` / `doc.geography.deriat-village` | `village-deriat.yaml` / `doc.geography.village-deriat` |

（title 副题不迁移，如「沙拉斯·城与港」→「沙拉斯」；`entity_ids` 与全部 sources/assertions 内容保留。）

## 二、全库分类前缀规范化（33 档）

**分类词表**：geography→`mountains-`/`plateau-`/`peninsula-`/`lake-`/`desert-`/`river-`/`sea-`/`bay-`；culture→`tale-`；economy→`furs-`/`mine-`；politics→`throne-`/`territory-`/`clan-`；war→`weapon-`/`troop-`/`military-`。

| 旧 id | 新 id |
|---|---|
| `doc.geography.charas-bay` | `doc.geography.bay-charas` |
| `doc.geography.dawn-mountains` | `doc.geography.mountains-dawn` |
| `doc.geography.devseg-plateau` | `doc.geography.plateau-devseg` |
| `doc.geography.dryatic-mountains` | `doc.geography.mountains-dryatic` |
| `doc.geography.kachar-peninsula` | `doc.geography.peninsula-kachar` |
| `doc.geography.lakonis-lake` | `doc.geography.lake-lakonis` |
| `doc.geography.llyn-modris` | `doc.geography.lake-llyn-modris` |
| `doc.geography.miron-river` | `doc.geography.river-miron` |
| `doc.geography.nahasa-desert` | `doc.geography.desert-nahasa` |
| `doc.geography.perassic-sea` | `doc.geography.sea-perassic` |
| `doc.geography.sethys-river` | `doc.geography.river-sethys` |
| `doc.geography.tanaesis-lake` | `doc.geography.lake-tanaesis` |
| `doc.culture.charas-origin-tales` | `doc.culture.tale-charas-origin` |
| `doc.culture.dawn-taboo` | `doc.culture.tale-dawn-taboo` |
| `doc.culture.husn-fulq-tales` | `doc.culture.tale-husn-fulq` |
| `doc.culture.kachar-three-tales` | `doc.culture.tale-kachar-three` |
| `doc.culture.lakonis-lake-tales` | `doc.culture.tale-lakonis-lake` |
| `doc.culture.lycaron-rock-tales` | `doc.culture.tale-lycaron-rock` |
| `doc.economy.deriat-furs` | `doc.economy.furs-deriat` |
| `doc.economy.lycaron-mines` | `doc.economy.mine-lycaron` |
| `doc.politics.saneopa` | `doc.politics.throne-saneopa` |
| `doc.politics.paravenos` | `doc.politics.throne-paravenos` |
| `doc.politics.charas-reign` | `doc.politics.territory-charas-reign` |
| `doc.politics.dawn-stewardship` | `doc.politics.territory-dawn-stewardship` |
| `doc.politics.kachar-ownership` | `doc.politics.territory-kachar-ownership` |
| `doc.politics.varcheg-swap` | `doc.politics.territory-varcheg-swap` |
| `doc.politics.charas-cortain-secret` | `doc.politics.clan-charas-cortain-secret` |
| `doc.war.crossbow` | `doc.war.weapon-crossbow` |
| `doc.war.mamluk` | `doc.war.troop-mamluk` |
| `doc.war.royal-guard` | `doc.war.troop-royal-guard` |
| `doc.war.khuzait-military` | `doc.war.military-khuzait` |
| `doc.war.sturgia-military` | `doc.war.military-sturgia` |
| `doc.war.vlandia-military` | `doc.war.military-vlandia` |

## 三、附带规范化

- 档内 `assertion.*` / `expr.*` 前缀同步（如 `expr.sethys-river-*` → `expr.river-sethys-*`）。
- 字段顺序与 block 风格对齐主流（`sources` 先于 `authority`；flow-style 档转 block），`entity_ids` 保留于 `aliases` 之后。

## 四、执行脚本

- `_rename_town_ids_20260913.py`（聚落类 5 档）
- `_normalize_filenames_20260913.py`（全库 33 档，幂等可续跑）

## 五、编译验收

- geo1-full17（聚落类批）：register 246/246、compile 0 错误。
- geo1-full18（全库分类批）：见当日验收报告 / 日志。
