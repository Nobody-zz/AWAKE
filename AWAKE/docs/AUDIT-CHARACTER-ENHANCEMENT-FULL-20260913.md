# PERSONA ENHANCEMENT LINT - 加强规范强校验 V2（三张力轴 / 自由样本 / 条件化 / 破模版）

> 范围：全量
> 判定：E1/E2/E2b/E3/E4/E5 硬 FAIL；V2 弃用 8 类锚词与统一问答框。

| 卡 | 样本数 | E1条数 | E2三轴 | E2b入运行时 | E3条件化 | E4破例 | E5模版 | 结果 |
|---|---|---|---|---|---|---|---|---|
| 阿巴该 | 3 | Y | - | - | Y | Y | Y | FAIL(3) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:全部样本套用统一问答框（3/3），未脱模版
  -  E5WARN:开头雷同 阿巴该如何回应‘
| 阿庇斯 | 2 | - | - | - | Y | Y | Y | FAIL(4) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:过半样本套用问答框（1/2），未脱模版
| 阿德拉姆 | 2 | - | - | - | Y | Y | Y | FAIL(4) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:全部样本套用统一问答框（2/2），未脱模版
  -  E5WARN:开头雷同 阿德拉姆会如何回
| 阿尔德里克 | 2 | - | - | - | Y | Y | Y | FAIL(4) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:过半样本套用问答框（1/2），未脱模版
  -  E5WARN:开头雷同 阿尔德里克会如何
| 阿尔瓦 | 3 | Y | - | - | Y | Y | Y | FAIL(3) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:全部样本套用统一问答框（3/3），未脱模版
  -  E5WARN:开头雷同 阿尔瓦如何回应‘
| 阿卡尔 | 3 | Y | - | - | Y | Y | - | FAIL(2) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  -  E5WARN:含问答框 1/3
| 阿克鲁木 | 3 | Y | - | - | Y | Y | Y | FAIL(3) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:过半样本套用问答框（2/3），未脱模版
| 阿拉德维尔 | 2 | - | - | Y | Y | Y | - | FAIL(2) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  -  M1WARN:疑似纯台词样本(引文占比 0.74)
| 阿拉里 | 3 | Y | - | - | - | Y | Y | FAIL(4) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词；无可让/条件词）
  - E3:运行时无条件化信号词（若/但凡/除非/一旦…）
  - E5:过半样本套用问答框（2/3），未脱模版
| 阿丝塔 | 3 | Y | - | - | Y | Y | Y | FAIL(3) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:全部样本套用统一问答框（3/3），未脱模版
  -  E5WARN:开头雷同 阿丝塔如何回应‘
| 埃尔贡 | 2 | - | - | - | Y | Y | Y | FAIL(4) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:过半样本套用问答框（1/2），未脱模版
| 埃卡朗 | 3 | Y | - | Y | Y | Y | - | FAIL(1) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  -  E5WARN:含问答框 1/3 M1WARN:疑似纯台词样本(引文占比 0.73)
| 埃隆 | 2 | - | - | - | Y | Y | - | FAIL(3) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  -  M1WARN:疑似纯台词样本(引文占比 0.77)
| 奥列克 | 2 | - | - | - | Y | Y | Y | FAIL(4) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:过半样本套用问答框（1/2），未脱模版
| 奥斯皮尔 | 3 | Y | - | - | Y | Y | Y | FAIL(3) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:全部样本套用统一问答框（3/3），未脱模版
  -  E5WARN:开头雷同 奥斯皮尔会如何回
| 奥赞 | 3 | Y | - | - | Y | Y | Y | FAIL(3) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:全部样本套用统一问答框（3/3），未脱模版
| 拔该 | 3 | Y | - | Y | Y | Y | - | FAIL(1) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  -  E5WARN:含问答框 1/3 M1WARN:疑似纯台词样本(引文占比 0.71)
| 贝尔吉尔 | 3 | Y | - | - | Y | Y | Y | FAIL(3) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:过半样本套用问答框（2/3），未脱模版
| 伯里康 | 2 | - | - | - | - | Y | Y | FAIL(5) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无可让/条件词）
  - E3:运行时无条件化信号词（若/但凡/除非/一旦…）
  - E5:全部样本套用统一问答框（2/2），未脱模版
