# 角色卡「家族归属」矩阵审计（2026-10-01）

对每张卡：卡片正文提到的所有已知氏族 vs sage 由 `faction` 推出的氏族。

- 已解析出真身的卡：353
- 已知氏族（sage）：95 个

| 判决 | 含义 | 张数 | 其中已入库 |
|---|---|---|---|
| OK-full | 官方全名出现（如「巴努·萨兰」） | **292** | 72 |
| OK-core | 只出现核心名（省了「巴努·/戴·/芬·」前缀） | **32** | 0 |
| WRONG | **卡里写的是另一个氏族** | **0** | 0 |
| NONE | 卡里没提任何已知氏族 | **29** | 3 |
| NO-SAGE-CLAN | sage 侧推不出氏族 | **2** | 1 |

## 一、写错氏族的卡（0 张）—— 这是真错误

| 卡片 | sage 说属于 | 卡里实际提到的氏族 | 组 |
|---|---|---|---|

## 二、只省了前缀的卡（32 张）—— 写法不一致，不是写错

| 卡片 | sage 官方名 | 卡里出现的形式 | 组 |
|---|---|---|---|
| `乌赛伊尔_Usair_banu_qild_aserai` | 巴努·吉勒德 | 吉勒德 | 未跟踪 |
| `利特加蒂斯_Lietgardis_dey_jelind_vlandia` | 戴·热兰 | 热兰 | 未跟踪 |
| `努卡尔_Nuqar_banu_hulyan_aserai` | 巴努·胡勒延 | 胡勒延 | 未跟踪 |
| `卡费德_Carfyd_fen_eingal_battania` | 芬·埃因加尔 | 埃因加尔 | 未跟踪 |
| `卢康_Lucand_dey_gunric_vlandia` | 戴·贡里克 | 贡里克 | 未跟踪 |
| `厄吕斯_Elys_dey_meroc_vlandia` | 戴·梅罗克 | 梅罗克 | 未跟踪 |
| `古东赫尔达_Gudonhelda_dey_molarn_vlandia` | 戴·莫拉恩 | 莫拉恩 | 未跟踪 |
| `因格特鲁德_Ingeltrud_dey_valant_vlandia` | 戴·瓦朗 | 瓦朗 | 未跟踪 |
| `埃尔杜兰_Erdurand_dey_meroc_vlandia` | 戴·梅罗克 | 梅罗克 | 未跟踪 |
| `埃莉德_Eilidh_fen_uvain_battania` | 芬·乌万 | 乌万 | 未跟踪 |
| `塔里克_Tariq_banu_sarran_aserai` | 巴努·萨兰 | 萨兰 | 未跟踪 |
| `安妮扎_Anidha_banu_hulyan_aserai` | 巴努·胡勒延 | 胡勒延 | 未跟踪 |
| `希姆拉_Shimra_banu_qaraz_aserai` | 巴努·卡拉兹 | 卡拉兹 | 未跟踪 |
| `恩泰里_Unthery_dey_arromanc_vlandia` | 戴·阿罗曼克 | 阿罗曼克 | 未跟踪 |
| `托蒙德_Thomund_dey_arromanc_vlandia` | 戴·阿罗曼克 | 阿罗曼克 | 未跟踪 |
| `拉奇_Rath_fen_gruffendoc_battania` | 芬·格鲁芬多克 | 格鲁芬多克 | 未跟踪 |
| `拉瑙恩_Ranaon_fen_derngil_battania` | 芬·登吉尔 | 登吉尔 | 未跟踪 |
| `明瑟_Muinser_fen_gruffendoc_battania` | 芬·格鲁芬多克 | 格鲁芬多克 | 未跟踪 |
| `朱蒂拉_Judira_banu_qild_aserai` | 巴努·吉勒德 | 吉勒德 | 未跟踪 |
| `济尤勒_Dhiyul_banu_hulyan_aserai` | 巴努·胡勒延 | 胡勒延 | 未跟踪 |
| `玛南_Manan_banu_qild_aserai` | 巴努·吉勒德 | 吉勒德 | 未跟踪 |
| `瓦尔蒙德_Varmund_dey_valant_vlandia` | 戴·瓦朗 | 瓦朗 | 未跟踪 |
| `祖艾德_Zuad_banu_atij_aserai` | 巴努·阿提吉 | 阿提吉 | 未跟踪 |
| `罗达拉克_Rodarac_fen_caernacht_battania` | 芬·凯尔纳奇 | 凯尔纳奇 | 未跟踪 |
| `茜尔文_Silvind_dey_arromanc_vlandia` | 戴·阿罗曼克 | 阿罗曼克 | 未跟踪 |
| `莉亚欣_Liasin_fen_giall_battania` | 芬·吉尔 | 吉尔 | 未跟踪 |
| `蒂尔谢_Taorse_fen_eingal_battania` | 芬·埃因加尔 | 埃因加尔 | 未跟踪 |
| `贝特莉安娜_Bertliana_dey_gunric_vlandia` | 戴·贡里克 | 贡里克 | 未跟踪 |
| `贝阿塔格_Beathag_fen_eingal_battania` | 芬·埃因加尔 | 埃因加尔 | 未跟踪 |
| `里谢尔达_Richelda_dey_fortes_vlandia` | 戴·佛特斯 | 佛特斯 | 未跟踪 |
| `阿希莎_Ashisa_banu_arbas_aserai` | 巴努·阿巴斯 | 阿巴斯 | 未跟踪 |
| `马拉_Maraa_banu_sarran_aserai` | 巴努·萨兰 | 萨兰 | 未跟踪 |

