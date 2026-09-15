# 家族批（Batch F1）选题稿

> 状态：**方案待 Max 定调子**，未动笔写正文。
> 依据：条目章法 §七 挂账 1–3 条＋红测三（登记表查无科尔坦）＋今日两批收尾后的现状。
> 领跑原则：一实体一档；家族档只写"家族整体"，个人英雄事迹归角色卡侧（trae），本批不碰。

---

## 一、现状与病灶（三档一冷摊）

| 档 | 域/子域 | 现状 | 病灶 |
|---|---|---|---|
| `charas-reign` 沙拉斯·归属**与科尔坦家** | politics/territories | 2 断言：归属(1)＋财富底气(2) | 标题与内容两主题粘连 |
| `charas-cortain-secret` 沙拉斯·科尔坦家的账 | politics/clans | 1 断言 3 表达：公开面＋秘密面混装 | summary 自述"分层分档"但公开/秘密混在一档 |
| `charas-town` 沙拉斯·城与港 | geography/settlements | 3 断言，城史两说并录 | 无病灶，本批不动 |

交叉双投挂账：官方源 **town_V7 沙拉斯城描述文**（引文 A6320996 / 3C991D66）被 `cortain-secret` 与 `reign` 双方引用——同一变体两处投。

另：登记表 890 实体（hero 415 / clan 82 / settlement 393）**查无科尔坦**——立 clan 锚点档前须先走 §3.5 扩表流程。

## 二、拆分方案（按章法档型四种）

### 1. 新立 `doc.clan.cortain`（科尔坦家·家族本档，politics/clans）
- **装什么**：家族构成（戴·科尔坦家与沙拉斯的关系）、财富事实面（海务财富尽归此家）、家族与城的关系。
- **素材**：现 `reign-2`（财富底气）＋ `cortain-secret` 公开面（所有权事实）整体迁入；A 级引文随迁。
- **先决**：登记表扩 clan 实体（科尔坦家）＋相关 hero 位（只占位挂锚，不写人物详情）。

### 2. `charas-reign` 瘦身为纯归属沿革（politics/territories）
- 标题改「沙拉斯·归属」；保留断言 1（瓦兰迪亚人到来后归戴·科尔坦家）＋易主沿革；
- **财富主题（断言 2）整段迁出**至 clan 档；
- 旧标题词进 aliases 保检索（sturgia 军制先例）；矩阵受影响行同步改预期（§4.3）。

### 3. `charas-cortain-secret` 转纯秘密档
- 公开面（3 表达全身份 grant）迁 clan 档后，本档只留政治解读/把柄/软肋类秘密层；
- denies 定向到具体身份（禁祖链根，§3.6）。

### 4. 双投引文裁定（09-13 复核后修订）
- town_V7 描述文实际被**四档引用**：`charas-town`（地理，10+ 处，**主消费方**）、`charas-origin-tales`、`charas-reign`、`charas-cortain-secret`。
- **主引权归 `charas-town`（地理）**——描述文讲城的区位/港口/地貌，地理域是天然调取场景，保留整段引文。
- `reign` / `secret` / `origin-tales` 三档改为**更小子串引文**（hash 随之改变），从"整段双投"降为"合法多域引用"，交叉警消除（naha 先例同法）。

## 三、权限设计草案

- **clan 档**（身份覆盖按调取场景）：
  - 村民（沙拉斯本地）：rumor「城里的营生都归科尔坦老爷家」
  - 商人：summary（与海务财富有生意往来）
  - 市民/头人：summary
  - 贵族（vlandia 门）：detail（家族势力构成）
  - 士兵：rumor 或 summary（征粮驻防接触面）
- **secret 档**：noble（vlandia 或特定家族门）detail/secret；denies 封死其他身份。
- 不变式 `grant.min_detail == layer` 自检兜底；写档前过 §四 硬门 7 条。

## 四、流程与回归

1. 扩登记表（clan/hero 锚点）→ 2. 本方案签收 → 3. 写三档改一档新立一档 → 4. 红测（自检 v2＋矩阵加 F 行约 8 条：村民本地 rumor / 商人 summary / vlandia 贵族 detail / 异文化贵族 blocked / 秘密档 noble secret / 士兵 blocked / 旧标题别名检索 / A6320996 单档回归）→ 5. 六步编译 geo1-full8 → 6. 对账表＋章法 §七 状态更新收档。

## 五、顺带核对（本批收口时更新章法 §七 状态）

- §七 4（三档 en title）：整改批已清零 → 标已完成；
- §七 5（规模存量）：**今日规模收敛批已清零**（5 档断言合并＋paravenos 拆组，矩阵 64/64）→ 标已完成；
- §七 6（军制 3 档）：**今日身份覆盖批已修**（sturgia/royal-guard 补 merchant，mamluk 补 soldier；W13–W15 探针转绿）→ 标已完成；
- §七 1–3：本批执行。

## 六、工具链本批新增血泪（已修）

1. **yaml 重排会产生锚点/别名**（`&id001/*id001`）——工作室解析器禁用（WB-YAML-006），连锁炸出 73 条假 schema 错。修法：重排前 JSON 往返断开共享引用＋`ignore_aliases`。下批起凡脚本重排 yaml 一律走此通道。
2. **compile 失败异常被吞**（MUTATION-UNKNOWN 无因由）——已改 `AuthorityMutationUnknownException` 携带真实原因，CLI 往 stderr 打。
3. `--out` 相对路径按**进程 CWD**（仓库根）解析，非 workspace——正确写法 `tools/worldbook-studio/workspace/full-geo1/compiled/<pkg>`。
4. 六步编排脚本必须显式断言 `res["ok"]`（本次曾漏检导致误报 DONE）。
