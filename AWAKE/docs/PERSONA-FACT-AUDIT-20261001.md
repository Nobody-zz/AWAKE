# 角色卡事实主张审计（对 sage 关系图）

- 基准：`bannerlord.db`（v1.3.15 快照）`bannerlord_heroes` 的 spouse / father / mother
- 被审：355 张 `.persona.json`

## A. 无法解析的 2 张 —— 官方本地化是否真有这个中文名

| 卡片 | displayName | 卡片声称的英文名 | sage 里同名英雄 | 该英雄官方中文名 |
|---|---|---|---|---|
| `古速坎_gusukan_oburit_khuzait` | 古速坎 | gusukan | commander_15(en=Gusukan, cn=古速坎); lord_K9_l(en=Gusukan, cn=古速坎) | |
| `埃索斯_Aesos_serapides_empire_n` | 埃索斯 | Aesos | commander_23(en=Aesos, cn=埃索斯); lord_NE9_s(en=Aesos, cn=埃索斯) | |

对照：全部 355 张里，sage 侧查不到官方中文名的真身共有 **473** 个。

## B. 亲属主张 vs sage 关系图

| 项 | 数 |
|---|---|
| 有 sage 亲属关系的卡 | 296 |
| identityFacts 提到全部 sage 亲属 | 139 |
| **一个都没提到** | **113** |
| 提到部分 | 44 |

### 至少漏了一个 sage 亲属的卡（前 40）