## 三、完全没提氏族的卡（29 张）

| 卡片 | sage 氏族 | 组 |
|---|---|---|
| `德泰尔_derthert_dey_meroc_vlandia` | 戴·梅罗克 | 已入库 |
| `约里格_yorig_ormidoving_sturgia` | 奥米多夫 | 已入库 |
| `阿德拉姆_adram_banu_sarran_aserai` | 巴努·萨兰 | 已入库 |
| `乌海_Ukhai_banu_arbas_aserai` | 巴努·阿巴斯 | 未跟踪 |
| `博剌特_Bolat_urkhunait_khuzait` | 兀儿浑乃特 | 未跟踪 |
| `古兰_Guaran_fen_caernacht_battania` | 芬·凯尔纳奇 | 未跟踪 |
| `古齐德_Ghuzid_banu_qaraz_aserai` | 巴努·卡拉兹 | 未跟踪 |
| `哈菲莎_Hafisa_banu_atij_aserai` | 巴努·阿提吉 | 未跟踪 |
| `喀里斯_Karith_banu_qild_aserai` | 巴努·吉勒德 | 未跟踪 |
| `孛儿图_Bortu_urkhunait_khuzait` | 兀儿浑乃特 | 未跟踪 |
| `察罕_Chaghan_urkhunait_khuzait` | 兀儿浑乃特 | 未跟踪 |
| `崔斯坦尼娅_Tristania_hongeros_empire_s` | 翁革洛斯 | 未跟踪 |
| `布丽甘_Brighan_fen_eingal_battania` | 芬·埃因加尔 | 未跟踪 |
| `拉桑_Lasand_dey_arromanc_vlandia` | 戴·阿罗曼克 | 未跟踪 |
| `欧蕾萨_Euresa_palladios_empire_w` | 帕拉狄俄斯 | 未跟踪 |
| `法丽娜_Farina_banu_qaraz_aserai` | 巴努·卡拉兹 | 未跟踪 |
| `法扎娜_Farzana_banu_atij_aserai` | 巴努·阿提吉 | 未跟踪 |
| `涅雷达_Nereida_lonalion_empire_w` | 罗那利翁 | 未跟踪 |
| `索法利娅_Sophalia_lonalion_empire_w` | 罗那利翁 | 未跟踪 |
| `苏凯娜_Sukayna_banu_sarmal_aserai` | 巴努·萨马勒 | 未跟踪 |
| `萨勒玛_Salma_banu_sarmal_aserai` | 巴努·萨马勒 | 未跟踪 |
| `萨姆扎_Thamza_banu_qaraz_aserai` | 巴努·卡拉兹 | 未跟踪 |
| `萨赛莎_Sasaitha_banu_qaraz_aserai` | 巴努·卡拉兹 | 未跟踪 |
| `锡卡_Thiqa_banu_ruwaid_aserai` | 巴努·鲁瓦 | 未跟踪 |
| `阿丽真_Alijin_urkhunait_khuzait` | 兀儿浑乃特 | 未跟踪 |
| `阿尔忒诺斯_Altenos_lonalion_empire_w` | 罗那利翁 | 未跟踪 |
| `阿格娜拉_Agnala_lonalion_empire_w` | 罗那利翁 | 未跟踪 |
| `阿达斯_Addas_banu_sarran_aserai` | 巴努·萨兰 | 未跟踪 |
| `阿达特鲁德_Adaltrud_dey_molarn_vlandia` | 戴·莫拉恩 | 未跟踪 |