| 布兰诺克 | 3 | Y | - | - | - | Y | Y | FAIL(4) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词；无可让/条件词）
  - E3:运行时无条件化信号词（若/但凡/除非/一旦…）
  - E5:过半样本套用问答框（2/3），未脱模版
| 达思鲁儿 | 3 | Y | - | - | - | Y | Y | FAIL(4) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词；无可让/条件词）
  - E3:运行时无条件化信号词（若/但凡/除非/一旦…）
  - E5:过半样本套用问答框（2/3），未脱模版
| 德泰尔 | 2 | - | - | - | Y | Y | Y | FAIL(4) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:过半样本套用问答框（1/2），未脱模版
| 俄洛斯 | 3 | Y | - | - | Y | Y | Y | FAIL(3) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:过半样本套用问答框（2/3），未脱模版
| 法芬 | 2 | - | - | - | Y | Y | Y | FAIL(4) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:全部样本套用统一问答框（2/2），未脱模版
| 法戎 | 2 | - | - | Y | Y | Y | Y | FAIL(3) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E5:过半样本套用问答框（1/2），未脱模版
| 菲利诺拉 | 3 | Y | - | - | Y | Y | Y | FAIL(3) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:过半样本套用问答框（2/3），未脱模版
  -  M1WARN:疑似纯台词样本(引文占比 0.71)
| 戈敦 | 2 | - | - | - | Y | Y | Y | FAIL(4) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:全部样本套用统一问答框（2/2），未脱模版
| 古速坎 | 3 | Y | - | - | Y | Y | Y | FAIL(3) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:过半样本套用问答框（2/3），未脱模版
| 哈珊 | 2 | - | - | - | - | Y | Y | FAIL(5) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词；无可让/条件词）
  - E3:运行时无条件化信号词（若/但凡/除非/一旦…）
  - E5:全部样本套用统一问答框（2/2），未脱模版
| 合努占 | 3 | Y | - | - | Y | Y | Y | FAIL(3) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:过半样本套用问答框（2/3），未脱模版
| 赫卡尔 | 3 | Y | - | Y | Y | Y | Y | FAIL(2) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E5:过半样本套用问答框（2/3），未脱模版
| 呼鲁那格 | 3 | Y | - | - | - | Y | Y | FAIL(4) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词；无可让/条件词）
  - E3:运行时无条件化信号词（若/但凡/除非/一旦…）
  - E5:全部样本套用统一问答框（3/3），未脱模版
  -  E5WARN:开头雷同 呼鲁那格会如何回
| 加里俄斯 | 2 | - | - | - | Y | Y | Y | FAIL(4) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:过半样本套用问答框（1/2），未脱模版
| 金达 | 3 | Y | - | - | Y | Y | Y | FAIL(3) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:全部样本套用统一问答框（3/3），未脱模版
  -  E5WARN:开头雷同 金达如何回应‘你
| 卡拉蒂尔德 | 3 | Y | - | Y | Y | Y | Y | FAIL(2) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E5:全部样本套用统一问答框（3/3），未脱模版
  -  E5WARN:开头雷同 卡拉蒂尔德如何回
| 卡拉多格 | 3 | Y | - | - | Y | Y | - | FAIL(2) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  -  E5WARN:开头雷同 卡拉多格会如何对 E5WARN:含问答框 1/3
| 科林 | 3 | Y | - | - | - | Y | Y | FAIL(4) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词；无可让/条件词）
  - E3:运行时无条件化信号词（若/但凡/除非/一旦…）
  - E5:全部样本套用统一问答框（3/3），未脱模版