| 卡片 | sage 亲属 | 状态 |
|---|---|---|
| `亚恰娜_Yachana_vezhoving_sturgia` | 父=Ratagost(拉塔戈斯特) | 全未提及 |
| `亚里翁_Arion_osticos_empire_n` | 父=Arcor(阿科耳) | 全未提及 |
| `伊兹登卡_Izdenka_ormidoving_sturgia` | 配偶=Svedorn(斯维多恩) | 全未提及 |
| `伊德伦_Idrun_kuloving_sturgia` | 父=Olek(奥列克) | 全未提及 |
| `伊斯凡_Isvan_ormidoving_sturgia` | 配偶=Valkava(瓦尔卡娃) | 全未提及 |
| `佐安娜_Zoana_osticos_empire_n` | 父=Lucon(卢孔); 母=Zerosica(泽洛西卡) | 部分未提及 |
| `俄布戎_Obron_hongeros_empire_s` | 配偶=Tristania(崔斯坦尼娅) | 全未提及 |
| `兀那根_Unagen_tigrit_khuzait` | 父=Hurunag(呼鲁那格); 母=Chambui(察木步亦) | 部分未提及 |
| `列克_Lek_vagiroving_sturgia` | 父=Godun(戈敦); 母=Erta(埃尔塔) | 部分未提及 |
| `利利扎_Lilizha_gundaroving_sturgia` | 父=Vidar(维达尔) | 全未提及 |
| `博万_Bovan_vezhoving_sturgia` | 父=Ratagost(拉塔戈斯特) | 全未提及 |
| `博剌特_Bolat_urkhunait_khuzait` | 父=Monchug(蒙楚格); 母=Anat(阿那特) | 全未提及 |
| `卡拉蒂尔德_calatild_dey_arromanc_vlandia` | 配偶=Unthery(恩泰里) | 全未提及 |
| `卡欣_Qahin_banu_sarmal_aserai` | 配偶=Sukayna(苏凯娜) | 全未提及 |
| `卡西农_Casinon_dionicos_empire_w` | 父=Crotor(克洛托耳); 母=Lysica(吕西卡) | 部分未提及 |
| `卢伊汉_luichan_fen_penraic_battania` | 配偶=Eabyr(埃比尔) | 全未提及 |
| `卢康_Lucand_dey_gunric_vlandia` | 配偶=Bertliana(贝特莉安娜) | 全未提及 |
| `厄吕斯_Elys_dey_meroc_vlandia` | 父=Derthert(德泰尔); 母=Philenora(菲利诺拉) | 全未提及 |
| `厄洛倪克斯_Eronyx_varros_empire_w` | 父=Apys(阿庇斯); 母=Melkea(墨尔刻娅) | 部分未提及 |
| `古东赫尔达_Gudonhelda_dey_molarn_vlandia` | 父=Hecard(赫卡尔); 母=Adaltrud(阿达特鲁德) | 部分未提及 |
| `古兰_Guaran_fen_caernacht_battania` | 父=Rodarac(罗达拉克); 母=Maireas(毛蕾阿斯) | 部分未提及 |
| `哈利娅_Chalia_neretzes_empire_n` | 父=Penton(彭同); 母=Hylasiana(许拉西娅娜) | 部分未提及 |
| `哈坎_Haqan_banu_qild_aserai` | 父=Tais(泰伊斯); 母=Ruma(鲁玛) | 部分未提及 |
| `喀里斯_Karith_banu_qild_aserai` | 配偶=Judira(朱蒂拉) | 全未提及 |
| `因格特鲁德_Ingeltrud_dey_valant_vlandia` | 配偶=Varmund(瓦尔蒙德) | 全未提及 |
| `因贡德_Ingunde_dey_molarn_vlandia` | 父=Hecard(赫卡尔); 母=Adaltrud(阿达特鲁德) | 部分未提及 |
| `埃尔杜兰_Erdurand_dey_meroc_vlandia` | 父=Derthert(德泰尔); 母=Philenora(菲利诺拉) | 全未提及 |
| `埃尔贡_ergeon_fen_derngil_battania` | 配偶=Nywin(妮温) | 全未提及 |
| `埃莉德_Eilidh_fen_uvain_battania` | 父=Melidir(梅利迪尔); 母=Alcaea(阿尔凯娅) | 部分未提及 |
| `埃蒂尔德_Elthild_dey_tihr_vlandia` | 配偶=Aldric(阿尔德里克) | 全未提及 |
| `埃隆_aeron_fen_giall_battania` | 配偶=Liasin(莉亚欣) | 全未提及 |
| `堤诺普斯_Tynops_elaches_empire_w` | 配偶=Catella(卡忒拉) | 全未提及 |
| `塔洛斯_Tharos_vizartos_empire_s` | 配偶=Silvina(西尔维娜) | 全未提及 |
| `塔里克_Tariq_banu_sarran_aserai` | 父=Adram(阿德拉姆); 母=Maraa(马拉) | 部分未提及 |
| `塞兰冬_Serandon_avlonos_empire_s` | 配偶=Megethia(墨革提娅) | 全未提及 |
| `墨剌_Mela_oburit_khuzait` | 父=Gusukan(古速坎); 母=Sevin(辞温) | 部分未提及 |
| `墨里托耳_Meritor_dionicos_empire_w` | 父=Crotor(克洛托耳); 母=Lysica(吕西卡) | 部分未提及 |
| `奥列克_olek_kuloving_sturgia` | 父=Olek the Old(“年长的”奥列克) | 全未提及 |
| `奥多芙蕾德_Odofled_dey_arromanc_vlandia` | 父=Unthery(恩泰里); 母=Calatild(卡拉蒂尔德) | 全未提及 |
| `奥斯温_Osven_vagiroving_sturgia` | 父=Godun(戈敦); 母=Erta(埃尔塔) | 部分未提及 |

## C. identityFacts 文本拼接缺陷

可疑样本共 **88** 张**（此判据是启发式，需人工确认）**：

