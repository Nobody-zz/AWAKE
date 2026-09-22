# 双写一致性核对与镜像同步

**日期**：2026-09-20　**线**：世界书　**状态**：**已修复（302 档），两侧逐字节一致**

---

## 一、缘起

甲方令：`把镜像修了，同步现役的内容`（起因是发现 `weapons-head-kettle` 两侧标题不一致）。

**没只修那一档** —— 先做全量比对。两侧目录：

- 现役（编译源，库外）：`tools/worldbook-studio/workspace/full-geo1/authoring/`
- 镜像（入库留档，库内）：`docs/worldbook-migration/projection/authoring-out/`

## 二、核对方法与结果

`diff -rq` 两侧目录 ⇒ **302 档内容不等**（不是 1 档）。

再做**结构化**核对（`tools/_mirror_sync_check_20260920.py`：解析 yaml 后逐字段递归 diff），差异只有三处：

| 差异路径 | 档数 | 方向 |
|---|---|---|
| `/entity_ids` | **301** | **现役有、镜像没有** |
| `/title/zh-CN` | 1 | kettle：现役`圆顶锅盔`／镜像`锅盔` |
| `/aliases/zh-CN` | 1 | 同 kettle（标题改了，别名跟着不同） |

**关键前置**：核对同时输出「**镜像独有项**」——**结果为空**。⇒ 覆盖不会单向丢内容，才敢往下做。

## 三、根因

按文件名前缀看分布：

- **曾不一致 302 档** ＝ `villages` **267** ／ `towns` **34** ／ `weapons` **1**
- **本来就一致 181 档** 里 —— `castles` **67 档全在**

⇒ 对应 2026-09-14 `CASTLE-LINKAGE-PILOT-20260914.md` 的批量计划：
**P1 城堡档 67 座（净新增）** 两边都写了；**P2 村庄档 273 档补 `entity_ids`／P3 城镇档 53 档** **只写了现役**。

⇒ **单写造成的静默缺口**：镜像少了一整个字段，而平时只比 title／aliases 看不出来。

## 四、修复

`tools/_mirror_sync_apply_20260920.py`：

1. **先备份当前镜像全量** → `projection/_archive-alias-tighten-20260920/AO-before-mirror-sync/`
   （镜像工作区当时已带未提交改动 ⇒ `git checkout` 回不到「同步前」这个状态，必须另备）；
2. 以现役**逐字节覆盖** 302 档；
3. **复验**：逐字节不一致 **0 档** ⇒ 两侧完全一致。

名录重刷：`OK rows=483 exprs=1163 grants=7092`。

**kettle 同步后形态更对**：标题「圆顶锅盔」＋ 别名留「锅盔」当简称 —— 正是别名收紧后的正确形态
（旧镜像里标题就是「锅盔」，别名再放「锅盔」即死条）。口径见 `ALIAS-POLICY-20260920.md`。

## 五、已落规矩

写进技能 `worldbook-encyclopedia-rollout-batch`「硬约束·双写复验」：

- 每批收尾**必对两侧目录做逐字节比对**（`diff -rq`），**不能只比 title／aliases**；
- 同步前先证「**镜像无独有内容**」，否则覆盖 ＝ 单向丢数据；
- 覆盖前**备份当前镜像**，覆盖后**复验归零**。

## 六、脚本与备份

| 路径 | 用途 |
|---|---|
| `tools/_mirror_sync_check_20260920.py` | 结构化核对：差异路径分类 ＋ **镜像独有项全列** |
| `tools/_mirror_sync_apply_20260920.py` | 以现役覆盖镜像（含备份 ＋ 复验） |
| `projection/_archive-alias-tighten-20260920/AO-before-mirror-sync/` | 同步前镜像全量（302 档） |