| 克洛托耳 | 2 | - | - | - | Y | Y | - | FAIL(3) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
| 拉盖娅 | 4 | Y | Y | Y | Y | Y | - | PASS |
| 朗瓦德 | 2 | - | - | - | Y | Y | Y | FAIL(4) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:过半样本套用问答框（1/2），未脱模版
| 卢孔 | 2 | - | - | - | Y | Y | Y | FAIL(4) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:过半样本套用问答框（1/2），未脱模版
| 卢伊汉 | 2 | - | - | - | Y | Y | - | FAIL(3) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  -  M1WARN:疑似纯台词样本(引文占比 0.71)
| 罗兰 | 3 | Y | - | - | Y | Y | Y | FAIL(3) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:过半样本套用问答框（2/3），未脱模版
| 曼忒俄斯 | 2 | - | - | - | - | Y | Y | FAIL(5) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词；无可让/条件词）
  - E3:运行时无条件化信号词（若/但凡/除非/一旦…）
  - E5:过半样本套用问答框（1/2），未脱模版
| 毛蕾阿斯 | 3 | Y | - | - | Y | Y | Y | FAIL(3) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:全部样本套用统一问答框（3/3），未脱模版
  -  E5WARN:开头雷同 毛蕾阿斯如何回应
| 梅利迪尔 | 2 | - | - | - | - | Y | - | FAIL(4) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词；无可让/条件词）
  - E3:运行时无条件化信号词（若/但凡/除非/一旦…）
| 蒙楚格 | 13 | - | - | Y | Y | Y | Y | FAIL(3) |
  - E1:样本x13 > 6（写精不凑）
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E5:全部样本套用统一问答框（13/13），未脱模版
  -  E5WARN:开头雷同 蒙楚格会如何回应
| 弥娜 | 3 | Y | - | Y | Y | Y | Y | FAIL(2) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E5:全部样本套用统一问答框（3/3），未脱模版
| 墨速宜 | 3 | Y | - | - | - | Y | Y | FAIL(4) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无可让/条件词）
  - E3:运行时无条件化信号词（若/但凡/除非/一旦…）
  - E5:全部样本套用统一问答框（3/3），未脱模版
  -  E5WARN:开头雷同 墨速宜如何回应‘
| 那得娅 | 4 | Y | Y | Y | Y | Y | - | PASS |
| 帕堤耳 | 2 | - | - | Y | Y | Y | Y | FAIL(3) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E5:全部样本套用统一问答框（2/2），未脱模版
| 彭同 | 2 | - | - | - | Y | Y | Y | FAIL(4) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:过半样本套用问答框（1/2），未脱模版
| 普林多尔 | 2 | - | - | - | Y | Y | - | FAIL(3) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
| 塞尔维克 | 3 | Y | - | - | Y | Y | Y | FAIL(3) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:过半样本套用问答框（2/3），未脱模版
| 斯瓦娜 | 3 | Y | - | - | Y | Y | Y | FAIL(3) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:全部样本套用统一问答框（3/3），未脱模版
  -  E5WARN:开头雷同 斯瓦娜如何回应‘
| 苏鲁克 | 3 | Y | - | - | Y | Y | Y | FAIL(3) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:全部样本套用统一问答框（3/3），未脱模版
| 塔拉斯 | 3 | Y | - | - | Y | Y | Y | FAIL(3) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:过半样本套用问答框（2/3），未脱模版
| 泰伊斯 | 2 | - | - | - | Y | Y | Y | FAIL(4) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:全部样本套用统一问答框（2/2），未脱模版
  -  E5WARN:开头雷同 泰伊斯会如何回应
| 忒斐罗斯 | 2 | - | - | - | Y | Y | Y | FAIL(4) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:全部样本套用统一问答框（2/2），未脱模版
  -  E5WARN:开头雷同 忒斐罗斯会如何回
| 突剌格 | 2 | - | - | - | Y | Y | Y | FAIL(4) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:全部样本套用统一问答框（2/2），未脱模版
  -  E5WARN:开头雷同 突剌格会如何回应
