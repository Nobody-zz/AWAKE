# 事故留痕：通配符移走 op 记录，触发 studio 的孤儿回收

**日期**：2026-09-17 约 18:13–18:24
**责任人**：本轮作者（AWAKE 世界书 v13 重编）
**影响面**：`tools/worldbook-studio/workspace/full-geo1/`（studio 工作区，**gitignore 里**，不是仓库上线件）
**线上件是否受影响**：**否**。真机读的是 `ModuleData/Worldbook/packages/calradia/`，全程完好。

---

## 一、经过

v13 重编第一次 compile 报 `WB-AUTHORITY-MUTATION-UNKNOWN`（503）。这个码是
`AuthorityGate.cs` 的**兜底包装**，真因写在 stderr，被我用错的子进程解码吃掉了
（见下方"教训 1"）。我为了抓真因，写了个恢复脚本重跑 compile —— 那个脚本第 1 版有**两个错**：

1. 清理时用**通配** `k1_op_*` / `k1_rv_*` 移走文件 ⇒ 一次移走了 **52 个历史 op 记录**（不只是本次那一个）；
2. `--out` 传的是 `compiled/...`，漏了工作区前缀 ⇒ 报 `WB-PATH-003`，于是又重跑了一次。

op 记录不见了之后重跑 compile，studio 的 `QuarantineCompileOrphans()`（`AuthorityGate.cs:1330`）
按设计执行：它把 `compiled/` 下**没有 op 记录（tracked）的一级条目**当作孤儿移进
`compiled/quarantine/orphans/`。

我随后把 op 文件放回去，但产出一度丢过，`validate` 触发 `RecoverCompileOperation` ⇒
**53 个 op 被标成 `state=quarantined` + `failure_code=WB-AUTHORITY-RECOVERY-409`**，
它们的 marker / result 被隔离进 `compiled/quarantine/<opid>/`。

## 二、现在的实况（2026-09-17 19:0x 清点）

| 项 | 数量 | 位置 |
|---|---:|---|
| op 记录总数 | 4677 | `authoring-v1/operations/` |
| 其中 `state=committed` | 4622 | — |
| 其中 `state=quarantined`（全带 `WB-AUTHORITY-RECOVERY-409`） | **53** | — |
| 被隔离的产物目录（`geo1-*`） | **43** | `compiled/quarantine/orphans/` |
| 被隔离的报告目录（`reports.*`） | **4724** | `compiled/quarantine/orphans/` |
| 被隔离的 op 目录（`customer.compile.v1.<hash>`） | **53** | `compiled/quarantine/` |
| `compiled/` 下现存的产物 | 3（v13 / v13e / v13f，都是本轮编的） | `compiled/` |

**没有任何内容被删**：43 个产物目录与 4724 个报告目录只是**换了位置**。
清点脚本：`tools/_inventory_quarantine_20260917.py`（只读）。

## 三、处置

**没有做批量还原**，理由写在这里免得下次有人以为漏了：

- 把 4767 个目录搬回去、再逐条改 53 个 op 的 state，是**又一次大规模移动**——
  正是这一类操作出了这次的事。收益是"工作区的历史 lineage 更好看"，代价是再冒一次险。
- 下游**没有任何东西**依赖 `compiled/` 里的历史产物：真机读 `ModuleData/Worldbook/`；
  studio 只读 `authoring-v1/`；离线验台读 `ModuleData/Worldbook/`。
- 唯一"当时就要用"的东西是 **v12 对拍基线**，已经从隔离区取出并冻结：
  `tools/_baseline/geo1-v12-runtime.json`（sha256 `6f796dfe…`，与上线包逐字节一致，已复核）。

如果哪天确实需要还原，做法是：把 `compiled/quarantine/orphans/<名字>` 移回 `compiled/<名字>`，
并把 `authoring-v1/operations/` 里 `state=quarantined` 的对应记录改回 `committed`
（`compiled/quarantine/<opid>/` 里留着当时的 marker/result，可据此对号）。

## 四、教训（已落到下一份产出上，不是只写在这里）

1. **清理只许动本次那一个 operation，绝不允许通配。**
   本次唯一的错在第 1 条通配上——`k1_op_*` 看着"就是清理临时文件"，实际是全部历史。
2. **子进程必须自己按 utf-8 解码**（`capture_output=True` 不给 `text=`，拿回 bytes 再 decode）。
   Windows 下 `text=True` 用 GBK 解码，会**静默丢掉 stderr 里的真因**，只剩一个兜底错误码。
   本次把 `WB-AUTHORITY-COMPILE-422 → WB-SOURCE-001` 一路吞成了一个 `503`。
3. **看错误码之前先问"它是不是兜底包装"。**
   `WB-AUTHORITY-MUTATION-UNKNOWN`（503 / side_effect=unknown）是包装码，
   真因一定在 stderr（`Program.cs:212`：`if (status == 503) Console.Error.WriteLine(...)`）。
4. **恢复脚本自己要能看见自己干了什么。** 第 1 版没有"移走了几个文件"的打印，
   所以我是在 compile 出问题后才倒推出 52 个文件不见了。