| 卡片 | identityFacts 原文 |
|---|---|
| `乌尔玻斯_ulbos_pethros_empire_s` | 南帝国珀特洛斯家族成员。妻弥娜，育有女儿卡绪雷娅与科拉谟柏娅。掌家族田产庄园，不涉元老院政治。无官方生平正文。 |
| `乌赛伊尔_Usair_banu_qild_aserai` | 阿塞莱吉勒德家族成员，属子女。已知亲属：父亲泰伊斯是、母亲鲁玛的关、父亲的铁血训。官方中文译名为「乌赛伊尔」。 |
| `乌里克_Urik_kuloving_sturgia` | 斯特吉亚库洛夫家族成员，属子女。已知亲属：父亲奥列克是。官方中文译名为「乌里克」。 |
| `亚米娜_Yamina_banu_habbab_aserai` | 阿塞莱哈巴卜家族成员，属家眷（配偶）。已知亲属：族长哈珊的妻。官方中文译名为「亚米娜」。 |
| `伊克拉提娅_Icratia_maneolis_empire_w` | 西帝国马涅俄利斯家族成员，属亲族部属。已知亲属：族长维彭是一。官方中文译名为「伊克拉提娅」。 |
| `伊德伦_Idrun_kuloving_sturgia` | 斯特吉亚库洛夫家族成员，属子女。已知亲属：父亲对大公朗、父亲的强硬立。官方中文译名为「伊德伦」。 |
| `伊斯凡_Isvan_ormidoving_sturgia` | 斯特吉亚奥米多夫家族成员，属亲族部属。已知亲属：族长约里格、丈夫以及两个、女儿扎韦列娜。官方中文译名为「伊斯凡」。 |
| `伊翁娜_Jonna_vetranis_empire_s` | 南帝国维特然尼斯家族成员，属家眷（配偶）。已知亲属：丈夫萨特洛斯、丈夫在复杂。官方中文译名为「伊翁娜」。 |
| `佐安娜_Zoana_osticos_empire_n` | 北帝国俄斯提科斯家族成员，属子女。已知亲属：父亲卢孔的教、父亲领导的北。官方中文译名为「佐安娜」。 |
| `佐里卡_Zorika_isyaroving_sturgia` | 斯特吉亚伊夏罗夫家族成员，属家眷（配偶）。已知亲属：族长法芬、丈夫一同治理、丈夫法芬以凶。官方中文译名为「佐里卡」。 |
| `佐里娜_Zorina_togaroving_sturgia` | 斯特吉亚托加罗夫家族成员，属亲族部属。已知亲属：族长维杜尔以、丈夫阿尔瓦尔。官方中文译名为「佐里娜」。 |
| `兀那根_Unagen_tigrit_khuzait` | 库赛特帖克力特家族成员，属子女。已知亲属：族长呼鲁那格、父亲的影响下。官方中文译名为「兀那根」。 |
| `克洛托耳_crotor_dionicos_empire_w` | 西帝国狄俄尼科斯家族族长，世代卫戍帝国西北防线，坐镇拉革塔。常年与边境掠袭队、斥候为伍，与首都元老院疏远。帝国内战中出于对战友的忠诚支持加里俄斯。妻吕西卡，弓马娴熟，丈夫在外时率兵守卫领地。 |
| `兹拉特卡_Zlatka_isyaroving_sturgia` | 斯特吉亚伊夏罗夫家族成员，属亲族部属。已知亲属：父亲加尔登、族长则是以残、族长法芬的统。官方中文译名为「兹拉特卡」。 |
| `列克_Lek_vagiroving_sturgia` | 斯特吉亚瓦吉罗夫家族成员，属子女。已知亲属：族长戈敦与埃、父亲影响。官方中文译名为「列克」。 |
| `利利扎_Lilizha_gundaroving_sturgia` | 斯特吉亚贡达罗夫家族成员，属亲族部属。已知亲属：族长则是现任。官方中文译名为「利利扎」。 |
| `加尔登_Galden_isyaroving_sturgia` | 斯特吉亚伊夏罗夫家族成员，属亲族部属。已知亲属：族长法芬的铁、女儿兹拉特卡。官方中文译名为「加尔登」。 |
| `博剌特_Bolat_urkhunait_khuzait` | 库赛特管理家族成员，属子女。已知亲属：父亲正致力于、父亲统治的暗。官方中文译名为「博剌特」。 |
| `卡忒拉_Catella_elaches_empire_w` | 西帝国厄拉刻斯家族成员，属家眷（配偶）。已知亲属：族长堤诺普斯、丈夫在政治角、丈夫管理着托。官方中文译名为「卡忒拉」。 |
| `卡西农_Casinon_dionicos_empire_w` | 西帝国狄俄尼科斯家族成员，属子女。已知亲属：父亲克洛托耳、父亲在橡树林、父亲对加里俄。官方中文译名为「卡西农」。 |
| `厄洛倪克斯_Eronyx_varros_empire_w` | 西帝国瓦罗斯家族成员，属子女。已知亲属：父亲阿庇斯是、父亲与加里俄。官方中文译名为「厄洛倪克斯」。 |
| `哈坎_Haqan_banu_qild_aserai` | 阿塞莱吉勒德家族成员，属子女。已知亲属：父亲泰伊斯是。官方中文译名为「哈坎」。 |
| `喀宋_Chason_neretzes_empire_n` | 北帝国涅雷采斯家族成员，属亲族部属。已知亲属：族长彭同的亲、妻子厄奥狄西。官方中文译名为「喀宋」。 |
| `埃尔塔_Erta_vagiroving_sturgia` | 斯特吉亚瓦吉罗夫家族成员，属家眷（配偶）。已知亲属：族长戈敦的妻、丈夫戈敦作为。官方中文译名为「埃尔塔」。 |
| `基莎_Kisha_ubroving_sturgia` | 斯特吉亚贵族乌布罗夫家族成员，属家眷（配偶）。已知亲属：族长托维尔、丈夫在战场上。官方中文译名为「基莎」。 |
| `塞因_Sein_fen_derngil_battania` | 巴旦尼亚登吉尔家族成员，属子女。已知亲属：族长埃尔贡、父亲的教导下。官方中文译名为「塞因」。 |
| `夏拉穆斯_Siaramus_fen_giall_battania` | 巴旦尼亚吉尔家族成员，属亲族部属。已知亲属：族长埃隆以疯。官方中文译名为「夏拉穆斯」。 |
| `奥列克_olek_kuloving_sturgia` | 斯特吉亚库洛夫家族现任族长，波耶贵族。家族是斯特吉亚最古老的家族之一。从未接受君主制观念，坚信波耶们才应该是自己土地的主人。其父“年长的奥列克”（前任库洛夫波耶）在潘德拉克战役中从阵前领兵，战死沙场；他袭位成为族长，对王公集权的怨恨始终未曾消减，如今年事已高，愈发不露衷曲。 |
| `奥斯温_Osven_vagiroving_sturgia` | 斯特吉亚瓦吉罗夫家族成员，属子女。已知亲属：族长戈敦与埃、父亲戈敦是一。官方中文译名为「奥斯温」。 |
| `妮温_Nywin_fen_derngil_battania` | 巴旦尼亚登吉尔家族成员，属家眷（配偶）。已知亲属：族长埃尔贡、丈夫更是先王、丈夫的意志。官方中文译名为「妮温」。 |
| `孛儿图_Bortu_urkhunait_khuzait` | 库赛特草原局势中守护家族成员，属子女。已知亲属：父亲蒙楚格一、父亲穷兵黩武、父亲与平息部。官方中文译名为「孛儿图」。 |
| `密米尔_Mimir_gundaroving_sturgia` | 斯特吉亚贡达罗夫家族成员，属子女。已知亲属：父亲如何利用、母亲家族与护、父亲朗瓦德。官方中文译名为「密米尔」。 |
| `察衮_Chagun_baltait_khuzait` | 库赛特巴鲁台特家族成员，属子女。已知亲属：族长亦剌塔儿、父亲的影响下、父亲管理迪纳。官方中文译名为「察衮」。 |
| `尼丰_Niphon_avlonos_empire_s` | 南帝国奥隆诺斯家族成员，属亲族部属。已知亲属：族长的脚步、妻子阿雷莉安、女儿能在未来。官方中文译名为「尼丰」。 |
| `布丽甘_Brighan_fen_eingal_battania` | 巴旦尼亚以确保家族成员，属家眷（配偶）。已知亲属：族长阿拉德、丈夫过于温和。官方中文译名为「布丽甘」。 |
| `布兰诺克_branoc_fen_morcar_battania` | 巴旦尼亚芬·莫卡尔氏族的子弟，族长普林多尔之子，其父以残暴闻名。妻肖娜格。骁勇的猎手与先锋战士，效忠至高王卡拉多格。不喜氏族议事之争，愿以战功立身。官方仅载其家世与亲缘，本卡依卡拉迪亚编年史补写其性格。 |
| `弥那耳维娜_Minarvina_sorados_empire_w` | 西帝国索拉多斯家族成员，属家眷（配偶）。已知亲属：女儿墨伽里塔、丈夫以稳健著。官方中文译名为「弥那耳维娜」。 |
| `得巴娜_Debana_mestricaros_empire_s` | 南帝国墨斯特里卡洛斯家族成员，属子女。已知亲属：父亲的严苛教。官方中文译名为「得巴娜」。 |
| `德拉恰_Dracha_togaroving_sturgia` | 斯特吉亚托加罗夫家族成员，属家眷（配偶）。已知亲属：族长维杜尔、丈夫维杜尔以。官方中文译名为「德拉恰」。 |
| `拉桑_Lasand_dey_arromanc_vlandia` | 瓦兰迪亚他深知家族成员，属子女。已知亲属：母亲卡拉蒂尔、母亲的严格教、母亲有时被认。官方中文译名为「拉桑」。 |