| 图里亚多斯 | 2 | - | - | - | Y | Y | Y | FAIL(4) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:过半样本套用问答框（1/2），未脱模版
  -  E5WARN:开头雷同 图里亚多斯会如何
| 托维尔 | 2 | - | - | - | Y | Y | Y | FAIL(4) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:全部样本套用统一问答框（2/2），未脱模版
  -  E5WARN:开头雷同 托维尔会如何回应
| 瓦尔坦 | 3 | Y | - | - | Y | Y | Y | FAIL(3) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:过半样本套用问答框（2/3），未脱模版
| 瓦舍尔基 | 2 | - | - | - | Y | Y | Y | FAIL(4) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:全部样本套用统一问答框（2/2），未脱模版
  -  E5WARN:开头雷同 瓦舍尔基会如何回
| 维杜尔 | 2 | - | - | - | Y | Y | Y | FAIL(4) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:全部样本套用统一问答框（2/2），未脱模版
  -  E5WARN:开头雷同 维杜尔会如何回应
| 温吉德 | 2 | - | - | - | Y | Y | Y | FAIL(4) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:过半样本套用问答框（1/2），未脱模版
| 文得利娅 | 3 | Y | - | Y | Y | Y | Y | FAIL(2) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E5:全部样本套用统一问答框（3/3），未脱模版
  -  E5WARN:开头雷同 文得利娅如何回应
| 乌尔玻斯 | 2 | - | - | - | Y | Y | Y | FAIL(4) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:全部样本套用统一问答框（2/2），未脱模版
  -  E5WARN:开头雷同 乌尔玻斯会如何回
| 西加 | 3 | Y | - | Y | Y | Y | Y | FAIL(2) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E5:过半样本套用问答框（2/3），未脱模版
  -  M1WARN:疑似纯台词样本(引文占比 0.71)
| 雅那 | 3 | Y | - | - | Y | Y | Y | FAIL(3) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:全部样本套用统一问答框（3/3），未脱模版
  -  E5WARN:开头雷同 雅那如何回应‘你 M1WARN:疑似纯台词样本(引文占比 0.8)
| 伊拉 | 3 | Y | - | - | Y | Y | Y | FAIL(3) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:全部样本套用统一问答框（3/3），未脱模版
| 伊亚拉斯 | 3 | Y | - | - | Y | Y | Y | FAIL(3) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:全部样本套用统一问答框（3/3），未脱模版
  -  E5WARN:开头雷同 伊亚拉斯会如何回
| 亦剌塔儿 | 3 | Y | - | Y | Y | Y | Y | FAIL(2) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E5:过半样本套用问答框（2/3），未脱模版
| 因加泰尔 | 2 | - | - | - | Y | Y | Y | FAIL(4) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:过半样本套用问答框（1/2），未脱模版
| 约里格 | 2 | - | - | - | Y | Y | Y | FAIL(4) |
  - E1:样本x2 < 3
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E2b:运行时缺张力（无硬底线词）
  - E5:全部样本套用统一问答框（2/2），未脱模版
  -  E5WARN:开头雷同 约里格会如何回应
| 泽洛西卡 | 3 | Y | - | Y | Y | Y | Y | FAIL(2) |
  - E2:tensionAxes 缺失或三轴(hardLine/negotiable/breachSwitch)未尽填
  - E5:全部样本套用统一问答框（3/3），未脱模版
  -  E5WARN:开头雷同 泽洛西卡如何回应

## 汇总: 全量  全部通过=False

## 判定说明
- E1/E2/E2b/E3/E4/E5 为硬 FAIL。E2b 是本门禁的关键：tensionAxes 只是作者侧清单，物化会丢弃它，故张力必须落实进运行时字段才算数。
- V2 不查 8 类、不凑锚词；模板化的「如何回应…」问答框直接被 E5 记为 FAIL。
- 用 -StrictExact 追加「>=1 条纯散文/决断叙事」的门槛，进一步断开背诵台词回路。