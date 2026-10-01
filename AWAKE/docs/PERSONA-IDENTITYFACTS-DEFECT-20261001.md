# `identityFacts`「已知亲属」截断缺陷（2026-10-01）

判据：`已知亲属：` 后按 `、` 切分的条目，**以功能字（是/的/在/以/与/和/对/把/被/从/为/而/则…）结尾**
⇒ 说明该亲属名是从正文里定长切出来的，被截在半句上。

| 组 | 卡数 | 含「已知亲属」 | **含截断条目** | 截断条目/总条目 |
|---|---|---|---|---|
| 好（已入库） | 76 | 0 | **0** | 0/0 |
| 坏（未跟踪） | 279 | 233 | **93** | 117/409 |

## 一、坏批次里含截断条目的卡（前 60）

| 卡片 | 「已知亲属」原文 |
|---|---|
| `乌赛伊尔_Usair_banu_qild_aserai` | 父亲泰伊斯是、母亲鲁玛的关、父亲的铁血训 |
| `乌里克_Urik_kuloving_sturgia` | 父亲奥列克是 |
| `亚米娜_Yamina_banu_habbab_aserai` | 族长哈珊的妻 |
| `伊克拉提娅_Icratia_maneolis_empire_w` | 族长维彭是一 |
| `伊德伦_Idrun_kuloving_sturgia` | 父亲对大公朗、父亲的强硬立 |
| `伊斯凡_Isvan_ormidoving_sturgia` | 族长约里格、丈夫以及两个、女儿扎韦列娜 |
| `伊翁娜_Jonna_vetranis_empire_s` | 丈夫萨特洛斯、丈夫在复杂 |
| `佐安娜_Zoana_osticos_empire_n` | 父亲卢孔的教、父亲领导的北 |
| `佐里卡_Zorika_isyaroving_sturgia` | 族长法芬、丈夫一同治理、丈夫法芬以凶 |
| `佐里娜_Zorina_togaroving_sturgia` | 族长维杜尔以、丈夫阿尔瓦尔 |
| `佩里克_Peric_dey_molarn_vlandia` | 族长辈对权力 |
| `兀那根_Unagen_tigrit_khuzait` | 族长呼鲁那格、父亲的影响下 |
| `兹拉特卡_Zlatka_isyaroving_sturgia` | 父亲加尔登、族长则是以残、族长法芬的统 |
| `列克_Lek_vagiroving_sturgia` | 族长戈敦与埃、父亲影响 |
| `利利扎_Lilizha_gundaroving_sturgia` | 族长则是现任 |
| `加尔登_Galden_isyaroving_sturgia` | 族长法芬的铁、女儿兹拉特卡 |
| `博剌特_Bolat_urkhunait_khuzait` | 父亲正致力于、父亲统治的暗 |
| `卡忒拉_Catella_elaches_empire_w` | 族长堤诺普斯、丈夫在政治角、丈夫管理着托 |
| `卡西农_Casinon_dionicos_empire_w` | 父亲克洛托耳、父亲在橡树林、父亲对加里俄 |
| `卡费德_Carfyd_fen_eingal_battania` | 族长阿拉德、族长那狡诈且、妻子贝阿塔格、女儿蒂尔谢 |
| `厄洛倪克斯_Eronyx_varros_empire_w` | 父亲阿庇斯是、父亲与加里俄 |
| `古兰_Guaran_fen_caernacht_battania` | 族长毛蕾阿斯、母亲阴暗且狡 |
| `哈坎_Haqan_banu_qild_aserai` | 父亲泰伊斯是 |
| `喀宋_Chason_neretzes_empire_n` | 族长彭同的亲、妻子厄奥狄西 |
| `埃尔塔_Erta_vagiroving_sturgia` | 族长戈敦的妻、丈夫戈敦作为 |
| `基莎_Kisha_ubroving_sturgia` | 族长托维尔、丈夫在战场上 |
| `塞因_Sein_fen_derngil_battania` | 族长埃尔贡、父亲的教导下 |
| `夏拉穆斯_Siaramus_fen_giall_battania` | 族长埃隆以疯 |
| `奥斯温_Osven_vagiroving_sturgia` | 族长戈敦与埃、父亲戈敦是一 |
| `妮温_Nywin_fen_derngil_battania` | 族长埃尔贡、丈夫更是先王、丈夫的意志 |
| `孛儿图_Bortu_urkhunait_khuzait` | 父亲蒙楚格一、父亲穷兵黩武、父亲与平息部 |
| `密米尔_Mimir_gundaroving_sturgia` | 父亲如何利用、母亲家族与护、父亲朗瓦德 |
| `察衮_Chagun_baltait_khuzait` | 族长亦剌塔儿、父亲的影响下、父亲管理迪纳 |
| `尼丰_Niphon_avlonos_empire_s` | 族长的脚步、妻子阿雷莉安、女儿能在未来 |
| `布丽甘_Brighan_fen_eingal_battania` | 族长阿拉德、丈夫过于温和 |
| `弥那耳维娜_Minarvina_sorados_empire_w` | 女儿墨伽里塔、丈夫以稳健著 |
| `得巴娜_Debana_mestricaros_empire_s` | 父亲的严苛教 |
| `德拉恰_Dracha_togaroving_sturgia` | 族长维杜尔、丈夫维杜尔以 |
| `拉桑_Lasand_dey_arromanc_vlandia` | 母亲卡拉蒂尔、母亲的严格教、母亲有时被认 |
| `拉瑙恩_Ranaon_fen_derngil_battania` | 族长埃尔贡、父亲的教导下 |
| `拉道古尔_Ladogual_fen_derngil_battania` | 族长埃尔贡对 |
| `欧特洛庇俄斯_Eutropios_pethros_empire_s` | 父亲帕堤耳和、母亲维里娜将 |
| `沃勒里克_Voleric_dey_rothad_vlandia` | 族长伯里康以 |
| `济拉_Dhila_banu_ruwaid_aserai` | 父亲阿卡尔是、父亲的阴影下、父亲忙于处理 |
| `特弥翁_Temion_leonipardes_empire_s` | 父亲法戎的影 |
| `玛南_Manan_banu_qild_aserai` | 族长泰伊斯、父亲的统治风 |
| `瓦密洛斯_Varmyros_maneolis_empire_w` | 父亲那种凶残、父亲维彭在政 |
| `瓦弥那_Vamina_serapides_empire_n` | 父亲在政治上 |
| `祖艾德_Zuad_banu_atij_aserai` | 父亲塔拉斯是、父亲的熏陶下 |
| `祖莱卡_Zulaika_banu_sarmal_aserai` | 父亲奥赞的雄、父亲那种大张 |
| `福斯托耳_Phostor_lonalion_empire_w` | 父亲得斯波里、父亲严苛且多 |
| `福里姆_Forim_kostoroving_sturgia` | 族长罗兰的儿 |
| `纳延泰_Nayantai_khergit_khuzait` | 族长墨速宜、母亲的步伐 |
| `维托米拉_Vitomira_vezhoving_sturgia` | 族长瓦舍尔基、丈夫以铁腕和 |
| `罗达拉克_Rodarac_fen_caernacht_battania` | 族长的妻子毛、儿子古兰 |
| `肖娜格_Seonag_fen_morcar_battania` | 族长普林多尔、族长以凶残著 |
| `芝诺_Zeno_leonipardes_empire_s` | 父亲法戎的教 |
| `苏凯娜_Sukayna_banu_sarmal_aserai` | 族长奥赞的带、族长奥赞野心 |
| `苏哈娜_Sulhana_banu_sarmal_aserai` | 族长奥赞的女、父亲奥赞是一、父亲野心背后 |
| `苏娜_Suna_banu_habbab_aserai` | 父亲的慈悲无 |

## 二、好批次里含截断条目的卡（0 张）

| 卡片 | 「已知亲属」原文 |
|---|---|
