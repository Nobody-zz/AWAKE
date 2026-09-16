# 「编译那把钥匙」到底是什么

> 2026-09-16 · 纯排查记录。起因：我把 `compile` 要的那个 proof 说成「服务端签发的钥匙，我造不出来」，甲方没听懂。
> **结论：没有服务端，也没有别人。那句话是我说错了。**

---

## 一、一句话答案

那把「钥匙」＝**上一步命令自己打印出来的一串编号**，存在本机同一个工作区文件夹里。
它不需要任何人批准，我自己跑一条命令就能生成。

---

## 二、它长什么样、在哪

以仓库里已经跑过的那条链为例（`tools/worldbook-studio/workspace/full-geo1/_v2i_chain_log.txt`）：

```
[2/5] select  -> selection.c64bdf0dfa164daf9c43f49a3079d060
[3/5] approve -> approval.db2861d3b234439bb7862946dc640d12
[4/5] proof   -> compile.cb262d0b2b2f4a609c31323336d364a8      ← 这就是「钥匙」
[5/5] compile OK
```

- 「钥匙」＝ `compile.cb262d0b…` 这一串。
- 它被写成文件，位置：`tools/worldbook-studio/workspace/full-geo1/authoring-v1/compile-proofs/compile.cb262d0b….json`
- 上一步 `approval.*`、再上一步 `selection.*`，同理。

---

## 三、那句「必须由服务端签发」是怎么回事

代码里确实有这道校验（`AuthorityGate.cs:425`）：

```csharp
if (proof["issuer"] != ServerIssuer || proof["immutable"] != true)
    throw new InvalidOperationException("WB-AUTHORITY-PROOF-409: CompileProof 不是 server-issued immutable proof。");
```

而 `ServerIssuer` 是**同一个文件里写死的常量**（`AuthorityGate.cs:9`）：

```csharp
private const string ServerIssuer = "awake.worldbook-studio.authoring-v1";
```

两个事实：

1. 这个 `issuer` 字段是**签发那条命令自己填进文件里的**（`AuthorityGate.cs:190`），签发与校验是**同一个程序**。
2. 校验只是把它和自己文件里那句常量比一下 ⇒ **对暗号，不是远程授权**。

⇒ 没有服务器、没有密钥、没有人签字。**任何人只要能跑这条 CLI，就能自己出这张证。**

---

## 四、那它到底拦住了什么

拦「跳步」，不拦「外人」。两件事：

| 想干的事 | 会不会被拦 | 为什么 |
|---|---|---|
| 跳过登记 / 选择 / 批准，直接 `compile` | 拦 | 不给 `--proof` 直接报 `WB-AUTHORITY-400`（`Cli/Program.cs:81`） |
| 拿 A 批的批准去编 B 批的稿子 | 拦 | proof 里存着 selection 的**条目清单与哈希**，`compile` 会逐个复核（`AuthorityGate.cs:475` 一带） |
| 自己按顺序走完四步再编译 | **不拦** | 这就是设计路径 |

⇒ 它是一把**内容锁**（防内容和批文对不上），不是一把**权限锁**（防人）。我把后者当前者讲，讲岔了。

---

## 五、所以「重编 448 档」的实际前置条件

只有一条真的缺：

- **改过的编译器还没进二进制。**
  - `RuntimePackageCompiler.cs` 改动时间 **2026-09-16 15:14**
  - CLI 二进制 `src/Awake.WorldbookStudio.Cli/bin/Release/net10.0/worldbook-studio.dll` 生成时间 **2026-09-16 00:32**
  - ⇒ 12 小时前的旧 DLL，改动不可能生效。**重编一次 Release 即可，不需要任何授权。**

重编之后，链是现成的（脚本在仓库里，不用手敲）：

```bash
python -u tools/worldbook-studio/workspace/full-geo1/_v2i_batch_20260915.py
```

它依次做五步，全自动：
`扫出改动档 → authoring-register-batch → authoring-select → authoring-approve → authoring-proof → compile → 回读校验`

⚠️ 两个脚本层的坑（同上日志与脚本注释里已记）：

1. **`--operation` 是幂等的**：同一个 operation id 再跑一次会被短路成「返回上次结果」，**不会重读磁盘**。改过内容的档必须换新的 operation id，否则是「假成功」。
2. **同一个 proof 不能换 `--out` 重编**：`request_digest` 里含输出目录，换了目录会报 `WB-AUTHORITY-OPERATION-409`。要换目录得**出新的 proof**。

重编后同步到游戏侧：`ModuleData/Worldbook/packages/calradia/`（现在那份是 `compiled/geo1-v6/` 的副本，字节与时间戳一致，见下）。

---

## 六、取证命令

```bash
# 链条日志（谁什么时候出了哪张证）
cat tools/worldbook-studio/workspace/full-geo1/_v2i_chain_log.txt

# 五步的完整命令原文
cat tools/worldbook-studio/workspace/full-geo1/_v2i_batch_20260915.py

# 校验点与常量
grep -n "ServerIssuer" tools/worldbook-studio/src/Awake.WorldbookStudio.Core/AuthorityGate.cs

# 时间戳对照（证明改动还没进二进制）
stat -c '%y %n' \
  tools/worldbook-studio/src/Awake.WorldbookStudio.Core/RuntimePackageCompiler.cs \
  tools/worldbook-studio/src/Awake.WorldbookStudio.Cli/bin/Release/net10.0/worldbook-studio.dll

# 产物仍是「compile 必需 proof」前的样子
grep -o '"doc\.[a-z0-9.-]*"' ModuleData/Worldbook/packages/calradia/runtime.json | wc -l   # 1308
```

---

## 七、我错在哪

- 把一串**自己打印的编号**讲成「服务端签发的钥匙」，还据此说「我造不出来」⇒ 等于凭空造了一个不存在的外部权威，把一件自己就能干的事讲成要等别人。
- 正确说法：**这把锁是本机程序自己上的，钥匙也在本机程序手里。** 缺的只是「重编一次二进制」，不是「找谁批」。
