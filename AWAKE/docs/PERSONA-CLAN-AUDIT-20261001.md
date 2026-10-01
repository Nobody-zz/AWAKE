# 角色卡 × sage 氏族（家族）对账（2026-10-01）

基准：`bannerlord_heroes.faction` → `bannerlord_clans.name` → CNs 本地化

- 卡总数 355（已跟踪 76 / 未跟踪 279）

## 一、总账

| 项 | 数 |
|---|---|
| 能解析出真身、且 sage 侧有氏族的卡 | 353 |
| **卡片自述的家族名 ≠ sage 氏族** | **61** |
| 卡片完全没提任何家族名 | 22 |
| 真身解析出但 sage 侧无氏族名 | 0 |
| 真身未解析出 | 2 |

## 二、全文都没提到 sage 氏族的卡（子串判断）

| 卡片 | 卡里出现的「…家族」片段 | sage 氏族(中) | sage 氏族(英) | clanId | 组 |
|---|---|---|---|---|---|
| `德泰尔_derthert_dey_meroc_vlandia` | — | **戴·梅罗克** | dey Meroc | `clan_vlandia_1` | 已入库 |
| `约里格_yorig_ormidoving_sturgia` | — | **奥米多夫** | Ormidoving | `clan_sturgia_4` | 已入库 |
| `阿德拉姆_adram_banu_sarran_aserai` |  阿塞莱豪门阿德拉姆/。以不越字面的手段为/与字面之间的空当里替/判例，琢磨下一桩能为/家，善在律法缝隙间为 | **巴努·萨兰** | Banu Sarran | `clan_aserai_2` | 已入库 |
| `乌海_Ukhai_banu_arbas_aserai` | 合的动荡时期，乌海在/管理职责 阿塞莱维护 | **巴努·阿巴斯** | Banu Arbas | `clan_aserai_5` | 未跟踪 |
| `乌赛伊尔_Usair_banu_qild_aserai` | 人。 对外，他继承了/法则中 阿塞莱吉勒德 | **巴努·吉勒德** | Banu Qild | `clan_aserai_3` | 未跟踪 |
| `利特加蒂斯_Lietgardis_dey_jelind_vlandia` | 加尔堡的女主人，她与/的支柱 瓦兰迪亚热兰/越的内政才干，成为了 | **戴·热兰** | dey Jelind | `clan_vlandia_9` | 未跟踪 |
| `努卡尔_Nuqar_banu_hulyan_aserai` | 吉德，但他更看重的是/悍将。 私下里，作为/我是努卡尔，作为/的过程 阿塞莱胡勒延/运用上也颇有造诣，是 | **巴努·胡勒延** | Banu Hulyan | `clan_aserai_1` | 未跟踪 |
| `博剌特_Bolat_urkhunait_khuzait` | 感到厌倦 库赛特管理 | **兀儿浑乃特** | Urkhunait | `clan_khuzait_1` | 未跟踪 |
| `卡费德_Carfyd_fen_eingal_battania` | 剑 巴旦尼亚埃因加尔/尊重。 私下里，作为/我是卡费德，作为/然不同，卡费德更像是 | **芬·埃因加尔** | fen Eingal | `clan_battania_6` | 未跟踪 |
| `卢康_Lucand_dey_gunric_vlandia` | 忠诚 瓦兰迪亚贡里克 | **戴·贡里克** | dey Gunric | `clan_vlandia_5` | 未跟踪 |
| `厄吕斯_Elys_dey_meroc_vlandia` | 境中耳濡目染，继承了/智慧 瓦兰迪亚梅罗克 | **戴·梅罗克** | dey Meroc | `clan_vlandia_1` | 未跟踪 |
| `古东赫尔达_Gudonhelda_dey_molarn_vlandia` | 了如何在权力的游戏和/生存 瓦兰迪亚莫拉恩 | **戴·莫拉恩** | dey Molarn | `clan_vlandia_8` | 未跟踪 |
| `古兰_Guaran_fen_caernacht_battania` | 力和战术头脑，成为了/要支柱 巴旦尼亚尽管/阿斯的影响，他在处理 | **芬·凯尔纳奇** | fen Caernacht | `clan_battania_8` | 未跟踪 |
| `古齐德_Ghuzid_banu_qaraz_aserai` | 上的卓越天赋，稳固了/我是古齐德，他的/的地位 阿塞莱稳固了/计算。 私下里，他的 | **巴努·卡拉兹** | Banu Qaraz | `clan_aserai_4` | 未跟踪 |
| `哈菲莎_Hafisa_banu_atij_aserai` | 管理之道 阿塞莱维护/美名。 私下里，她的 | **巴努·阿提吉** | Banu Atij | `clan_aserai_6` | 未跟踪 |
| `喀里斯_Karith_banu_qild_aserai` | 冷酷的人。 对外，受/斗的背景下，喀里斯在/生存本领 阿塞莱维护 | **巴努·吉勒德** | Banu Qild | `clan_aserai_3` | 未跟踪 |
| `因格特鲁德_Ingeltrud_dey_valant_vlandia` | 传统影响。 尽管她的/的地位 瓦兰迪亚瓦朗/，进一步巩固了自己在 | **戴·瓦朗** | dey Valant | `clan_vlandia_6` | 未跟踪 |
| `埃尔杜兰_Erdurand_dey_meroc_vlandia` | 不满 瓦兰迪亚梅罗克 | **戴·梅罗克** | dey Meroc | `clan_vlandia_1` | 未跟踪 |
| `埃莉德_Eilidh_fen_uvain_battania` | 濡目染 巴旦尼亚乌万 | **芬·乌万** | fen Uvain | `clan_battania_3` | 未跟踪 |
| `塔里克_Tariq_banu_sarran_aserai` | 虑者。 私下里，他的/过人天赋 阿塞莱萨兰 | **巴努·萨兰** | Banu Sarran | `clan_aserai_2` | 未跟踪 |
| `孛儿图_Bortu_urkhunait_khuzait` | 伤员。 私下里，他的/库赛特草原局势中守护 | **兀儿浑乃特** | Urkhunait | `clan_khuzait_1` | 未跟踪 |
| `安妮扎_Anidha_banu_hulyan_aserai` | 丹温吉德之女，她深受/是避开风险，优先考虑/的繁华宫廷中，见证了/统治者 阿塞莱胡勒延 | **巴努·胡勒延** | Banu Hulyan | `clan_aserai_1` | 未跟踪 |
| `察罕_Chaghan_urkhunait_khuzait` | 原上长大，背负着重振/精通骑术与战术，成为/量 库赛特背负着重振 | **兀儿浑乃特** | Urkhunait | `clan_khuzait_1` | 未跟踪 |
| `崔斯坦尼娅_Tristania_hongeros_empire_s` | 领 南帝国乱世中巩固 | **翁革洛斯** | Hongeros | `clan_empire_south_4` | 未跟踪 |
| `布丽甘_Brighan_fen_eingal_battania` | 于温和的决策，以确保/受损 巴旦尼亚以确保/维尔的妻子，她不仅是 | **芬·埃因加尔** | fen Eingal | `clan_battania_6` | 未跟踪 |
| `希姆拉_Shimra_banu_qaraz_aserai` | 域的深厚造诣，成为了/声望。 私下里，作为/海神针 阿塞莱卡拉兹 | **巴努·卡拉兹** | Banu Qaraz | `clan_aserai_4` | 未跟踪 |
| `恩泰里_Unthery_dey_arromanc_vlandia` | 名义上并非族长，却是/恩泰里则更多地扮演了/敬畏。 私下里，作为/色 瓦兰迪亚阿罗曼克 | **戴·阿罗曼克** | dey Arromanc | `clan_vlandia_3` | 未跟踪 |
| `托蒙德_Thomund_dey_arromanc_vlandia` | 实但冷酷的风格统治着/欲 瓦兰迪亚阿罗曼克/里的儿子，他自幼便在 | **戴·阿罗曼克** | dey Arromanc | `clan_vlandia_3` | 未跟踪 |
| `拉奇_Rath_fen_gruffendoc_battania` |  巴旦尼亚格鲁芬多克/马鲁纳斯，亲眼见证了 | **芬·格鲁芬多克** | fen Gruffendoc | `clan_battania_1` | 未跟踪 |
| `拉桑_Lasand_dey_arromanc_vlandia` | 他的母亲卡拉蒂尔德是/领主 瓦兰迪亚他深知 | **戴·阿罗曼克** | dey Arromanc | `clan_vlandia_3` | 未跟踪 |
| `拉瑙恩_Ranaon_fen_derngil_battania` | 我是拉瑙恩，尽管她的/斯的深宫之中，见证了/混乱 巴旦尼亚登吉尔 | **芬·登吉尔** | fen Derngil | `clan_battania_2` | 未跟踪 |
| `明瑟_Muinser_fen_gruffendoc_battania` |  巴旦尼亚格鲁芬多克/纳斯地区的领主，更是 | **芬·格鲁芬多克** | fen Gruffendoc | `clan_battania_1` | 未跟踪 |
| `朱蒂拉_Judira_banu_qild_aserai` | 土的权力游戏中，她的/我是朱蒂拉，作为/的影响 阿塞莱吉勒德 | **巴努·吉勒德** | Banu Qild | `clan_aserai_3` | 未跟踪 |
| `欧蕾萨_Euresa_palladios_empire_w` | 商与管理天赋，成为了/政的支柱 西帝国确保 | **帕拉狄俄斯** | Palladios | `clan_empire_west_9` | 未跟踪 |
| `法丽娜_Farina_banu_qaraz_aserai` | 固与繁荣 阿塞莱各大/长苏鲁克的妻子，她在 | **巴努·卡拉兹** | Banu Qaraz | `clan_aserai_4` | 未跟踪 |
| `法扎娜_Farzana_banu_atij_aserai` |  阿塞莱她不仅学习了/高效的领地管理来增强 | **巴努·阿提吉** | Banu Atij | `clan_aserai_6` | 未跟踪 |
| `济尤勒_Dhiyul_banu_hulyan_aserai` | 的挑战 阿塞莱胡勒延 | **巴努·胡勒延** | Banu Hulyan | `clan_aserai_1` | 未跟踪 |
| `涅雷达_Nereida_lonalion_empire_w` | 商贸往来 西帝国利翁/帝国贵族。 对外，受 | **罗那利翁** | Lonalion | `clan_empire_west_5` | 未跟踪 |
| `玛南_Manan_banu_qild_aserai` | 井有条 阿塞莱吉勒德 | **巴努·吉勒德** | Banu Qild | `clan_aserai_3` | 未跟踪 |
| `瓦尔蒙德_Varmund_dey_valant_vlandia` | 的地位 瓦兰迪亚瓦朗/的武力和战术头脑，在 | **戴·瓦朗** | dey Valant | `clan_vlandia_6` | 未跟踪 |
| `祖艾德_Zuad_banu_atij_aserai` | 族封地 阿塞莱阿提吉/能熟练操纵商队并管理 | **巴努·阿提吉** | Banu Atij | `clan_aserai_6` | 未跟踪 |
| `索法利娅_Sophalia_lonalion_empire_w` | 中的坚韧 西帝国利翁/贸易头脑，让她在维持 | **罗那利翁** | Lonalion | `clan_empire_west_5` | 未跟踪 |
| `罗达拉克_Rodarac_fen_caernacht_battania` | 领 巴旦尼亚凯尔纳奇 | **芬·凯尔纳奇** | fen Caernacht | `clan_battania_8` | 未跟踪 |
| `苏凯娜_Sukayna_banu_sarmal_aserai` | 往来中 阿塞莱始终将 | **巴努·萨马勒** | Banu Sarmal | `clan_aserai_7` | 未跟踪 |
| `茜尔文_Silvind_dey_arromanc_vlandia` | 藉 瓦兰迪亚阿罗曼克 | **戴·阿罗曼克** | dey Arromanc | `clan_vlandia_3` | 未跟踪 |
| `莉亚欣_Liasin_fen_giall_battania` | 比虚伪的荣誉更能保障/程支持 巴旦尼亚吉尔/险”的丈夫共同维系着 | **芬·吉尔** | fen Giall | `clan_battania_5` | 未跟踪 |
| `萨勒玛_Salma_banu_sarmal_aserai` | 族长奥赞的配偶，她是/臣 阿塞莱她不仅负责 | **巴努·萨马勒** | Banu Sarmal | `clan_aserai_7` | 未跟踪 |
| `萨姆扎_Thamza_banu_qaraz_aserai` | 和公正的声望，成为了/塞莱萨赛莎共同经营着/无私。 私下里，作为 | **巴努·卡拉兹** | Banu Qaraz | `clan_aserai_4` | 未跟踪 |
| `萨赛莎_Sasaitha_banu_qaraz_aserai` | 即将重启 阿塞莱稳固/里，作为族长苏鲁克的 | **巴努·卡拉兹** | Banu Qaraz | `clan_aserai_4` | 未跟踪 |
| `蒂尔谢_Taorse_fen_eingal_battania` | 感 巴旦尼亚埃因加尔/诈著称，但蒂尔谢却在/越年龄的稳重，在处理 | **芬·埃因加尔** | fen Eingal | `clan_battania_6` | 未跟踪 |
| `贝特莉安娜_Bertliana_dey_gunric_vlandia` | 影响 瓦兰迪亚贡里克/皮尔的族人，她自幼在 | **戴·贡里克** | dey Gunric | `clan_vlandia_5` | 未跟踪 |
| `贝阿塔格_Beathag_fen_eingal_battania` | 一 巴旦尼亚埃因加尔/形成了鲜明对比，成为 | **芬·埃因加尔** | fen Eingal | `clan_battania_6` | 未跟踪 |
| `里谢尔达_Richelda_dey_fortes_vlandia` | 影响 瓦兰迪亚佛特斯/风的环境中成长，深受 | **戴·佛特斯** | dey Fortes | `clan_vlandia_7` | 未跟踪 |
| `锡卡_Thiqa_banu_ruwaid_aserai` | 易经营 阿塞莱维持着/的妻子，她长期定居在 | **巴努·鲁瓦** | Banu Ruwaid | `clan_aserai_9` | 未跟踪 |
| `阿丽真_Alijin_urkhunait_khuzait` | 。 私下里，她成长于/特urkhunait | **兀儿浑乃特** | Urkhunait | `clan_khuzait_1` | 未跟踪 |
| `阿尔忒诺斯_Altenos_lonalion_empire_w` | 少壮派领主，他继承了/斯波里翁的亲族，他在/风格影响 西帝国利翁 | **罗那利翁** | Lonalion | `clan_empire_west_5` | 未跟踪 |
| `阿希莎_Ashisa_banu_arbas_aserai` | 统治者 阿塞莱阿巴斯/达成联盟，阿希莎作为/这位来自巴努·胡勒延 | **巴努·阿巴斯** | Banu Arbas | `clan_aserai_5` | 未跟踪 |
| `阿格娜拉_Agnala_lonalion_empire_w` | 化的熏陶 西帝国利翁/我是阿格娜拉，她的/有余。 私下里，她的 | **罗那利翁** | Lonalion | `clan_empire_west_5` | 未跟踪 |
| `阿达斯_Addas_banu_sarran_aserai` | 界处长大 阿塞莱尽管/，他的父亲阿德拉姆是 | **巴努·萨兰** | Banu Sarran | `clan_aserai_2` | 未跟踪 |
| `阿达特鲁德_Adaltrud_dey_molarn_vlandia` | 下秩序的掌控，确保了/地位 瓦兰迪亚确保了 | **戴·莫拉恩** | dey Molarn | `clan_vlandia_8` | 未跟踪 |

