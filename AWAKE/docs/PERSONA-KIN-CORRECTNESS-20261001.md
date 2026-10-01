# 角色卡亲属关系正确性审计（2026-10-01）

从卡正文抽取自称的「父/母/配偶」，与 sage `bannerlord_heroes` 的 father/mother/spouse 逐条比对。

- 卡总数 355（已跟踪 76 / 未跟踪 279）

| 判决 | 张数 | 其中已入库 |
|---|---|---|
| OK | 8 | 0 |
| no-declared-kin | 345 | 0 |
| no-hero | 2 | 0 |
| role-no-sage-mother | 1 | 0 |

## 一、亲属写错的卡（0 张）

| 卡片 | 角色 | 卡片写的 | sage 实际 | 真身 | 组 |
|---|---|---|---|---|---|

## 二、正文里没有任何自称亲属关系的卡（345 张，前 40）

| 卡片 | sage 亲属 | 组 |
|---|---|---|
| `亦剌塔儿_ilatar_baltait_khuzait` | spouse=惕伦 | 已入库 |
| `伊亚拉斯_iyalas_banu_arbas_aserai` | spouse=谢玛 | 已入库 |
| `伊拉_ira_pethros_empire_s` | mother=拉盖娅 | 已入库 |
| `伯里康_berican_dey_rothad_vlandia` | 无 | 已入库 |
| `俄洛斯_oros_mestricaros_empire_s` | spouse=雅忒娅 | 已入库 |
| `克洛托耳_crotor_dionicos_empire_w` | spouse=吕西卡 | 已入库 |
| `加里俄斯_garios_comnos_empire_w` | spouse=文得利娅 | 已入库 |
| `卡拉多格_caladog_fen_gruffendoc_battania` | 无 | 已入库 |
| `卡拉蒂尔德_calatild_dey_arromanc_vlandia` | spouse=恩泰里 | 已入库 |
| `卢伊汉_luichan_fen_penraic_battania` | spouse=埃比尔 | 已入库 |
| `卢孔_lucon_osticos_empire` | spouse=泽洛西卡 | 已入库 |
| `合努占_kanujan_koltit_khuzait` | spouse=锁合台 | 已入库 |
| `呼鲁那格_hurunag_tigrit_khuzait` | spouse=察木步亦 | 已入库 |
| `哈珊_hashan_banu_habbab_aserai` | spouse=亚米娜 | 已入库 |
| `因加泰尔_ingalther_dey_cortain_vlandia` | spouse=埃尔蓓 | 已入库 |
| `图里亚多斯_turiados_hongeros_empire_s` | spouse=查士丁娜 | 已入库 |
| `埃卡朗_ecarand_dey_folcun_vlandia` | 无 | 已入库 |
| `埃尔贡_ergeon_fen_derngil_battania` | spouse=妮温 | 已入库 |
| `埃隆_aeron_fen_giall_battania` | spouse=莉亚欣 | 已入库 |
| `塔拉斯_talas_banu_atij_aserai` | 无 | 已入库 |
| `塞尔维克_servic_dey_valant_vlandia` | spouse=阿尔维特 | 已入库 |
| `墨速宜_mesui_khergit_khuzait` | 无 | 已入库 |
| `奥列克_olek_kuloving_sturgia` | father=“年长的”奥列克 | 已入库 |
| `奥斯皮尔_ospir_dey_gunric_vlandia` | 无 | 已入库 |
| `奥赞_awdhan_banu_sarmal_aserai` | spouse=萨勒玛 | 已入库 |
| `布兰诺克_branoc_fen_morcar_battania` | father=普林多尔; spouse=肖娜格 | 已入库 |
| `帕堤耳_patyr_pethros_empire_s` | spouse=维里娜 | 已入库 |
| `弥娜_mina_pethros_empire_s` | spouse=乌尔玻斯 | 已入库 |
| `彭同_penton_neretzes_empire` | 无 | 已入库 |
| `德泰尔_derthert_dey_meroc_vlandia` | spouse=菲利诺拉 | 已入库 |
| `忒斐罗斯_thephilos_comnos_empire_w` | father=加里俄斯 | 已入库 |
| `戈敦_godun_vagiroving_sturgia` | spouse=埃尔塔 | 已入库 |
| `托维尔_tovir_ubroving_sturgia` | spouse=基莎 | 已入库 |
| `拉盖娅_rhagaea_pethros_empire_s` | 无 | 已入库 |
| `拔该_bagai_khergit_khuzait` | 无 | 已入库 |
| `文得利娅_vendelia_comnos_empire_w` | spouse=加里俄斯 | 已入库 |
| `斯瓦娜_svana_vagiroving_sturgia` | father=戈敦; mother=埃尔塔 | 已入库 |
| `普林多尔_pryndor_fen_morcar_battania` | 无 | 已入库 |
| `曼忒俄斯_manteos_argoros_empire` | spouse=斐诺里娅 | 已入库 |
| `梅利迪尔_melidir_fen_uvain_battania` | spouse=阿尔凯娅 | 已入库 |
