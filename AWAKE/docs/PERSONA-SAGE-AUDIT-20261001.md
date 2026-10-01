# 角色卡 × BannerlordSage 质量审计（2026-10-01）

- 基准库：`bannerlord.db`（sage 快照 `gameVersion = v1.3.15`，397 heroes / 95 clans / 272223 条本地化）
- 被审对象：`AWAKE/tools/persona-workbench/characters/*.persona.json`，共 **355** 张
- 口径：以 sage 为基准，检查「真身 id 能否解析」「官方中文名是否一致」「文化是否一致」

> ⚠️ sage 快照为 v1.3.15，本机安装版为 v1.4.8；引用读数前需注意版本差。

## 一、总账

| 项 | 数 |
|---|---|
| 卡片总数 | 355 |
| JSON 解析失败 | 0 |
| sourceDescription 已引用真身 | 316 |
| 经 sage 解析出真身（含按名回填） | 353 |
| **仍无法解析到任何真身** | **2** |
| 解析出的真身不在 sage 库内 | 0 |
| **官方中文名与卡片不一致** | **0** |
| **文化字段与 sage 不一致** | **0** |
| sage 侧查不到官方中文名 | 2 |

## 二、未引用真身、但已由 sage 回填出 id 的卡（建议补进 sourceDescription）

共 **37** 张：