## 三、全文完全没出现「家族」二字的卡

| 卡片 | sage 氏族(中) | clanId | 组 |
|---|---|---|---|
| `伊拉_ira_pethros_empire_s` | 珀特洛斯 | `clan_empire_south_1` | 已入库 |
| `加里俄斯_garios_comnos_empire_w` | 科穆诺斯 | `clan_empire_west_1` | 已入库 |
| `卡拉多格_caladog_fen_gruffendoc_battania` | 芬·格鲁芬多克 | `clan_battania_1` | 已入库 |
| `呼鲁那格_hurunag_tigrit_khuzait` | 帖克力特 | `clan_khuzait_4` | 已入库 |
| `塔拉斯_talas_banu_atij_aserai` | 巴努·阿提吉 | `clan_aserai_6` | 已入库 |
| `奥赞_awdhan_banu_sarmal_aserai` | 巴努·萨马勒 | `clan_aserai_7` | 已入库 |
| `布兰诺克_branoc_fen_morcar_battania` | 芬·莫卡尔 | `clan_battania_7` | 已入库 |
| `德泰尔_derthert_dey_meroc_vlandia` | 戴·梅罗克 | `clan_vlandia_1` | 已入库 |
| `托维尔_tovir_ubroving_sturgia` | 乌布罗夫 | `clan_sturgia_8` | 已入库 |
| `毛蕾阿斯_maireas_fen_caernacht_battania` | 芬·凯尔纳奇 | `clan_battania_8` | 已入库 |
| `泰伊斯_tais_banu_qild_aserai` | 巴努·吉勒德 | `clan_aserai_3` | 已入库 |
| `瓦尔坦_vartin_dey_jelind_vlandia` | 戴·热兰 | `clan_vlandia_9` | 已入库 |
| `科林_corein_fen_gruffendoc_battania` | 芬·格鲁芬多克 | `clan_battania_1` | 已入库 |
| `突剌格_tulag_arkit_khuzait` | 阿契特 | `clan_khuzait_3` | 已入库 |
| `约里格_yorig_ormidoving_sturgia` | 奥米多夫 | `clan_sturgia_4` | 已入库 |
| `维杜尔_vyldur_togaroving_sturgia` | 托加罗夫 | `clan_sturgia_5` | 已入库 |
| `罗兰_rolan_kostoroving_sturgia` | 科斯托罗夫 | `clan_sturgia_9` | 已入库 |
| `菲利诺拉_philenora_dey_meroc_vlandia` | 戴·梅罗克 | `clan_vlandia_1` | 已入库 |
| `赫卡尔_hecard_dey_molarn_vlandia` | 戴·莫拉恩 | `clan_vlandia_8` | 已入库 |
| `金达_jinda_banu_hulyan_aserai` | 巴努·胡勒延 | `clan_aserai_1` | 已入库 |
| `阿克鲁木_akrum_harfit_khuzait` | 合儿必特 | `clan_khuzait_5` | 已入库 |
| `阿卡尔_aqar_banu_ruwaid_aserai` | 巴努·鲁瓦 | `clan_aserai_9` | 已入库 |

## 四、亲属在「全部文本字段」里是否被提到（比上一版更宽的口径）

| 项 | 数 |
|---|---|
| 有 sage 亲属关系的卡 | 296 |
| 全部亲属都被提到 | 184 |
| **一个亲属都没提到** | **64** |
| 提到部分 | 48 |

### 一个亲属都没提到的卡（前 50）

| 卡片 | sage 亲属 | 组 |
|---|---|---|
| `亚里翁_Arion_osticos_empire_n` | father=Arcor(阿科耳) | 未跟踪 |
| `伊兹登卡_Izdenka_ormidoving_sturgia` | spouse=Svedorn(斯维多恩) | 未跟踪 |
| `伊斯凡_Isvan_ormidoving_sturgia` | spouse=Valkava(瓦尔卡娃) | 未跟踪 |
| `俄布戎_Obron_hongeros_empire_s` | spouse=Tristania(崔斯坦尼娅) | 未跟踪 |
| `利利扎_Lilizha_gundaroving_sturgia` | father=Vidar(维达尔) | 未跟踪 |
| `博万_Bovan_vezhoving_sturgia` | father=Ratagost(拉塔戈斯特) | 未跟踪 |
| `卡拉蒂尔德_calatild_dey_arromanc_vlandia` | spouse=Unthery(恩泰里) | 已入库 |
| `卡欣_Qahin_banu_sarmal_aserai` | spouse=Sukayna(苏凯娜) | 未跟踪 |
| `卢伊汉_luichan_fen_penraic_battania` | spouse=Eabyr(埃比尔) | 已入库 |
| `卢康_Lucand_dey_gunric_vlandia` | spouse=Bertliana(贝特莉安娜) | 未跟踪 |
| `厄吕斯_Elys_dey_meroc_vlandia` | father=Derthert(德泰尔); mother=Philenora(菲利诺拉) | 未跟踪 |
| `喀里斯_Karith_banu_qild_aserai` | spouse=Judira(朱蒂拉) | 未跟踪 |
| `埃尔杜兰_Erdurand_dey_meroc_vlandia` | father=Derthert(德泰尔); mother=Philenora(菲利诺拉) | 未跟踪 |
| `埃尔贡_ergeon_fen_derngil_battania` | spouse=Nywin(妮温) | 已入库 |
| `埃隆_aeron_fen_giall_battania` | spouse=Liasin(莉亚欣) | 已入库 |
| `堤诺普斯_Tynops_elaches_empire_w` | spouse=Catella(卡忒拉) | 未跟踪 |
| `塔洛斯_Tharos_vizartos_empire_s` | spouse=Silvina(西尔维娜) | 未跟踪 |
| `塞兰冬_Serandon_avlonos_empire_s` | spouse=Megethia(墨革提娅) | 未跟踪 |
| `奥列克_olek_kuloving_sturgia` | father=Olek the Old(“年长的”奥列克) | 已入库 |
| `安德鲁塔_Andruta_gundaroving_sturgia` | father=Vidar(维达尔) | 未跟踪 |
| `尼丰_Niphon_avlonos_empire_s` | spouse=Areliana(阿雷莉安娜) | 未跟踪 |
| `巴拉诺耳_Baranor_julios_empire_s` | spouse=Valaria(瓦拉里娅) | 未跟踪 |
| `希姆拉_Shimra_banu_qaraz_aserai` | spouse=Ghuzid(古齐德) | 未跟踪 |
| `弗洛赖德_Floraidh_fen_morcar_battania` | father=Pryndor(普林多尔) | 未跟踪 |
| `得坎提娅_Decantia_osticos_empire_n` | spouse=Ascyron(阿斯居戎) | 未跟踪 |
| `得斯波里翁_Despórion_lonalion_empire_w` | spouse=Agnala(阿格娜拉) | 未跟踪 |
| `扎喀尼斯_Zachanis_julios_empire_s` | spouse=Zena(泽娜) | 未跟踪 |
| `扎韦列娜_Zaverena_ormidoving_sturgia` | father=Isvan(伊斯凡); mother=Valkava(瓦尔卡娃) | 未跟踪 |
| `拉奇_Rath_fen_gruffendoc_battania` | father=Muinser(明瑟) | 未跟踪 |
| `拉绍涅克_Lashonek_togaroving_sturgia` | spouse=Zheneva(热涅娃) | 未跟踪 |
| `普拉敦提娅_Pradentia_vatatzes_empire_n` | spouse=Maritzios(马利齐俄斯) | 未跟踪 |
| `梅利迪尔_melidir_fen_uvain_battania` | spouse=Alcaea(阿尔凯娅) | 已入库 |
| `梅根赫尔达_Megenhelda_dey_tihr_vlandia` | father=Aldric(阿尔德里克); mother=Elthild(埃蒂尔德) | 未跟踪 |
| `欧蕾萨_Euresa_palladios_empire_w` | spouse=Torvasis(托耳瓦西斯) | 未跟踪 |
| `毛蕾阿斯_maireas_fen_caernacht_battania` | spouse=Rodarac(罗达拉克) | 已入库 |
| `法拉里萨_Phalarisa_argoros_empire_n` | spouse=Belithor(柏利托耳) | 未跟踪 |
| `波庇利娅_Popilia_corenios_empire_w` | spouse=Ovagos(俄瓦戈斯) | 未跟踪 |
| `温吉德_unqid_banu_hulyan_aserai` | spouse=Jinda(金达) | 已入库 |
| `热涅娃_Zheneva_togaroving_sturgia` | spouse=Lashonek(拉绍涅克) | 未跟踪 |
| `瓦尔蒙德_Varmund_dey_valant_vlandia` | spouse=Ingeltrud(因格特鲁德) | 未跟踪 |
| `瓦拉里娅_Valaria_julios_empire_s` | spouse=Baranor(巴拉诺耳) | 未跟踪 |
| `米兰卡_Milanka_vezhoving_sturgia` | father=Ratagost(拉塔戈斯特) | 未跟踪 |
| `索法利娅_Sophalia_lonalion_empire_w` | spouse=Altenos(阿尔忒诺斯) | 未跟踪 |
| `约戎_Joron_leonipardes_empire_s` | spouse=Alympia(阿林匹娅) | 未跟踪 |
| `维涅兰达_Veneranda_argoros_empire_n` | spouse=Andros(安德洛斯) | 未跟踪 |
| `肖娜格_Seonag_fen_morcar_battania` | spouse=Branoc(布兰诺克) | 未跟踪 |
| `苏凯娜_Sukayna_banu_sarmal_aserai` | spouse=Qahin(卡欣) | 未跟踪 |
| `苏萨达_Susada_osticos_empire_n` | spouse=Arcor(阿科耳) | 未跟踪 |
| `萨拉提斯_Saratis_sorados_empire_w` | spouse=Minarvina(弥那耳维娜) | 未跟踪 |
| `萨特洛斯_Satros_vetranis_empire_s` | spouse=Jonna(伊翁娜) | 未跟踪 |