| 卡片文件 | 卡片 id | 回填真身 id | 官方中文名 | 回填依据 |
|---|---|---|---|---|
| `于兰迪夫_Urundulf_dey_jelind_vlandia` | `calradia.Urundulf.vlandia` | `lord_V9_u` | 于兰迪夫 | displayName(cn) |
| `伊克拉提娅_Icratia_maneolis_empire_w` | `calradia.Icratia.empire.w` | `lord_WE8_c` | 伊克拉提娅 | displayName(cn) |
| `伊塔里娅_Itaria_prienicos_empire_s` | `calradia.Itaria.empire.s` | `lord_SE8_c` | 伊塔里娅 | displayName(cn) |
| `伊翁娜_Jonna_vetranis_empire_s` | `calradia.Jonna.empire.s` | `lord_SE9_s` | 伊翁娜 | displayName(cn) |
| `伯里康_berican_dey_rothad_vlandia` | `calradia.berican.vlandia` | `lord_V11_l` | 伯里康 | displayName(cn) |
| `加卢克_Galyk_ubroving_sturgia` | `calradia.Galyk.sturgia` | `lord_S8_u` | 加卢克 | displayName(cn) |
| `卡班_Qaban_banu_ruwaid_aserai` | `calradia.Qaban.aserai` | `lord_A9_u` | 卡班 | displayName(cn) |
| `古兰_Guaran_fen_caernacht_battania` | `calradia.Guaran.battania` | `lord_B8_c` | 古兰 | displayName(cn) |
| `埃朗达拉_Elendara_dey_rothad_vlandia` | `calradia.Elendara.vlandia` | `lord_V11_c1` | 埃朗达拉 | displayName(cn) |
| `墨剌_Mela_oburit_khuzait` | `calradia.Mela.khuzait` | `lord_K9_c2` | 墨剌 | displayName(cn) |
| `孛容察儿_Boronchar_yanserit_khuzait` | `calradia.Boronchar.khuzait` | `lord_K8_u` | 孛容察儿 | displayName(cn) |
| `帕加里俄斯_Pagarios_vetranis_empire_s` | `calradia.Pagarios.empire.s` | `lord_SE9_c1` | 帕加里俄斯 | displayName(cn) |
| `忒阿维索斯_Theavisos_vatatzes_empire_n` | `calradia.Theavisos.empire.n` | `lord_NE8_c1` | 忒阿维索斯 | displayName(cn) |
| `恰斯季米尔_Chastimir_kostoroving_sturgia` | `calradia.Chastimir.sturgia` | `lord_S9_u` | 恰斯季米尔 | displayName(cn) |
| `托耳瓦西斯_Torvasis_palladios_empire_w` | `calradia.Torvasis.empire.w` | `lord_WE9_u2` | 托耳瓦西斯 | displayName(cn) |
| `普拉敦提娅_Pradentia_vatatzes_empire_n` | `calradia.Pradentia.empire.n` | `lord_NE8_s` | 普拉敦提娅 | displayName(cn) |
| `欧蕾萨_Euresa_palladios_empire_w` | `calradia.Euresa.empire.w` | `lord_WE9_u` | 欧蕾萨 | displayName(cn) |
| `毛蕾阿斯_maireas_fen_caernacht_battania` | `calradia.maireas.battania` | `lord_B8_l` | 毛蕾阿斯 | displayName(cn) |
| `沃勒里克_Voleric_dey_rothad_vlandia` | `calradia.Voleric.vlandia` | `lord_V11_u` | 沃勒里克 | displayName(cn) |
| `波法利奥斯_Porfálios_serapides_empire_n` | `calradia.Porfálios.empire.north` | `lord_NE9_l` | 波法利奥斯 | displayName(cn) |
| `济拉_Dhila_banu_ruwaid_aserai` | `calradia.Dhila.aserai` | `lord_A9_c` | 济拉 | displayName(cn) |
| `涅斯堤斯_Nesthys_phalentes_empire_n` | `calradia.Nesthys.empire.n` | `lord_NE7_u` | 涅斯堤斯 | displayName(cn) |
| `温坎提俄斯_Vincântios_palladios_empire_w` | `calradia.Vincântios.empire.west` | `lord_WE9_l` | 温坎提俄斯 | displayName(cn) |
| `狄亚丝卡_Diasca_vetranis_empire_s` | `calradia.Diasca.empire.s` | `lord_SE9_c2` | 狄亚丝卡 | displayName(cn) |
| `瓦密洛斯_Varmyros_maneolis_empire_w` | `calradia.Varmyros.empire.w` | `lord_WE8_u` | 瓦密洛斯 | displayName(cn) |
| `瓦弥那_Vamina_serapides_empire_n` | `calradia.Vamina.empire.n` | `lord_NE9_d` | 瓦弥那 | displayName(cn) |
| `福里姆_Forim_kostoroving_sturgia` | `calradia.Forim.sturgia` | `lord_S9_c` | 福里姆 | displayName(cn) |
| `罗兰_rolan_kostoroving_sturgia` | `calradia.rolan.sturgia` | `lord_S9_l` | 罗兰 | displayName(cn) |
| `罗达拉克_Rodarac_fen_caernacht_battania` | `calradia.Rodarac.battania` | `lord_B8_s` | 罗达拉克 | displayName(cn) |
| `萨特洛斯_Satros_vetranis_empire_s` | `calradia.Satros.empire.south` | `lord_SE9_l` | 萨特洛斯 | displayName(cn) |
| `辞温_Sevin_oburit_khuzait` | `calradia.Sevin.khuzait` | `lord_K9_s` | 辞温 | displayName(cn) |
| `达希拉_Dakhila_kostoroving_sturgia` | `calradia.Dakhila.sturgia` | `lord_S9_m` | 达希拉 | displayName(cn) |
| `达甘尼克_Dagunic_dey_rothad_vlandia` | `calradia.Dagunic.vlandia` | `lord_V11_c2` | 达甘尼克 | displayName(cn) |
| `锡卡_Thiqa_banu_ruwaid_aserai` | `calradia.Thiqa.aserai` | `lord_A9_s` | 锡卡 | displayName(cn) |
| `阿勒涂_Altu_oburit_khuzait` | `calradia.Altu.khuzait` | `lord_K9_c1` | 阿勒涂 | displayName(cn) |
| `阿卡尔_aqar_banu_ruwaid_aserai` | `calradia.aqar.aserai` | `lord_A9_l` | 阿卡尔 | displayName(cn) |
| `马利齐俄斯_Marítzios_vatatzes_empire_n` | `calradia.Marítzios.empire.north` | `lord_NE8_l` | 马利齐俄斯 | displayName(cn) |

## 三、无法解析到任何真身的卡

共 **2** 张：

| 卡片文件 | 卡片 id | displayName | 卡片文化段 |
|---|---|---|---|
| `古速坎_gusukan_oburit_khuzait` | `calradia.gusukan.khuzait` | 古速坎 | khuzait |
| `埃索斯_Aesos_serapides_empire_n` | `calradia.Aesos.empire.n` | 埃索斯 | empire |

## 四、官方中文名与卡片 displayName 不一致

共 **0** 张：

| 卡片文件 | 卡片 displayName | sage 官方中文名 | 真身 id |
|---|---|---|---|

## 五、文化字段与 sage 不一致

共 **0** 张：

| 卡片文件 | 卡片文化段 | sage 文化 | 真身 id |
|---|---|---|---|
