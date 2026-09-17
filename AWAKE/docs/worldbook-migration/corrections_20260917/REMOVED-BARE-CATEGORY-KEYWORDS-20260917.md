# 留痕：A 项删掉的裸类别词（逐条原值）

甲方 2026-09-17 裁决「B+A」里的 **A（清数据）**：把 389 条聚落条目上挂的**裸类别词**去掉 —— 它们是「谁都能沾」的键，既不指向任何东西，又给「德里亚特·村庄」这类手写复合词留了坑（272 个村庄的泛问被它劫走）。

| 项 | 值 |
|---|---|
| 旧值（对拍基线） | `tools/_baseline/geo1-v12-runtime.json`（v12 冻结副本，sha256 `6f796dfe…`）|
| 新值（现挂） | `ModuleData/Worldbook/packages/calradia/runtime.json`（v13f，451 条）|
| keywords 有增删的条目 | 389 |
| 其中新增档（只在新包里） | 3：`awake:entry:geography.settlement-types-castle`、`awake:entry:geography.settlement-types-town`、`awake:entry:geography.settlement-types-village` |
| 删除词分布 | `城堡` 67 条；`城镇` 49 条；`德里亚特·村庄` 1 条；`村庄` 272 条 |

⚠️ 这一步**只动 keywords**：三条概念词条的 `summary` 是本轮另一笔改动（见报告），不在本表内。

## 逐条原值

| # | 条目 | 删掉 | 新增 | 旧 keywords（原值逐字） | 新 keywords |
|---:|---|---|---|---|---|
| 1 | `geography.castles-ab-comer-castle` | 城堡 | （无新增） | ["阿布·科梅尔堡", "Ab Comer Castle", "城堡", "阿布·科梅尔", "因韦斯", "castle_B1", "Ab Comer", "Inveth"] | ["阿布·科梅尔堡", "Ab Comer Castle", "阿布·科梅尔", "因韦斯", "castle_B1", "Ab Comer", "Inveth"] |
| 2 | `geography.castles-ain-baliq-castle` | 城堡 | （无新增） | ["艾因·巴力克堡", "Ain Baliq Castle", "城堡", "艾因·巴力克", "代尔·哈瓦", "castle_A3", "Ain Baliq", "Deir Hawa"] | ["艾因·巴力克堡", "Ain Baliq Castle", "艾因·巴力克", "代尔·哈瓦", "castle_A3", "Ain Baliq", "Deir Hawa"] |
| 3 | `geography.castles-akiser-castle` | 城堡 | （无新增） | ["阿契赛尔堡", "Akiser Castle", "城堡", "阿契赛尔", "喀木沙", "castle_K2", "Akiser", "Kamshar"] | ["阿契赛尔堡", "Akiser Castle", "阿契赛尔", "喀木沙", "castle_K2", "Akiser", "Kamshar"] |
| 4 | `geography.castles-aster-castle` | 城堡 | （无新增） | ["阿斯特堡", "Aster Castle", "城堡", "阿斯特", "伊姆拉赫", "castle_B7", "Aster", "Imlagh"] | ["阿斯特堡", "Aster Castle", "阿斯特", "伊姆拉赫", "castle_B7", "Aster", "Imlagh"] |
| 5 | `geography.castles-ataconia-castle` | 城堡 | （无新增） | ["阿塔科尼亚堡", "Ataconia Castle", "城堡", "阿塔科尼亚", "珀塔米斯", "castle_EN6", "Ataconia", "Potamis"] | ["阿塔科尼亚堡", "Ataconia Castle", "阿塔科尼亚", "珀塔米斯", "castle_EN6", "Ataconia", "Potamis"] |
| 6 | `geography.castles-atrion-castle` | 城堡 | （无新增） | ["阿特里翁堡", "Atrion Castle", "城堡", "阿特里翁", "马珊加拉", "castle_EN5", "Atrion", "Masangara"] | ["阿特里翁堡", "Atrion Castle", "阿特里翁", "马珊加拉", "castle_EN5", "Atrion", "Masangara"] |
| 7 | `geography.castles-barihal-castle` | 城堡 | （无新增） | ["巴里哈勒堡", "Barihal Castle", "城堡", "巴里哈勒", "瓦达尔", "castle_A9", "Barihal", "Wadar"] | ["巴里哈勒堡", "Barihal Castle", "巴里哈勒", "瓦达尔", "castle_A9", "Barihal", "Wadar"] |
| 8 | `geography.castles-caleus-castle` | 城堡 | （无新增） | ["卡琉斯堡", "Caleus Castle", "城堡", "卡琉斯", "德里亚特", "castle_V6", "Caleus", "Deriat"] | ["卡琉斯堡", "Caleus Castle", "卡琉斯", "德里亚特", "castle_V6", "Caleus", "Deriat"] |
| 9 | `geography.castles-chanopsis-castle` | 城堡 | （无新增） | ["卡诺普西斯堡", "Chanopsis Castle", "城堡", "卡诺普西斯", "波普西亚", "castle_ES8", "Chanopsis", "Popsia"] | ["卡诺普西斯堡", "Chanopsis Castle", "卡诺普西斯", "波普西亚", "castle_ES8", "Chanopsis", "Popsia"] |
| 10 | `geography.castles-corenia-castle` | 城堡 | （无新增） | ["科雷尼亚堡", "Corenia Castle", "城堡", "科雷尼亚", "墨塔基亚", "castle_ES2", "Corenia", "Metachia"] | ["科雷尼亚堡", "Corenia Castle", "科雷尼亚", "墨塔基亚", "castle_ES2", "Corenia", "Metachia"] |
| 11 | `geography.castles-dinar-castle` | 城堡 | （无新增） | ["迪纳尔堡", "Dinar Castle", "城堡", "迪纳尔", "喀拉哈力", "castle_K6", "Dinar", "Karahalli"] | ["迪纳尔堡", "Dinar Castle", "迪纳尔", "喀拉哈力", "castle_K6", "Dinar", "Karahalli"] |
| 12 | `geography.castles-drapand-castle` | 城堡 | （无新增） | ["德拉庞堡", "Drapand Castle", "城堡", "德拉庞", "瓦朗比", "castle_V3", "Drapand", "Valanby"] | ["德拉庞堡", "Drapand Castle", "德拉庞", "瓦朗比", "castle_V3", "Drapand", "Valanby"] |
| 13 | `geography.castles-druimmor-castle` | 城堡 | （无新增） | ["德鲁伊莫尔堡", "Druimmor Castle", "城堡", "德鲁伊莫尔", "托·梅利纳", "castle_B3", "Druimmor", "Tor Melina"] | ["德鲁伊莫尔堡", "Druimmor Castle", "德鲁伊莫尔", "托·梅利纳", "castle_B3", "Druimmor", "Tor Melina"] |
| 14 | `geography.castles-epinosa-castle` | 城堡 | （无新增） | ["厄毗诺萨堡", "Epinosa Castle", "城堡", "厄毗诺萨", "庞斯", "castle_EN7", "Epinosa", "Pons"] | ["厄毗诺萨堡", "Epinosa Castle", "厄毗诺萨", "庞斯", "castle_EN7", "Epinosa", "Pons"] |
| 15 | `geography.castles-erzenur-castle` | 城堡 | （无新增） | ["埃泽努尔堡", "Erzenur Castle", "城堡", "埃泽努尔", "格烈登", "castle_K8", "Erzenur", "Gereden"] | ["埃泽努尔堡", "Erzenur Castle", "埃泽努尔", "格烈登", "castle_K8", "Erzenur", "Gereden"] |
| 16 | `geography.castles-flintolg-castle` | 城堡 | （无新增） | ["弗林托格堡", "Flintolg Castle", "城堡", "弗林托格", "格林托尔", "castle_B6", "Flintolg", "Glintor"] | ["弗林托格堡", "Flintolg Castle", "弗林托格", "格林托尔", "castle_B6", "Flintolg", "Glintor"] |
| 17 | `geography.castles-gaos-castle` | 城堡 | （无新增） | ["伽俄斯堡", "Gaos Castle", "城堡", "伽俄斯", "忒密斯", "castle_EN4", "Gaos", "Themys"] | ["伽俄斯堡", "Gaos Castle", "伽俄斯", "忒密斯", "castle_EN4", "Gaos", "Themys"] |
| 18 | `geography.castles-garontor-castle` | 城堡 | （无新增） | ["加隆托堡", "Garontor Castle", "城堡", "加隆托", "吕西亚", "castle_EW1", "Garontor", "Lysia"] | ["加隆托堡", "Garontor Castle", "加隆托", "吕西亚", "castle_EW1", "Garontor", "Lysia"] |
| 19 | `geography.castles-gersegos-castle` | 城堡 | （无新增） | ["革耳塞戈斯堡", "Gersegos Castle", "城堡", "革耳塞戈斯", "瓦忒亚", "castle_EW8", "Gersegos", "Vathea"] | ["革耳塞戈斯堡", "Gersegos Castle", "革耳塞戈斯", "瓦忒亚", "castle_EW8", "Gersegos", "Vathea"] |
| 20 | `geography.castles-hakkun-castle` | 城堡 | （无新增） | ["哈坤堡", "Hakkun Castle", "城堡", "哈坤", "基拉兹", "castle_K3", "Hakkun", "Kiraz"] | ["哈坤堡", "Hakkun Castle", "哈坤", "基拉兹", "castle_K3", "Hakkun", "Kiraz"] |
| 21 | `geography.castles-hertogea-castle` | 城堡 | （无新增） | ["赫托该亚堡", "Hertogea Castle", "城堡", "赫托该亚", "尼得翁", "castle_EW6", "Hertogea", "Nideon"] | ["赫托该亚堡", "Hertogea Castle", "赫托该亚", "尼得翁", "castle_EW6", "Hertogea", "Nideon"] |
| 22 | `geography.castles-hongard-castle` | 城堡 | （无新增） | ["翁加尔堡", "Hongard Castle", "城堡", "翁加尔", "费顿", "castle_V2", "Hongard", "Ferton"] | ["翁加尔堡", "Hongard Castle", "翁加尔", "费顿", "castle_V2", "Hongard", "Ferton"] |
| 23 | `geography.castles-jamayeh-castle` | 城堡 | （无新增） | ["贾迈耶堡", "Jamayeh Castle", "城堡", "贾迈耶", "侯纳卜", "castle_A5", "Jamayeh", "Hunab"] | ["贾迈耶堡", "Jamayeh Castle", "贾迈耶", "侯纳卜", "castle_A5", "Jamayeh", "Hunab"] |
| 24 | `geography.castles-jogurys-castle` | 城堡 | （无新增） | ["约格律斯堡", "Jogurys Castle", "城堡", "约格律斯", "优纳利卡", "castle_ES7", "Jogurys", "Eunalica"] | ["约格律斯堡", "Jogurys Castle", "约格律斯", "优纳利卡", "castle_ES7", "Jogurys", "Eunalica"] |
| 25 | `geography.castles-kaysar-castle` | 城堡 | （无新增） | ["开撒尔堡", "Kaysar Castle", "城堡", "开撒尔", "帕亚木", "castle_K9", "Kaysar", "Payam"] | ["开撒尔堡", "Kaysar Castle", "开撒尔", "帕亚木", "castle_K9", "Kaysar", "Payam"] |
| 26 | `geography.castles-khimli-castle` | 城堡 | （无新增） | ["希木利堡", "Khimli Castle", "城堡", "希木利", "鄂木罗托克", "castle_K5", "Khimli", "Omrotok"] | ["希木利堡", "Khimli Castle", "希木利", "鄂木罗托克", "castle_K5", "Khimli", "Omrotok"] |
| 27 | `geography.castles-kranirog-castle` | 城堡 | （无新增） | ["克拉尼罗格堡", "Kranirog Castle", "城堡", "克拉尼罗格", "伊斯米尔科格", "castle_S4", "Kranirog", "Ismilkorg"] | ["克拉尼罗格堡", "Kranirog Castle", "克拉尼罗格", "伊斯米尔科格", "castle_S4", "Kranirog", "Ismilkorg"] |
| 28 | `geography.castles-lavenia-castle` | 城堡 | （无新增） | ["拉文尼亚堡", "Lavenia Castle", "城堡", "拉文尼亚", "厄忒弥萨", "castle_ES4", "Lavenia", "Ethemisa"] | ["拉文尼亚堡", "Lavenia Castle", "拉文尼亚", "厄忒弥萨", "castle_ES4", "Lavenia", "Ethemisa"] |
| 29 | `geography.castles-llanoc-hen-castle` | 城堡 | （无新增） | ["拉诺克·亨堡", "Llanoc Hen Castle", "城堡", "拉诺克·亨", "坎特雷克", "castle_B2", "Llanoc Hen", "Cantrec"] | ["拉诺克·亨堡", "Llanoc Hen Castle", "拉诺克·亨", "坎特雷克", "castle_B2", "Llanoc Hen", "Cantrec"] |
| 30 | `geography.castles-lochana-castle` | 城堡 | （无新增） | ["罗卡那堡", "Lochana Castle", "城堡", "罗卡那", "诺耳塔尼萨", "castle_EN2", "Lochana", "Nortanisa"] | ["罗卡那堡", "Lochana Castle", "罗卡那", "诺耳塔尼萨", "castle_EN2", "Lochana", "Nortanisa"] |
| 31 | `geography.castles-mazhadan-castle` | 城堡 | （无新增） | ["马扎丹堡", "Mazhadan Castle", "城堡", "马扎丹", "福林", "castle_S2", "Mazhadan", "Forin"] | ["马扎丹堡", "Mazhadan Castle", "马扎丹", "福林", "castle_S2", "Mazhadan", "Forin"] |
| 32 | `geography.castles-mecalovea-castle` | 城堡 | （无新增） | ["墨卡罗维亚堡", "Mecalovea Castle", "城堡", "墨卡罗维亚", "阿加尔蒙", "castle_EN9", "Mecalovea", "Agalmon"] | ["墨卡罗维亚堡", "Mecalovea Castle", "墨卡罗维亚", "阿加尔蒙", "castle_EN9", "Mecalovea", "Agalmon"] |
| 33 | `geography.castles-medeni-castle` | 城堡 | （无新增） | ["迈代尼堡", "Medeni Castle", "城堡", "迈代尼", "吉德纳尔", "castle_A4", "Medeni", "Qidnar"] | ["迈代尼堡", "Medeni Castle", "迈代尼", "吉德纳尔", "castle_A4", "Medeni", "Qidnar"] |
| 34 | `geography.castles-melion-castle` | 城堡 | （无新增） | ["墨利翁堡", "Melion Castle", "城堡", "墨利翁", "萨戈利那", "castle_ES3", "Melion", "Sagolina"] | ["墨利翁堡", "Melion Castle", "墨利翁", "萨戈利那", "castle_ES3", "Melion", "Sagolina"] |
| 35 | `geography.castles-morenia-castle` | 城堡 | （无新增） | ["摩雷尼亚堡", "Morenia Castle", "城堡", "摩雷尼亚", "阿特费尼亚", "castle_ES5", "Morenia", "Atphynia"] | ["摩雷尼亚堡", "Morenia Castle", "摩雷尼亚", "阿特费尼亚", "castle_ES5", "Morenia", "Atphynia"] |
| 36 | `geography.castles-nevyansk-castle` | 城堡 | （无新增） | ["涅维扬斯克堡", "Nevyansk Castle", "城堡", "涅夫扬斯克", "德宁", "castle_S3", "Nevyansk", "Dnin"] | ["涅维扬斯克堡", "Nevyansk Castle", "涅夫扬斯克", "德宁", "castle_S3", "Nevyansk", "Dnin"] |
| 37 | `geography.castles-odrysa-castle` | 城堡 | （无新增） | ["俄德律萨堡", "Odrysa Castle", "城堡", "俄德律萨", "恺拉", "castle_ES1", "Odrysa", "Caira"] | ["俄德律萨堡", "Odrysa Castle", "俄德律萨", "恺拉", "castle_ES1", "Odrysa", "Caira"] |
| 38 | `geography.castles-onica-castle` | 城堡 | （无新增） | ["俄尼卡堡", "Onica Castle", "城堡", "俄尼卡", "塔耳库提斯", "castle_EW3", "Onica", "Tarcutis"] | ["俄尼卡堡", "Onica Castle", "俄尼卡", "塔耳库提斯", "castle_EW3", "Onica", "Tarcutis"] |
| 39 | `geography.castles-oristocorys-castle` | 城堡 | （无新增） | ["俄里斯托科律斯堡", "Oristocorys Castle", "城堡", "俄里斯托科律斯", "厄尔凡尼亚", "castle_EW7", "Oristocorys", "Elvania"] | ["俄里斯托科律斯堡", "Oristocorys Castle", "俄里斯托科律斯", "厄尔凡尼亚", "castle_EW7", "Oristocorys", "Elvania"] |
| 40 | `geography.castles-ormanfard-castle` | 城堡 | （无新增） | ["奥曼法德堡", "Ormanfard Castle", "城堡", "奥曼法德", "castle_V4", "Ormanfard"] | ["奥曼法德堡", "Ormanfard Castle", "奥曼法德", "castle_V4", "Ormanfard"] |
| 41 | `geography.castles-ov-castle` | 城堡 | （无新增） | ["奥夫堡", "Ov Castle", "城堡", "奥夫", "费赫", "castle_S5", "Ov", "Ferkh"] | ["奥夫堡", "Ov Castle", "奥夫", "费赫", "castle_S5", "Ov", "Ferkh"] |
| 42 | `geography.castles-pendraic-castle` | 城堡 | （无新增） | ["潘德拉克堡", "Pendraic Castle", "城堡", "潘德拉克", "林杜恩", "castle_B4", "Pendraic", "Lindorn"] | ["潘德拉克堡", "Pendraic Castle", "潘德拉克", "林杜恩", "castle_B4", "Pendraic", "Lindorn"] |
| 43 | `geography.castles-rhemtoil-castle` | 城堡 | （无新增） | ["雷姆托伊尔堡", "Rhemtoil Castle", "城堡", "雷姆托伊尔", "克莱格·班", "castle_B5", "Rhemtoil", "Claig Ban"] | ["雷姆托伊尔堡", "Rhemtoil Castle", "雷姆托伊尔", "克莱格·班", "castle_B5", "Rhemtoil", "Claig Ban"] |
| 44 | `geography.castles-rhesos-castle` | 城堡 | （无新增） | ["雷索斯堡", "Rhesos Castle", "城堡", "雷索斯", "底俄帕利斯", "castle_EN3", "Rhesos", "Dyopalis"] | ["雷索斯堡", "Rhesos Castle", "雷索斯", "底俄帕利斯", "castle_EN3", "Rhesos", "Dyopalis"] |
| 45 | `geography.castles-sahel-castle` | 城堡 | （无新增） | ["萨赫勒堡", "Sahel Castle", "城堡", "萨赫勒", "阿斯麦特", "castle_A2", "Sahel", "Asmait"] | ["萨赫勒堡", "Sahel Castle", "萨赫勒", "阿斯麦特", "castle_A2", "Sahel", "Asmait"] |
| 46 | `geography.castles-sestadaim-castle` | 城堡 | （无新增） | ["塞斯塔代姆堡", "Sestadaim Castle", "城堡", "塞斯塔代姆", "阿密孔", "castle_ES6", "Sestadaim", "Amycon"] | ["塞斯塔代姆堡", "Sestadaim Castle", "塞斯塔代姆", "阿密孔", "castle_ES6", "Sestadaim", "Amycon"] |
| 47 | `geography.castles-shibal-zumr-castle` | 城堡 | （无新增） | ["希巴勒·祖姆尔堡", "Shibal Zumr Castle", "城堡", "希巴勒·祖姆尔", "拉迈萨", "castle_A6", "Shibal Zumr", "Lamesa"] | ["希巴勒·祖姆尔堡", "Shibal Zumr Castle", "希巴勒·祖姆尔", "拉迈萨", "castle_A6", "Shibal Zumr", "Lamesa"] |
| 48 | `geography.castles-simira-castle` | 城堡 | （无新增） | ["西米拉堡", "Simira Castle", "城堡", "西米拉", "柯希·阿吉克", "castle_K7", "Simira", "Kohi Ajik"] | ["西米拉堡", "Simira Castle", "西米拉", "柯希·阿吉克", "castle_K7", "Simira", "Kohi Ajik"] |
| 49 | `geography.castles-syratos-castle` | 城堡 | （无新增） | ["叙拉托斯堡", "Syratos Castle", "城堡", "叙拉托斯", "特美摩斯", "castle_EN8", "Syratos", "Tememos"] | ["叙拉托斯堡", "Syratos Castle", "叙拉托斯", "特美摩斯", "castle_EN8", "Syratos", "Tememos"] |
| 50 | `geography.castles-takor-castle` | 城堡 | （无新增） | ["塔科尔堡", "Takor Castle", "城堡", "塔科尔", "德沃鲁斯塔", "castle_S6", "Takor", "Dvorusta"] | ["塔科尔堡", "Takor Castle", "塔科尔", "德沃鲁斯塔", "castle_S6", "Takor", "Dvorusta"] |
| 51 | `geography.castles-talivel-castle` | 城堡 | （无新增） | ["塔利维尔堡", "Talivel Castle", "城堡", "塔利维尔", "罗德唐", "castle_V7", "Talivel", "Rodetan"] | ["塔利维尔堡", "Talivel Castle", "塔利维尔", "罗德唐", "castle_V7", "Talivel", "Rodetan"] |
| 52 | `geography.castles-tamnuh-castle` | 城堡 | （无新增） | ["坦姆努堡", "Tamnuh Castle", "城堡", "坦姆努", "库加", "castle_A8", "Tamnuh", "Kuqa"] | ["坦姆努堡", "Tamnuh Castle", "坦姆努", "库加", "castle_A8", "Tamnuh", "Kuqa"] |
| 53 | `geography.castles-tepes-castle` | 城堡 | （无新增） | ["泰佩斯堡", "Tepes Castle", "城堡", "泰佩斯", "库鲁卢克", "castle_K4", "Tepes", "Kuruluk"] | ["泰佩斯堡", "Tepes Castle", "泰佩斯", "库鲁卢克", "castle_K4", "Tepes", "Kuruluk"] |
| 54 | `geography.castles-thorios-castle` | 城堡 | （无新增） | ["托里俄斯堡", "Thorios Castle", "城堡", "托里俄斯", "柏耳贡", "castle_EW2", "Thorios", "Bergum"] | ["托里俄斯堡", "Thorios Castle", "托里俄斯", "柏耳贡", "castle_EW2", "Thorios", "Bergum"] |
| 55 | `geography.castles-thractorae-castle` | 城堡 | （无新增） | ["色雷刻托堡", "Thractorae Castle", "城堡", "色雷刻托", "伽玛耳丹", "castle_EW4", "Thractorae", "Gamardan"] | ["色雷刻托堡", "Thractorae Castle", "色雷刻托", "伽玛耳丹", "castle_EW4", "Thractorae", "Gamardan"] |
| 56 | `geography.castles-tirby-castle` | 城堡 | （无新增） | ["蒂尔比堡", "Tirby Castle", "城堡", "蒂尔比", "西岚达克", "castle_V5", "Tirby", "Sirindac"] | ["蒂尔比堡", "Tirby Castle", "蒂尔比", "西岚达克", "castle_V5", "Tirby", "Sirindac"] |
| 57 | `geography.castles-tubilis-castle` | 城堡 | （无新增） | ["突比力斯堡", "Tubilis Castle", "城堡", "突比力斯", "法纳卜", "castle_A1", "Tubilis", "Fanab"] | ["突比力斯堡", "Tubilis Castle", "突比力斯", "法纳卜", "castle_A1", "Tubilis", "Fanab"] |
| 58 | `geography.castles-uqba-castle` | 城堡 | （无新增） | ["乌格巴堡", "Uqba Castle", "城堡", "乌格巴", "本盖兹", "castle_A7", "Uqba", "Bunqaz"] | ["乌格巴堡", "Uqba Castle", "乌格巴", "本盖兹", "castle_A7", "Uqba", "Bunqaz"] |
| 59 | `geography.castles-urikskala-castle` | 城堡 | （无新增） | ["乌里克斯卡拉堡", "Urikskala Castle", "城堡", "乌里克斯卡拉", "阿洛夫", "castle_S7", "Urikskala", "Alov"] | ["乌里克斯卡拉堡", "Urikskala Castle", "乌里克斯卡拉", "阿洛夫", "castle_S7", "Urikskala", "Alov"] |
| 60 | `geography.castles-usanc-castle` | 城堡 | （无新增） | ["于桑克堡", "Usanc Castle", "城堡", "于桑克", "castle_V1", "Usanc"] | ["于桑克堡", "Usanc Castle", "于桑克", "castle_V1", "Usanc"] |
| 61 | `geography.castles-usek-castle` | 城堡 | （无新增） | ["乌赛克堡", "Usek Castle", "城堡", "乌赛克", "埃斯梅", "castle_K1", "Usek", "Esme"] | ["乌赛克堡", "Usek Castle", "乌赛克", "埃斯梅", "castle_K1", "Usek", "Esme"] |
| 62 | `geography.castles-ustokol-castle` | 城堡 | （无新增） | ["乌斯托科堡", "Ustokol Castle", "城堡", "乌斯托科", "哲米扬", "castle_S1", "Ustokol", "Zhemyan"] | ["乌斯托科堡", "Ustokol Castle", "乌斯托科", "哲米扬", "castle_S1", "Ustokol", "Zhemyan"] |
| 63 | `geography.castles-uthelaim-castle` | 城堡 | （无新增） | ["乌瑟莱姆堡", "Uthelaim Castle", "城堡", "乌瑟莱姆", "肖尔达斯", "castle_B8", "Uthelaim", "Seordas"] | ["乌瑟莱姆堡", "Uthelaim Castle", "乌瑟莱姆", "肖尔达斯", "castle_B8", "Uthelaim", "Seordas"] |
| 64 | `geography.castles-varagos-castle` | 城堡 | （无新增） | ["瓦拉戈斯堡", "Varagos Castle", "城堡", "瓦拉戈斯", "艾俄里亚", "castle_EN1", "Varagos", "Aeoria"] | ["瓦拉戈斯堡", "Varagos Castle", "瓦拉戈斯", "艾俄里亚", "castle_EN1", "Varagos", "Aeoria"] |
| 65 | `geography.castles-verecsand-castle` | 城堡 | （无新增） | ["韦雷克桑堡", "Verecsand Castle", "城堡", "韦雷克桑", "马林", "castle_V8", "Verecsand", "Marin"] | ["韦雷克桑堡", "Verecsand Castle", "韦雷克桑", "马林", "castle_V8", "Verecsand", "Marin"] |
| 66 | `geography.castles-veron-castle` | 城堡 | （无新增） | ["维戎堡", "Veron Castle", "城堡", "维戎", "戈勒林", "castle_EW5", "Veron", "Goleryn"] | ["维戎堡", "Veron Castle", "维戎", "戈勒林", "castle_EW5", "Veron", "Goleryn"] |
| 67 | `geography.castles-vladiv-castle` | 城堡 | （无新增） | ["弗拉基夫堡", "Vladiv Castle", "城堡", "弗拉基夫", "格拉夫斯特伦", "castle_S8", "Vladiv", "Glavstrom"] | ["弗拉基夫堡", "Vladiv Castle", "弗拉基夫", "格拉夫斯特伦", "castle_S8", "Vladiv", "Glavstrom"] |
| 68 | `geography.towns-akkalat` | 城镇 | （无新增） | ["阿克卡拉特", "Akkalat", "城镇", "town_K2"] | ["阿克卡拉特", "Akkalat", "town_K2"] |
| 69 | `geography.towns-amitatys` | 城镇 | （无新增） | ["阿弥塔堤斯", "Amitatys", "城镇", "town_EW5"] | ["阿弥塔堤斯", "Amitatys", "town_EW5"] |
| 70 | `geography.towns-amprela` | 城镇 | （无新增） | ["安普雷拉", "Amprela", "城镇", "town_EN6"] | ["安普雷拉", "Amprela", "town_EN6"] |
| 71 | `geography.towns-argoron` | 城镇 | （无新增） | ["阿耳戈隆", "Argoron", "城镇", "town_EN4"] | ["阿耳戈隆", "Argoron", "town_EN4"] |
| 72 | `geography.towns-askar` | 城镇 | （无新增） | ["阿斯凯尔", "Askar", "城镇", "town_A7"] | ["阿斯凯尔", "Askar", "town_A7"] |
| 73 | `geography.towns-balgard` | 城镇 | （无新增） | ["巴尔加德", "Balgard", "城镇", "town_S2"] | ["巴尔加德", "Balgard", "town_S2"] |
| 74 | `geography.towns-baltakhand` | 城镇 | （无新增） | ["巴尔塔罕", "Baltakhand", "城镇", "town_K1"] | ["巴尔塔罕", "Baltakhand", "town_K1"] |
| 75 | `geography.towns-car-banseth` | 城镇 | （无新增） | ["卡·班塞斯", "Car Banseth", "城镇", "town_B3"] | ["卡·班塞斯", "Car Banseth", "town_B3"] |
| 76 | `geography.towns-chaikand` | 城镇 | （无新增） | ["柴坎", "Chaikand", "城镇", "town_K5"] | ["柴坎", "Chaikand", "town_K5"] |
| 77 | `geography.towns-danustica` | 城镇 | （无新增） | ["达努斯提卡", "Danustica", "城镇", "town_ES1"] | ["达努斯提卡", "Danustica", "town_ES1"] |
| 78 | `geography.towns-diathma` | 城镇 | （无新增） | ["狄亚特马", "Diathma", "城镇", "town_EN2"] | ["狄亚特马", "Diathma", "town_EN2"] |
| 79 | `geography.towns-dunglanys` | 城镇 | （无新增） | ["邓格拉尼斯", "Dunglanys", "城镇", "town_B2"] | ["邓格拉尼斯", "Dunglanys", "town_B2"] |
| 80 | `geography.towns-epicrotea` | 城镇 | （无新增） | ["厄庇克洛忒亚", "Epicrotea", "城镇", "town_EN1"] | ["厄庇克洛忒亚", "Epicrotea", "town_EN1"] |
| 81 | `geography.towns-galend` | 城镇 | （无新增） | ["加伦", "Galend", "城镇", "town_V5"] | ["加伦", "Galend", "town_V5"] |
| 82 | `geography.towns-hubyar` | 城镇 | （无新增） | ["胡比亚", "Hubyar", "城镇", "town_A5"] | ["胡比亚", "Hubyar", "town_A5"] |
| 83 | `geography.towns-iyakis` | 城镇 | （无新增） | ["以亚基斯", "Iyakis", "城镇", "town_A3"] | ["以亚基斯", "Iyakis", "town_A3"] |
| 84 | `geography.towns-jaculan` | 城镇 | （无新增） | ["杰屈朗", "Jaculan", "城镇", "town_V6"] | ["杰屈朗", "Jaculan", "town_V6"] |
| 85 | `geography.towns-jalmarys` | 城镇 | （无新增） | ["贾尔马律斯", "Jalmarys", "城镇", "town_EW3"] | ["贾尔马律斯", "Jalmarys", "town_EW3"] |
| 86 | `geography.towns-lageta` | 城镇 | （无新增） | ["拉革塔", "Lageta", "城镇", "town_EW1"] | ["拉革塔", "Lageta", "town_EW1"] |
| 87 | `geography.towns-makeb` | 城镇 | （无新增） | ["马凯布", "Makeb", "城镇", "town_K3"] | ["马凯布", "Makeb", "town_K3"] |
| 88 | `geography.towns-marunath` | 城镇 | （无新增） | ["马鲁纳斯", "Marunath", "城镇", "town_B1"] | ["马鲁纳斯", "Marunath", "town_B1"] |
| 89 | `geography.towns-myzea` | 城镇 | （无新增） | ["密泽亚", "Myzea", "城镇", "town_EN5"] | ["密泽亚", "Myzea", "town_EN5"] |
| 90 | `geography.towns-ocs-hall` | 城镇 | （无新增） | ["奥克斯·霍尔", "Ocs Hall", "城镇", "town_V2"] | ["奥克斯·霍尔", "Ocs Hall", "town_V2"] |
| 91 | `geography.towns-odokh` | 城镇 | （无新增） | ["奥多赫", "Odokh", "城镇", "town_K6"] | ["奥多赫", "Odokh", "town_K6"] |
| 92 | `geography.towns-omor` | 城镇 | （无新增） | ["奥莫尔", "Omor", "城镇", "town_S3"] | ["奥莫尔", "Omor", "town_S3"] |
| 93 | `geography.towns-onira` | 城镇 | （无新增） | ["俄尼拉", "Onira", "城镇", "town_ES5"] | ["俄尼拉", "Onira", "town_ES5"] |
| 94 | `geography.towns-ortongard` | 城镇 | （无新增） | ["奥通加德", "Ortongard", "城镇", "town_K4"] | ["奥通加德", "Ortongard", "town_K4"] |
| 95 | `geography.towns-ortysia` | 城镇 | （无新增） | ["俄耳堤西亚", "Ortysia", "城镇", "town_EW4"] | ["俄耳堤西亚", "Ortysia", "town_EW4"] |
| 96 | `geography.towns-ostican` | 城镇 | （无新增） | ["奥斯蒂港", "Ostican", "城镇", "town_V8"] | ["奥斯蒂港", "Ostican", "town_V8"] |
| 97 | `geography.towns-pen-cannoc` | 城镇 | （无新增） | ["彭·坎诺克", "Pen Cannoc", "城镇", "town_B5"] | ["彭·坎诺克", "Pen Cannoc", "town_B5"] |
| 98 | `geography.towns-phycaon` | 城镇 | （无新增） | ["费卡翁", "Phycaon", "城镇", "town_ES6"] | ["费卡翁", "Phycaon", "town_ES6"] |
| 99 | `geography.towns-poros` | 城镇 | （无新增） | ["波罗斯", "Poros", "城镇", "town_ES3"] | ["波罗斯", "Poros", "town_ES3"] |
| 100 | `geography.towns-pravend` | 城镇 | （无新增） | ["帕拉汶德", "Pravend", "城镇", "town_V3", "巴拉维诺斯", "Paravenos"] | ["帕拉汶德", "Pravend", "town_V3", "巴拉维诺斯", "Paravenos"] |
| 101 | `geography.towns-qasira` | 城镇 | （无新增） | ["加西拉", "Qasira", "城镇", "town_A8"] | ["加西拉", "Qasira", "town_A8"] |
| 102 | `geography.towns-quyaz` | 城镇 | （无新增） | ["古亚兹", "Quyaz", "城镇", "town_A1"] | ["古亚兹", "Quyaz", "town_A1"] |
| 103 | `geography.towns-razih` | 城镇 | （无新增） | ["拉齐赫", "Razih", "城镇", "town_A4"] | ["拉齐赫", "Razih", "town_A4"] |
| 104 | `geography.towns-revyl` | 城镇 | （无新增） | ["雷维尔", "Revyl", "城镇", "town_S7"] | ["雷维尔", "Revyl", "town_S7"] |
| 105 | `geography.towns-rhotae` | 城镇 | （无新增） | ["洛泰", "Rhotae", "城镇", "town_EW6"] | ["洛泰", "Rhotae", "town_EW6"] |
| 106 | `geography.towns-rovalt` | 城镇 | （无新增） | ["罗瓦尔", "Rovalt", "城镇", "town_V9"] | ["罗瓦尔", "Rovalt", "town_V9"] |
| 107 | `geography.towns-sanala` | 城镇 | （无新增） | ["撒纳拉", "Sanala", "城镇", "town_A6"] | ["撒纳拉", "Sanala", "town_A6"] |
| 108 | `geography.towns-saneopa` | 城镇 | （无新增） | ["萨涅俄帕", "Saneopa", "城镇", "town_EN3"] | ["萨涅俄帕", "Saneopa", "town_EN3"] |
| 109 | `geography.towns-sargot` | 城镇 | （无新增） | ["萨哥特", "Sargot", "城镇", "town_V1"] | ["萨哥特", "Sargot", "town_V1"] |
| 110 | `geography.towns-seonon` | 城镇 | （无新增） | ["肖农", "Seonon", "城镇", "town_B4"] | ["肖农", "Seonon", "town_B4"] |
| 111 | `geography.towns-sibir` | 城镇 | （无新增） | ["西比尔", "Sibir", "城镇", "town_S6"] | ["西比尔", "Sibir", "town_S6"] |
| 112 | `geography.towns-syronea` | 城镇 | （无新增） | ["席隆尼亚", "Syronea", "城镇", "town_ES7"] | ["席隆尼亚", "Syronea", "town_ES7"] |
| 113 | `geography.towns-tyal` | 城镇 | （无新增） | ["蒂亚尔", "Tyal", "城镇", "town_S5"] | ["蒂亚尔", "Tyal", "town_S5"] |
| 114 | `geography.towns-varnovapol` | 城镇 | （无新增） | ["瓦尔诺瓦波尔", "Varnovapol", "城镇", "town_S4"] | ["瓦尔诺瓦波尔", "Varnovapol", "town_S4"] |
| 115 | `geography.towns-vostrum` | 城镇 | （无新增） | ["沃斯特鲁姆", "Vostrum", "城镇", "town_ES2"] | ["沃斯特鲁姆", "Vostrum", "town_ES2"] |
| 116 | `geography.towns-zeonica` | 城镇 | （无新增） | ["泽翁尼卡", "Zeonica", "城镇", "town_EW2"] | ["泽翁尼卡", "Zeonica", "town_EW2"] |
| 117 | `geography.villages-ab-comer` | 村庄 | （无新增） | ["阿布·科梅尔", "Ab Comer", "村庄", "castle_village_B1_1"] | ["阿布·科梅尔", "Ab Comer", "castle_village_B1_1"] |
| 118 | `geography.villages-abba` | 村庄 | （无新增） | ["阿卜巴", "Abba", "村庄", "village_A3_1"] | ["阿卜巴", "Abba", "village_A3_1"] |
| 119 | `geography.villages-abghan` | 村庄 | （无新增） | ["阿卜甘", "Abghan", "村庄", "village_A8_2"] | ["阿卜甘", "Abghan", "village_A8_2"] |
| 120 | `geography.villages-abu-khih` | 村庄 | （无新增） | ["艾布·希", "Abu Khih", "村庄", "village_A2_2"] | ["艾布·希", "Abu Khih", "village_A2_2"] |
| 121 | `geography.villages-aegosca` | 村庄 | （无新增） | ["埃戈斯卡", "Aegosca", "村庄", "village_EW3_2"] | ["埃戈斯卡", "Aegosca", "village_EW3_2"] |
| 122 | `geography.villages-aeoria` | 村庄 | （无新增） | ["艾俄里亚", "Aeoria", "村庄", "castle_village_EN1_2"] | ["艾俄里亚", "Aeoria", "castle_village_EN1_2"] |
| 123 | `geography.villages-agalmon` | 村庄 | （无新增） | ["阿加尔蒙", "Agalmon", "村庄", "castle_village_EN9_2"] | ["阿加尔蒙", "Agalmon", "castle_village_EN9_2"] |
| 124 | `geography.villages-ain-baliq` | 村庄 | （无新增） | ["艾因·巴力克", "Ain Baliq", "村庄", "castle_village_A3_1"] | ["艾因·巴力克", "Ain Baliq", "castle_village_A3_1"] |
| 125 | `geography.villages-akiser` | 村庄 | （无新增） | ["阿契赛尔", "Akiser", "村庄", "castle_village_K2_1"] | ["阿契赛尔", "Akiser", "castle_village_K2_1"] |
| 126 | `geography.villages-alantas` | 村庄 | （无新增） | ["阿兰塔斯", "Alantas", "村庄", "village_V9_1"] | ["阿兰塔斯", "Alantas", "village_V9_1"] |
| 127 | `geography.villages-alatys` | 村庄 | （无新增） | ["阿拉堤斯", "Alatys", "村庄", "village_EN4_2"] | ["阿拉堤斯", "Alatys", "village_EN4_2"] |
| 128 | `geography.villages-alebat` | 村庄 | （无新增） | ["阿莱巴特", "Alebat", "村庄", "village_S6_3"] | ["阿莱巴特", "Alebat", "village_S6_3"] |
| 129 | `geography.villages-alision` | 村庄 | （无新增） | ["阿利西翁", "Alision", "村庄", "village_ES2_4"] | ["阿利西翁", "Alision", "village_ES2_4"] |
| 130 | `geography.villages-alorstan` | 村庄 | （无新增） | ["阿洛斯唐", "Alorstan", "村庄", "village_V6_3"] | ["阿洛斯唐", "Alorstan", "village_V6_3"] |
| 131 | `geography.villages-alosea` | 村庄 | （无新增） | ["阿罗塞亚", "Alosea", "村庄", "village_EN2_1"] | ["阿罗塞亚", "Alosea", "village_EN2_1"] |
| 132 | `geography.villages-alov` | 村庄 | （无新增） | ["阿洛夫", "Alov", "村庄", "castle_village_S7_2"] | ["阿洛夫", "Alov", "castle_village_S7_2"] |
| 133 | `geography.villages-alsasos` | 村庄 | （无新增） | ["阿尔萨索斯", "Alsasos", "村庄", "village_EW2_3"] | ["阿尔萨索斯", "Alsasos", "village_EW2_3"] |
| 134 | `geography.villages-amycon` | 村庄 | （无新增） | ["阿密孔", "Amycon", "村庄", "castle_village_ES6_2"] | ["阿密孔", "Amycon", "castle_village_ES6_2"] |
| 135 | `geography.villages-andurn` | 村庄 | （无新增） | ["安杜恩", "Andurn", "村庄", "village_B4_2"] | ["安杜恩", "Andurn", "village_B4_2"] |
| 136 | `geography.villages-arpotis` | 村庄 | （无新增） | ["阿耳波提斯", "Arpotis", "村庄", "village_EW4_1"] | ["阿耳波提斯", "Arpotis", "village_EW4_1"] |
| 137 | `geography.villages-arromanc` | 村庄 | （无新增） | ["阿罗曼克", "Arromanc", "村庄", "village_V6_1"] | ["阿罗曼克", "Arromanc", "village_V6_1"] |
| 138 | `geography.villages-asalig` | 村庄 | （无新增） | ["阿萨利格", "Asalig", "村庄", "village_K1_4"] | ["阿萨利格", "Asalig", "village_K1_4"] |
| 139 | `geography.villages-asmait` | 村庄 | （无新增） | ["阿斯麦特", "Asmait", "村庄", "castle_village_A2_2"] | ["阿斯麦特", "Asmait", "castle_village_A2_2"] |
| 140 | `geography.villages-aster` | 村庄 | （无新增） | ["阿斯特", "Aster", "村庄", "castle_village_B7_1"] | ["阿斯特", "Aster", "castle_village_B7_1"] |
| 141 | `geography.villages-ataconia` | 村庄 | （无新增） | ["阿塔科尼亚", "Ataconia", "村庄", "castle_village_EN6_1"] | ["阿塔科尼亚", "Ataconia", "castle_village_EN6_1"] |
| 142 | `geography.villages-ath-cafal` | 村庄 | （无新增） | ["阿斯·卡瓦尔", "Ath Cafal", "村庄", "village_B1_4"] | ["阿斯·卡瓦尔", "Ath Cafal", "village_B1_4"] |
| 143 | `geography.villages-atphynia` | 村庄 | （无新增） | ["阿特费尼亚", "Atphynia", "村庄", "castle_village_ES5_2"] | ["阿特费尼亚", "Atphynia", "castle_village_ES5_2"] |
| 144 | `geography.villages-atrion` | 村庄 | （无新增） | ["阿特里翁", "Atrion", "村庄", "castle_village_EN5_1"] | ["阿特里翁", "Atrion", "castle_village_EN5_1"] |
| 145 | `geography.villages-avalyps` | 村庄 | （无新增） | ["阿瓦吕普斯", "Avalyps", "村庄", "village_ES2_3"] | ["阿瓦吕普斯", "Avalyps", "village_ES2_3"] |
| 146 | `geography.villages-avasinton` | 村庄 | （无新增） | ["阿瓦辛顿", "Avasinton", "村庄", "village_EN6_1"] | ["阿瓦辛顿", "Avasinton", "village_EN6_1"] |
| 147 | `geography.villages-bait-hatif` | 村庄 | （无新增） | ["拜特·哈提夫", "Bait Hatif", "村庄", "village_A7_2"] | ["拜特·哈提夫", "Bait Hatif", "village_A7_2"] |
| 148 | `geography.villages-baq` | 村庄 | （无新增） | ["巴格", "Baq", "村庄", "village_A1_2"] | ["巴格", "Baq", "village_A1_2"] |
| 149 | `geography.villages-barihal` | 村庄 | （无新增） | ["巴里哈勒", "Barihal", "村庄", "castle_village_A9_1"] | ["巴里哈勒", "Barihal", "castle_village_A9_1"] |
| 150 | `geography.villages-beglomuar` | 村庄 | （无新增） | ["贝格洛米艾", "Beglomuar", "村庄", "village_B1_3"] | ["贝格洛米艾", "Beglomuar", "village_B1_3"] |
| 151 | `geography.villages-bergum` | 村庄 | （无新增） | ["柏耳贡", "Bergum", "村庄", "castle_village_EW2_2"] | ["柏耳贡", "Bergum", "castle_village_EW2_2"] |
| 152 | `geography.villages-bir-seif` | 村庄 | （无新增） | ["比尔·赛义夫", "Bir Seif", "村庄", "village_A3_3"] | ["比尔·赛义夫", "Bir Seif", "village_A3_3"] |
| 153 | `geography.villages-bog-beth` | 村庄 | （无新增） | ["博格·贝斯", "Bog Beth", "村庄", "village_B3_1"] | ["博格·贝斯", "Bog Beth", "village_B3_1"] |
| 154 | `geography.villages-borchovagorka` | 村庄 | （无新增） | ["博乔瓦戈卡", "Borchovagorka", "村庄", "village_S4_1"] | ["博乔瓦戈卡", "Borchovagorka", "village_S4_1"] |
| 155 | `geography.villages-boreagora` | 村庄 | （无新增） | ["玻瑞阿戈拉", "Boreagora", "村庄", "village_EN6_2"] | ["玻瑞阿戈拉", "Boreagora", "village_EN6_2"] |
| 156 | `geography.villages-bryn-glas` | 村庄 | （无新增） | ["布林·格拉斯", "Bryn Glas", "村庄", "village_B4_1"] | ["布林·格拉斯", "Bryn Glas", "village_B4_1"] |
| 157 | `geography.villages-bukits` | 村庄 | （无新增） | ["布基茨", "Bukits", "村庄", "village_S5_2"] | ["布基茨", "Bukits", "village_S5_2"] |
| 158 | `geography.villages-bunqaz` | 村庄 | （无新增） | ["本盖兹", "Bunqaz", "村庄", "castle_village_A7_2"] | ["本盖兹", "Bunqaz", "castle_village_A7_2"] |
| 159 | `geography.villages-caira` | 村庄 | （无新增） | ["恺拉", "Caira", "村庄", "castle_village_ES1_2"] | ["恺拉", "Caira", "castle_village_ES1_2"] |
| 160 | `geography.villages-caleus` | 村庄 | （无新增） | ["卡琉斯", "Caleus", "村庄", "castle_village_V6_1"] | ["卡琉斯", "Caleus", "castle_village_V6_1"] |
| 161 | `geography.villages-calioc` | 村庄 | （无新增） | ["卡利奥克", "Calioc", "村庄", "village_V1_1"] | ["卡利奥克", "Calioc", "village_V1_1"] |
| 162 | `geography.villages-cananc` | 村庄 | （无新增） | ["卡南克", "Cananc", "村庄", "village_V8_2"] | ["卡南克", "Cananc", "village_V8_2"] |
| 163 | `geography.villages-canoros` | 村庄 | （无新增） | ["卡诺洛斯", "Canoros", "村庄", "village_ES3_1"] | ["卡诺洛斯", "Canoros", "village_ES3_1"] |
| 164 | `geography.villages-canterion` | 村庄 | （无新增） | ["坎忒里翁", "Canterion", "村庄", "village_ES4_3"] | ["坎忒里翁", "Canterion", "village_ES4_3"] |
| 165 | `geography.villages-cantrec` | 村庄 | （无新增） | ["坎特雷克", "Cantrec", "村庄", "castle_village_B2_2"] | ["坎特雷克", "Cantrec", "castle_village_B2_2"] |
| 166 | `geography.villages-carphenion` | 村庄 | （无新增） | ["卡耳斐尼翁", "Carphenion", "村庄", "village_EW6_1"] | ["卡耳斐尼翁", "Carphenion", "village_EW6_1"] |
| 167 | `geography.villages-chanopsis` | 村庄 | （无新增） | ["卡诺普西斯", "Chanopsis", "村庄", "castle_village_ES8_1"] | ["卡诺普西斯", "Chanopsis", "castle_village_ES8_1"] |
| 168 | `geography.villages-chornad` | 村庄 | （无新增） | ["绍尔纳", "Chornad", "村庄", "village_V6_4"] | ["绍尔纳", "Chornad", "village_V6_4"] |
| 169 | `geography.villages-chornobas` | 村庄 | （无新增） | ["乔诺巴斯", "Chornobas", "村庄", "village_S3_1"] | ["乔诺巴斯", "Chornobas", "village_S3_1"] |
| 170 | `geography.villages-claig-ban` | 村庄 | （无新增） | ["克莱格·班", "Claig Ban", "村庄", "castle_village_B5_2"] | ["克莱格·班", "Claig Ban", "castle_village_B5_2"] |
| 171 | `geography.villages-corenia` | 村庄 | （无新增） | ["科雷尼亚", "Corenia", "村庄", "castle_village_ES2_1"] | ["科雷尼亚", "Corenia", "castle_village_ES2_1"] |
| 172 | `geography.villages-crios` | 村庄 | （无新增） | ["克里俄斯", "Crios", "村庄", "village_EN3_3"] | ["克里俄斯", "Crios", "village_EN3_3"] |
| 173 | `geography.villages-dalmengus` | 村庄 | （无新增） | ["达尔门格斯", "Dalmengus", "村庄", "village_B1_1"] | ["达尔门格斯", "Dalmengus", "village_B1_1"] |
| 174 | `geography.villages-danara` | 村庄 | （无新增） | ["达纳拉", "Danara", "村庄", "village_K6_3"] | ["达纳拉", "Danara", "village_K6_3"] |
| 175 | `geography.villages-deir-hawa` | 村庄 | （无新增） | ["代尔·哈瓦", "Deir Hawa", "村庄", "castle_village_A3_2"] | ["代尔·哈瓦", "Deir Hawa", "castle_village_A3_2"] |
| 176 | `geography.villages-deriat` | 德里亚特·村庄 | （无新增） | ["德里亚特", "Deriat", "德里亚特村", "德里亚特·村庄"] | ["德里亚特", "Deriat", "德里亚特村"] |
| 177 | `geography.villages-diantogmail` | 村庄 | （无新增） | ["迪安托格麦尔", "Diantogmail", "村庄", "village_B2_1"] | ["迪安托格麦尔", "Diantogmail", "village_B2_1"] |
| 178 | `geography.villages-dinar` | 村庄 | （无新增） | ["迪纳尔", "Dinar", "村庄", "castle_village_K6_1"] | ["迪纳尔", "Dinar", "castle_village_K6_1"] |
| 179 | `geography.villages-dnin` | 村庄 | （无新增） | ["德宁", "Dnin", "村庄", "castle_village_S3_2"] | ["德宁", "Dnin", "castle_village_S3_2"] |
| 180 | `geography.villages-doqa` | 村庄 | （无新增） | ["多加", "Doqa", "村庄", "village_A4_4"] | ["多加", "Doqa", "village_A4_4"] |
| 181 | `geography.villages-dradios` | 村庄 | （无新增） | ["得拉狄俄斯", "Dradios", "村庄", "village_EW3_3"] | ["得拉狄俄斯", "Dradios", "village_EW3_3"] |
| 182 | `geography.villages-drapand` | 村庄 | （无新增） | ["德拉庞", "Drapand", "村庄", "castle_village_V3_1"] | ["德拉庞", "Drapand", "castle_village_V3_1"] |
| 183 | `geography.villages-druimmor` | 村庄 | （无新增） | ["德鲁伊莫尔", "Druimmor", "村庄", "castle_village_B3_1"] | ["德鲁伊莫尔", "Druimmor", "castle_village_B3_1"] |
| 184 | `geography.villages-durn` | 村庄 | （无新增） | ["杜恩", "Durn", "村庄", "village_B5_1"] | ["杜恩", "Durn", "village_B5_1"] |
| 185 | `geography.villages-dvorusta` | 村庄 | （无新增） | ["德沃鲁斯塔", "Dvorusta", "村庄", "castle_village_S6_2"] | ["德沃鲁斯塔", "Dvorusta", "castle_village_S6_2"] |
| 186 | `geography.villages-dyopalis` | 村庄 | （无新增） | ["底俄帕利斯", "Dyopalis", "村庄", "castle_village_EN3_2"] | ["底俄帕利斯", "Dyopalis", "castle_village_EN3_2"] |
| 187 | `geography.villages-ebereth` | 村庄 | （无新增） | ["埃贝雷斯", "Ebereth", "村庄", "village_B1_2"] | ["埃贝雷斯", "Ebereth", "village_B1_2"] |
| 188 | `geography.villages-elipa` | 村庄 | （无新增） | ["厄利帕", "Elipa", "村庄", "village_EW5_1"] | ["厄利帕", "Elipa", "village_EW5_1"] |
| 189 | `geography.villages-elvania` | 村庄 | （无新增） | ["厄尔凡尼亚", "Elvania", "村庄", "castle_village_EW7_2"] | ["厄尔凡尼亚", "Elvania", "castle_village_EW7_2"] |
| 190 | `geography.villages-enoisa` | 村庄 | （无新增） | ["厄诺伊萨", "Enoisa", "村庄", "village_EN3_1"] | ["厄诺伊萨", "Enoisa", "village_EN3_1"] |
| 191 | `geography.villages-epinosa` | 村庄 | （无新增） | ["厄毗诺萨", "Epinosa", "村庄", "castle_village_EN7_1"] | ["厄毗诺萨", "Epinosa", "castle_village_EN7_1"] |
| 192 | `geography.villages-erebulos` | 村庄 | （无新增） | ["厄瑞玻洛斯", "Erebulos", "村庄", "village_ES1_4"] | ["厄瑞玻洛斯", "Erebulos", "village_ES1_4"] |
| 193 | `geography.villages-erzenur` | 村庄 | （无新增） | ["埃泽努尔", "Erzenur", "村庄", "castle_village_K8_1"] | ["埃泽努尔", "Erzenur", "castle_village_K8_1"] |
| 194 | `geography.villages-esme` | 村庄 | （无新增） | ["埃斯梅", "Esme", "村庄", "castle_village_K1_2"] | ["埃斯梅", "Esme", "castle_village_K1_2"] |
| 195 | `geography.villages-ethemisa` | 村庄 | （无新增） | ["厄忒弥萨", "Ethemisa", "村庄", "castle_village_ES4_2"] | ["厄忒弥萨", "Ethemisa", "castle_village_ES4_2"] |
| 196 | `geography.villages-etirfurd` | 村庄 | （无新增） | ["埃蒂尔菲德", "Etirfurd", "村庄", "village_V1_2"] | ["埃蒂尔菲德", "Etirfurd", "village_V1_2"] |
| 197 | `geography.villages-eunalica` | 村庄 | （无新增） | ["优纳利卡", "Eunalica", "村庄", "castle_village_ES7_2"] | ["优纳利卡", "Eunalica", "castle_village_ES7_2"] |
| 198 | `geography.villages-ezbet-nahul` | 村庄 | （无新增） | ["艾兹贝特·纳胡勒", "Ezbet Nahul", "村庄", "village_A8_1"] | ["艾兹贝特·纳胡勒", "Ezbet Nahul", "village_A8_1"] |
| 199 | `geography.villages-fanab` | 村庄 | （无新增） | ["法纳卜", "Fanab", "村庄", "castle_village_A1_2"] | ["法纳卜", "Fanab", "castle_village_A1_2"] |
| 200 | `geography.villages-fenon-etir` | 村庄 | （无新增） | ["韦农·埃蒂尔", "Fenon Etir", "村庄", "village_B5_3"] | ["韦农·埃蒂尔", "Fenon Etir", "village_B5_3"] |
| 201 | `geography.villages-ferkh` | 村庄 | （无新增） | ["费赫", "Ferkh", "村庄", "castle_village_S5_2"] | ["费赫", "Ferkh", "castle_village_S5_2"] |
| 202 | `geography.villages-ferton` | 村庄 | （无新增） | ["费顿", "Ferton", "村庄", "castle_village_V2_2"] | ["费顿", "Ferton", "castle_village_V2_2"] |
| 203 | `geography.villages-fisnar` | 村庄 | （无新增） | ["菲斯纳尔", "Fisnar", "村庄", "village_K1_1"] | ["菲斯纳尔", "Fisnar", "village_K1_1"] |
| 204 | `geography.villages-flintolg` | 村庄 | （无新增） | ["弗林托格", "Flintolg", "村庄", "castle_village_B6_1"] | ["弗林托格", "Flintolg", "castle_village_B6_1"] |
| 205 | `geography.villages-forin` | 村庄 | （无新增） | ["福林", "Forin", "村庄", "castle_village_S2_2"] | ["福林", "Forin", "castle_village_S2_2"] |
| 206 | `geography.villages-fregian` | 村庄 | （无新增） | ["弗雷吉昂", "Fregian", "村庄", "village_V2_3"] | ["弗雷吉昂", "Fregian", "village_V2_3"] |
| 207 | `geography.villages-furbec` | 村庄 | （无新增） | ["菲尔贝克", "Furbec", "村庄", "village_V5_1"] | ["菲尔贝克", "Furbec", "village_V5_1"] |
| 208 | `geography.villages-gainseth` | 村庄 | （无新增） | ["盖恩塞斯", "Gainseth", "村庄", "village_B5_2"] | ["盖恩塞斯", "Gainseth", "village_B5_2"] |
| 209 | `geography.villages-gamardan` | 村庄 | （无新增） | ["伽玛耳丹", "Gamardan", "村庄", "castle_village_EW4_2"] | ["伽玛耳丹", "Gamardan", "castle_village_EW4_2"] |
| 210 | `geography.villages-gaos` | 村庄 | （无新增） | ["伽俄斯", "Gaos", "村庄", "castle_village_EN4_1"] | ["伽俄斯", "Gaos", "castle_village_EN4_1"] |
| 211 | `geography.villages-garengolia` | 村庄 | （无新增） | ["伽伦戈利亚", "Garengolia", "村庄", "village_EW4_4"] | ["伽伦戈利亚", "Garengolia", "village_EW4_4"] |
| 212 | `geography.villages-garontor` | 村庄 | （无新增） | ["加隆托", "Garontor", "村庄", "castle_village_EW1_1"] | ["加隆托", "Garontor", "castle_village_EW1_1"] |
| 213 | `geography.villages-gereden` | 村庄 | （无新增） | ["格烈登", "Gereden", "村庄", "castle_village_K8_2"] | ["格烈登", "Gereden", "castle_village_K8_2"] |
| 214 | `geography.villages-gersegos` | 村庄 | （无新增） | ["革耳塞戈斯", "Gersegos", "村庄", "castle_village_EW8_1"] | ["革耳塞戈斯", "Gersegos", "castle_village_EW8_1"] |
| 215 | `geography.villages-geunat-nal` | 村庄 | （无新增） | ["格纳特·纳尔", "Geunat Nal", "村庄", "village_B3_2"] | ["格纳特·纳尔", "Geunat Nal", "village_B3_2"] |
| 216 | `geography.villages-glavstrom` | 村庄 | （无新增） | ["格拉夫斯特伦", "Glavstrom", "村庄", "castle_village_S8_2"] | ["格拉夫斯特伦", "Glavstrom", "castle_village_S8_2"] |
| 217 | `geography.villages-glenlithrig` | 村庄 | （无新增） | ["格伦利斯里格", "Glenlithrig", "村庄", "village_B2_2"] | ["格伦利斯里格", "Glenlithrig", "village_B2_2"] |
| 218 | `geography.villages-glintor` | 村庄 | （无新增） | ["格林托尔", "Glintor", "村庄", "castle_village_B6_2"] | ["格林托尔", "Glintor", "castle_village_B6_2"] |
| 219 | `geography.villages-goleryn` | 村庄 | （无新增） | ["戈勒林", "Goleryn", "村庄", "castle_village_EW5_2"] | ["戈勒林", "Goleryn", "castle_village_EW5_2"] |
| 220 | `geography.villages-gorcorys` | 村庄 | （无新增） | ["戈耳科律斯", "Gorcorys", "村庄", "village_ES2_2"] | ["戈耳科律斯", "Gorcorys", "village_ES2_2"] |
| 221 | `geography.villages-gymos` | 村庄 | （无新增） | ["居摩斯", "Gymos", "村庄", "village_EN1_3"] | ["居摩斯", "Gymos", "village_EN1_3"] |
| 222 | `geography.villages-hakkun` | 村庄 | （无新增） | ["哈坤", "Hakkun", "村庄", "castle_village_K3_1"] | ["哈坤", "Hakkun", "castle_village_K3_1"] |
| 223 | `geography.villages-halisvust` | 村庄 | （无新增） | ["阿利斯维斯特", "Halisvust", "村庄", "village_V9_2"] | ["阿利斯维斯特", "Halisvust", "village_V9_2"] |
| 224 | `geography.villages-hamoshawat` | 村庄 | （无新增） | ["哈穆沙瓦", "Hamoshawat", "村庄", "village_A6_2"] | ["哈穆沙瓦", "Hamoshawat", "village_A6_2"] |
| 225 | `geography.villages-hanekhy` | 村庄 | （无新增） | ["哈内希", "Hanekhy", "村庄", "village_K3_2"] | ["哈内希", "Hanekhy", "village_K3_2"] |
| 226 | `geography.villages-hertogea` | 村庄 | （无新增） | ["赫托该亚", "Hertogea", "村庄", "castle_village_EW6_1"] | ["赫托该亚", "Hertogea", "castle_village_EW6_1"] |
| 227 | `geography.villages-hetania` | 村庄 | （无新增） | ["赫塔尼亚", "Hetania", "村庄", "village_EN4_4"] | ["赫塔尼亚", "Hetania", "village_EN4_4"] |
| 228 | `geography.villages-hiblet` | 村庄 | （无新增） | ["希卜莱特", "Hiblet", "村庄", "village_A1_4"] | ["希卜莱特", "Hiblet", "village_A1_4"] |
| 229 | `geography.villages-hongard` | 村庄 | （无新增） | ["翁加尔", "Hongard", "村庄", "castle_village_V2_1"] | ["翁加尔", "Hongard", "castle_village_V2_1"] |
| 230 | `geography.villages-hoqqa` | 村庄 | （无新增） | ["胡加", "Hoqqa", "村庄", "village_A2_3"] | ["胡加", "Hoqqa", "village_A2_3"] |
| 231 | `geography.villages-horsger` | 村庄 | （无新增） | ["奥尔斯热", "Horsger", "村庄", "village_V8_1"] | ["奥尔斯热", "Horsger", "village_V8_1"] |
| 232 | `geography.villages-hunab` | 村庄 | （无新增） | ["侯纳卜", "Hunab", "村庄", "castle_village_A5_2"] | ["侯纳卜", "Hunab", "castle_village_A5_2"] |
| 233 | `geography.villages-imlagh` | 村庄 | （无新增） | ["伊姆拉赫", "Imlagh", "村庄", "castle_village_B7_2"] | ["伊姆拉赫", "Imlagh", "castle_village_B7_2"] |
| 234 | `geography.villages-inveth` | 村庄 | （无新增） | ["因韦斯", "Inveth", "村庄", "castle_village_B1_2"] | ["因韦斯", "Inveth", "castle_village_B1_2"] |
| 235 | `geography.villages-ismilkorg` | 村庄 | （无新增） | ["伊斯米尔科格", "Ismilkorg", "村庄", "castle_village_S4_2"] | ["伊斯米尔科格", "Ismilkorg", "castle_village_S4_2"] |
| 236 | `geography.villages-ispantar` | 村庄 | （无新增） | ["依斯潘塔尔", "Ispantar", "村庄", "village_K5_3"] | ["依斯潘塔尔", "Ispantar", "village_K5_3"] |
| 237 | `geography.villages-jahasim` | 村庄 | （无新增） | ["贾哈西姆", "Jahasim", "村庄", "village_A6_3"] | ["贾哈西姆", "Jahasim", "village_A6_3"] |
| 238 | `geography.villages-jamayeh` | 村庄 | （无新增） | ["贾迈耶", "Jamayeh", "村庄", "castle_village_A5_1"] | ["贾迈耶", "Jamayeh", "castle_village_A5_1"] |
| 239 | `geography.villages-jeracos` | 村庄 | （无新增） | ["耶拉科斯", "Jeracos", "村庄", "village_EN2_2"] | ["耶拉科斯", "Jeracos", "village_EN2_2"] |
| 240 | `geography.villages-jogurys` | 村庄 | （无新增） | ["约格律斯", "Jogurys", "村庄", "castle_village_ES7_1"] | ["约格律斯", "Jogurys", "castle_village_ES7_1"] |
| 241 | `geography.villages-kamshar` | 村庄 | （无新增） | ["喀木沙", "Kamshar", "村庄", "castle_village_K2_2"] | ["喀木沙", "Kamshar", "castle_village_K2_2"] |
| 242 | `geography.villages-karahalli` | 村庄 | （无新增） | ["喀拉哈力", "Karahalli", "村庄", "castle_village_K6_2"] | ["喀拉哈力", "Karahalli", "castle_village_K6_2"] |
| 243 | `geography.villages-karahan` | 村庄 | （无新增） | ["喀拉罕", "Karahan", "村庄", "village_K6_1"] | ["喀拉罕", "Karahan", "village_K6_1"] |
| 244 | `geography.villages-karakalat` | 村庄 | （无新增） | ["喀拉卡拉特", "Karakalat", "村庄", "village_K2_1"] | ["喀拉卡拉特", "Karakalat", "village_K2_1"] |
| 245 | `geography.villages-karbur` | 村庄 | （无新增） | ["卡布尔", "Karbur", "村庄", "village_S7_2"] | ["卡布尔", "Karbur", "village_S7_2"] |
| 246 | `geography.villages-kargrev` | 村庄 | （无新增） | ["卡格雷夫", "Kargrev", "村庄", "village_S1_3"] | ["卡格雷夫", "Kargrev", "village_S1_3"] |
| 247 | `geography.villages-kaysar` | 村庄 | （无新增） | ["开撒尔", "Kaysar", "村庄", "castle_village_K9_1"] | ["开撒尔", "Kaysar", "castle_village_K9_1"] |
| 248 | `geography.villages-khimli` | 村庄 | （无新增） | ["希木利", "Khimli", "村庄", "castle_village_K5_1"] | ["希木利", "Khimli", "castle_village_K5_1"] |
| 249 | `geography.villages-kiraz` | 村庄 | （无新增） | ["基拉兹", "Kiraz", "村庄", "castle_village_K3_2"] | ["基拉兹", "Kiraz", "castle_village_K3_2"] |
| 250 | `geography.villages-kohi-ajik` | 村庄 | （无新增） | ["柯希·阿吉克", "Kohi Ajik", "村庄", "castle_village_K7_2"] | ["柯希·阿吉克", "Kohi Ajik", "castle_village_K7_2"] |
| 251 | `geography.villages-korsyas` | 村庄 | （无新增） | ["科尔夏斯", "Korsyas", "村庄", "village_S7_1"] | ["科尔夏斯", "Korsyas", "village_S7_1"] |
| 252 | `geography.villages-kranirog` | 村庄 | （无新增） | ["克拉尼罗格", "Kranirog", "村庄", "castle_village_S4_1"] | ["克拉尼罗格", "Kranirog", "castle_village_S4_1"] |
| 253 | `geography.villages-kuqa` | 村庄 | （无新增） | ["库加", "Kuqa", "村庄", "castle_village_A8_2"] | ["库加", "Kuqa", "castle_village_A8_2"] |
| 254 | `geography.villages-kuruluk` | 村庄 | （无新增） | ["库鲁卢克", "Kuruluk", "村庄", "castle_village_K4_2"] | ["库鲁卢克", "Kuruluk", "castle_village_K4_2"] |
| 255 | `geography.villages-kvol` | 村庄 | （无新增） | ["克沃尔", "Kvol", "村庄", "village_S6_1"] | ["克沃尔", "Kvol", "village_S6_1"] |
| 256 | `geography.villages-lamesa` | 村庄 | （无新增） | ["拉迈萨", "Lamesa", "村庄", "castle_village_A6_2"] | ["拉迈萨", "Lamesa", "castle_village_A6_2"] |
| 257 | `geography.villages-lanthas` | 村庄 | （无新增） | ["兰塔斯", "Lanthas", "村庄", "village_ES5_1"] | ["兰塔斯", "Lanthas", "village_ES5_1"] |
| 258 | `geography.villages-larnac` | 村庄 | （无新增） | ["拉尔纳克", "Larnac", "村庄", "village_V3_3"] | ["拉尔纳克", "Larnac", "village_V3_3"] |
| 259 | `geography.villages-lartusys` | 村庄 | （无新增） | ["拉耳图绪斯", "Lartusys", "村庄", "village_ES5_2"] | ["拉耳图绪斯", "Lartusys", "village_ES5_2"] |
| 260 | `geography.villages-lavenia` | 村庄 | （无新增） | ["拉文尼亚", "Lavenia", "村庄", "castle_village_ES4_1"] | ["拉文尼亚", "Lavenia", "castle_village_ES4_1"] |
| 261 | `geography.villages-leblenion` | 村庄 | （无新增） | ["勒布伦尼翁", "Leblenion", "村庄", "village_EW6_4"] | ["勒布伦尼翁", "Leblenion", "village_EW6_4"] |
| 262 | `geography.villages-lindorn` | 村庄 | （无新增） | ["林杜恩", "Lindorn", "村庄", "castle_village_B4_2"] | ["林杜恩", "Lindorn", "castle_village_B4_2"] |
| 263 | `geography.villages-liwas` | 村庄 | （无新增） | ["利瓦斯", "Liwas", "村庄", "village_A5_2"] | ["利瓦斯", "Liwas", "village_A5_2"] |
| 264 | `geography.villages-llanoc-hen` | 村庄 | （无新增） | ["拉诺克·亨", "Llanoc Hen", "村庄", "castle_village_B2_1"] | ["拉诺克·亨", "Llanoc Hen", "castle_village_B2_1"] |
| 265 | `geography.villages-lochana` | 村庄 | （无新增） | ["罗卡那", "Lochana", "村庄", "castle_village_EN2_1"] | ["罗卡那", "Lochana", "castle_village_EN2_1"] |
| 266 | `geography.villages-lysia` | 村庄 | （无新增） | ["吕西亚", "Lysia", "村庄", "castle_village_EW1_2"] | ["吕西亚", "Lysia", "castle_village_EW1_2"] |
| 267 | `geography.villages-mabwaz` | 村庄 | （无新增） | ["马卜瓦兹", "Mabwaz", "村庄", "village_A7_3"] | ["马卜瓦兹", "Mabwaz", "village_A7_3"] |
| 268 | `geography.villages-mag-arba` | 村庄 | （无新增） | ["马格·阿尔巴", "Mag Arba", "村庄", "village_B4_3"] | ["马格·阿尔巴", "Mag Arba", "village_B4_3"] |
| 269 | `geography.villages-mahloul` | 村庄 | （无新增） | ["马赫卢勒", "Mahloul", "村庄", "village_A5_1"] | ["马赫卢勒", "Mahloul", "village_A5_1"] |
| 270 | `geography.villages-marabrot` | 村庄 | （无新增） | ["马拉布罗特", "Marabrot", "村庄", "village_S2_2"] | ["马拉布罗特", "Marabrot", "village_S2_2"] |
| 271 | `geography.villages-marathea` | 村庄 | （无新增） | ["马拉忒亚", "Marathea", "村庄", "village_EN1_1"] | ["马拉忒亚", "Marathea", "village_EN1_1"] |
| 272 | `geography.villages-mareiven` | 村庄 | （无新增） | ["马雷汶", "Mareiven", "村庄", "village_V2_1"] | ["马雷汶", "Mareiven", "village_V2_1"] |
| 273 | `geography.villages-marin` | 村庄 | （无新增） | ["马林", "Marin", "村庄", "castle_village_V8_2"] | ["马林", "Marin", "castle_village_V8_2"] |
| 274 | `geography.villages-masangara` | 村庄 | （无新增） | ["马珊加拉", "Masangara", "村庄", "castle_village_EN5_2"] | ["马珊加拉", "Masangara", "castle_village_EN5_2"] |
| 275 | `geography.villages-mazen` | 村庄 | （无新增） | ["马津", "Mazen", "村庄", "village_K3_3"] | ["马津", "Mazen", "village_K3_3"] |
| 276 | `geography.villages-mazhadan` | 村庄 | （无新增） | ["马扎丹", "Mazhadan", "村庄", "castle_village_S2_1"] | ["马扎丹", "Mazhadan", "castle_village_S2_1"] |
| 277 | `geography.villages-mecalovea` | 村庄 | （无新增） | ["墨卡罗维亚", "Mecalovea", "村庄", "castle_village_EN9_1"] | ["墨卡罗维亚", "Mecalovea", "castle_village_EN9_1"] |
| 278 | `geography.villages-medeni` | 村庄 | （无新增） | ["迈代尼", "Medeni", "村庄", "castle_village_A4_1"] | ["迈代尼", "Medeni", "castle_village_A4_1"] |
| 279 | `geography.villages-melion` | 村庄 | （无新增） | ["墨利翁", "Melion", "村庄", "castle_village_ES3_1"] | ["墨利翁", "Melion", "castle_village_ES3_1"] |
| 280 | `geography.villages-meroc` | 村庄 | （无新增） | ["梅罗克", "Meroc", "村庄", "village_V5_2"] | ["梅罗克", "Meroc", "village_V5_2"] |
| 281 | `geography.villages-metachia` | 村庄 | （无新增） | ["墨塔基亚", "Metachia", "村庄", "castle_village_ES2_2"] | ["墨塔基亚", "Metachia", "castle_village_ES2_2"] |
| 282 | `geography.villages-mijayit` | 村庄 | （无新增） | ["米贾伊特", "Mijayit", "村庄", "village_A6_1"] | ["米贾伊特", "Mijayit", "village_A6_1"] |
| 283 | `geography.villages-mivanjan` | 村庄 | （无新增） | ["米万占", "Mivanjan", "村庄", "village_K4_3"] | ["米万占", "Mivanjan", "village_K4_3"] |
| 284 | `geography.villages-montos` | 村庄 | （无新增） | ["蒙托斯", "Montos", "村庄", "village_EW1_2"] | ["蒙托斯", "Montos", "village_EW1_2"] |
| 285 | `geography.villages-morenia` | 村庄 | （无新增） | ["摩雷尼亚", "Morenia", "村庄", "castle_village_ES5_1"] | ["摩雷尼亚", "Morenia", "castle_village_ES5_1"] |
| 286 | `geography.villages-morihig` | 村庄 | （无新增） | ["莫里希格", "Morihig", "村庄", "village_B2_3"] | ["莫里希格", "Morihig", "village_B2_3"] |
| 287 | `geography.villages-mot` | 村庄 | （无新增） | ["莫特", "Mot", "村庄", "village_V6_2"] | ["莫特", "Mot", "village_V6_2"] |
| 288 | `geography.villages-mussum` | 村庄 | （无新增） | ["穆苏姆", "Mussum", "村庄", "village_A4_2"] | ["穆苏姆", "Mussum", "village_A4_2"] |
| 289 | `geography.villages-nahlan` | 村庄 | （无新增） | ["纳赫兰", "Nahlan", "村庄", "village_A6_4"] | ["纳赫兰", "Nahlan", "village_A6_4"] |
| 290 | `geography.villages-neocorys` | 村庄 | （无新增） | ["涅俄科律斯", "Neocorys", "村庄", "village_EW2_2"] | ["涅俄科律斯", "Neocorys", "village_EW2_2"] |
| 291 | `geography.villages-nevyansk` | 村庄 | （无新增） | ["涅夫扬斯克", "Nevyansk", "村庄", "castle_village_S3_1"] | ["涅夫扬斯克", "Nevyansk", "castle_village_S3_1"] |
| 292 | `geography.villages-nideon` | 村庄 | （无新增） | ["尼得翁", "Nideon", "村庄", "castle_village_EW6_2"] | ["尼得翁", "Nideon", "castle_village_EW6_2"] |
| 293 | `geography.villages-nogrent` | 村庄 | （无新增） | ["诺格伦", "Nogrent", "村庄", "village_V5_3"] | ["诺格伦", "Nogrent", "village_V5_3"] |
| 294 | `geography.villages-nortanisa` | 村庄 | （无新增） | ["诺耳塔尼萨", "Nortanisa", "村庄", "castle_village_EN2_2"] | ["诺耳塔尼萨", "Nortanisa", "castle_village_EN2_2"] |
| 295 | `geography.villages-nutyuk` | 村庄 | （无新增） | ["努丘克", "Nutyuk", "村庄", "village_K6_2"] | ["努丘克", "Nutyuk", "village_K6_2"] |
| 296 | `geography.villages-odrysa` | 村庄 | （无新增） | ["俄德律萨", "Odrysa", "村庄", "castle_village_ES1_1"] | ["俄德律萨", "Odrysa", "castle_village_ES1_1"] |
| 297 | `geography.villages-okhutan` | 村庄 | （无新增） | ["奥胡坦", "Okhutan", "村庄", "village_K5_2"] | ["奥胡坦", "Okhutan", "village_K5_2"] |
| 298 | `geography.villages-omkany` | 村庄 | （无新增） | ["奥姆卡尼", "Omkany", "村庄", "village_S4_3"] | ["奥姆卡尼", "Omkany", "village_S4_3"] |
| 299 | `geography.villages-omrotok` | 村庄 | （无新增） | ["鄂木罗托克", "Omrotok", "村庄", "castle_village_K5_2"] | ["鄂木罗托克", "Omrotok", "castle_village_K5_2"] |
| 300 | `geography.villages-onica` | 村庄 | （无新增） | ["俄尼卡", "Onica", "村庄", "castle_village_EW3_1"] | ["俄尼卡", "Onica", "castle_village_EW3_1"] |
| 301 | `geography.villages-oristocorys` | 村庄 | （无新增） | ["俄里斯托科律斯", "Oristocorys", "村庄", "castle_village_EW7_1"] | ["俄里斯托科律斯", "Oristocorys", "castle_village_EW7_1"] |
| 302 | `geography.villages-oritan` | 村庄 | （无新增） | ["奥里唐", "Oritan", "村庄", "village_V2_2"] | ["奥里唐", "Oritan", "village_V2_2"] |
| 303 | `geography.villages-ormanfard` | 村庄 | （无新增） | ["奥曼法德", "Ormanfard", "村庄", "castle_village_V4_1"] | ["奥曼法德", "Ormanfard", "castle_village_V4_1"] |
| 304 | `geography.villages-orthra` | 村庄 | （无新增） | ["俄耳特拉", "Orthra", "村庄", "village_EN5_2"] | ["俄耳特拉", "Orthra", "village_EN5_2"] |
| 305 | `geography.villages-ov` | 村庄 | （无新增） | ["奥夫", "Ov", "村庄", "castle_village_S5_1"] | ["奥夫", "Ov", "castle_village_S5_1"] |
| 306 | `geography.villages-pabastan` | 村庄 | （无新增） | ["帕巴斯坦", "Pabastan", "村庄", "village_K5_4"] | ["帕巴斯坦", "Pabastan", "village_K5_4"] |
| 307 | `geography.villages-palisont` | 村庄 | （无新增） | ["帕利松", "Palisont", "村庄", "village_V3_4"] | ["帕利松", "Palisont", "village_V3_4"] |
| 308 | `geography.villages-parasemnos` | 村庄 | （无新增） | ["帕拉森诺斯", "Parasemnos", "村庄", "village_ES5_3"] | ["帕拉森诺斯", "Parasemnos", "village_ES5_3"] |
| 309 | `geography.villages-payam` | 村庄 | （无新增） | ["帕亚木", "Payam", "村庄", "castle_village_K9_2"] | ["帕亚木", "Payam", "castle_village_K9_2"] |
| 310 | `geography.villages-pendraic` | 村庄 | （无新增） | ["潘德拉克", "Pendraic", "村庄", "castle_village_B4_1"] | ["潘德拉克", "Pendraic", "castle_village_B4_1"] |
| 311 | `geography.villages-phasos` | 村庄 | （无新增） | ["法索斯", "Phasos", "村庄", "village_EW5_2"] | ["法索斯", "Phasos", "village_EW5_2"] |
| 312 | `geography.villages-polisia` | 村庄 | （无新增） | ["波利西亚", "Polisia", "村庄", "village_ES1_2"] | ["波利西亚", "Polisia", "village_ES1_2"] |
| 313 | `geography.villages-pons` | 村庄 | （无新增） | ["庞斯", "Pons", "村庄", "castle_village_EN7_2"] | ["庞斯", "Pons", "castle_village_EN7_2"] |
| 314 | `geography.villages-popsia` | 村庄 | （无新增） | ["波普西亚", "Popsia", "村庄", "castle_village_ES8_2"] | ["波普西亚", "Popsia", "castle_village_ES8_2"] |
| 315 | `geography.villages-potamis` | 村庄 | （无新增） | ["珀塔米斯", "Potamis", "村庄", "castle_village_EN6_2"] | ["珀塔米斯", "Potamis", "castle_village_EN6_2"] |
| 316 | `geography.villages-primessos` | 村庄 | （无新增） | ["普里墨索斯", "Primessos", "村庄", "village_EW1_1"] | ["普里墨索斯", "Primessos", "village_EW1_1"] |
| 317 | `geography.villages-psotai` | 村庄 | （无新增） | ["索泰", "Psotai", "村庄", "village_ES7_1"] | ["索泰", "Psotai", "village_ES7_1"] |
| 318 | `geography.villages-qablab` | 村庄 | （无新增） | ["加卜拉卜", "Qablab", "村庄", "village_A4_1"] | ["加卜拉卜", "Qablab", "village_A4_1"] |
| 319 | `geography.villages-qidnar` | 村庄 | （无新增） | ["吉德纳尔", "Qidnar", "村庄", "castle_village_A4_2"] | ["吉德纳尔", "Qidnar", "castle_village_A4_2"] |
| 320 | `geography.villages-radakmed` | 村庄 | （无新增） | ["拉达克梅德", "Radakmed", "村庄", "village_S6_2"] | ["拉达克梅德", "Radakmed", "village_S6_2"] |
| 321 | `geography.villages-ransam` | 村庄 | （无新增） | ["兰萨木", "Ransam", "村庄", "village_K4_2"] | ["兰萨木", "Ransam", "village_K4_2"] |
| 322 | `geography.villages-remental` | 村庄 | （无新增） | ["勒芒塔尔", "Remental", "村庄", "village_V8_3"] | ["勒芒塔尔", "Remental", "village_V8_3"] |
| 323 | `geography.villages-rhemtoil` | 村庄 | （无新增） | ["雷姆托伊尔", "Rhemtoil", "村庄", "castle_village_B5_1"] | ["雷姆托伊尔", "Rhemtoil", "castle_village_B5_1"] |
| 324 | `geography.villages-rhesos` | 村庄 | （无新增） | ["雷索斯", "Rhesos", "村庄", "castle_village_EN3_1"] | ["雷索斯", "Rhesos", "castle_village_EN3_1"] |
| 325 | `geography.villages-rodetan` | 村庄 | （无新增） | ["罗德唐", "Rodetan", "村庄", "castle_village_V7_2"] | ["罗德唐", "Rodetan", "castle_village_V7_2"] |
| 326 | `geography.villages-rodobas` | 村庄 | （无新增） | ["罗多巴斯", "Rodobas", "村庄", "village_S1_1"] | ["罗多巴斯", "Rodobas", "village_S1_1"] |
| 327 | `geography.villages-rulund` | 村庄 | （无新增） | ["鲁兰德", "Rulund", "村庄", "village_V3_2"] | ["鲁兰德", "Rulund", "village_V3_2"] |
| 328 | `geography.villages-safna` | 村庄 | （无新增） | ["萨夫纳", "Safna", "村庄", "village_S2_1"] | ["萨夫纳", "Safna", "village_S2_1"] |
| 329 | `geography.villages-sagolina` | 村庄 | （无新增） | ["萨戈利那", "Sagolina", "村庄", "castle_village_ES3_2"] | ["萨戈利那", "Sagolina", "castle_village_ES3_2"] |
| 330 | `geography.villages-sagora` | 村庄 | （无新增） | ["萨戈拉", "Sagora", "村庄", "village_ES4_1"] | ["萨戈拉", "Sagora", "village_ES4_1"] |
| 331 | `geography.villages-sahel` | 村庄 | （无新增） | ["萨赫勒", "Sahel", "村庄", "castle_village_A2_1"] | ["萨赫勒", "Sahel", "castle_village_A2_1"] |
| 332 | `geography.villages-saldannis` | 村庄 | （无新增） | ["萨尔丹尼斯", "Saldannis", "村庄", "village_ES6_1"] | ["萨尔丹尼斯", "Saldannis", "village_ES6_1"] |
| 333 | `geography.villages-samatha` | 村庄 | （无新增） | ["萨马塔", "Samatha", "村庄", "village_EN5_4"] | ["萨马塔", "Samatha", "village_EN5_4"] |
| 334 | `geography.villages-savinth` | 村庄 | （无新增） | ["萨万特", "Savinth", "村庄", "village_V7_1"] | ["萨万特", "Savinth", "village_V7_1"] |
| 335 | `geography.villages-seordas` | 村庄 | （无新增） | ["肖尔达斯", "Seordas", "村庄", "castle_village_B8_2"] | ["肖尔达斯", "Seordas", "castle_village_B8_2"] |
| 336 | `geography.villages-sestadaim` | 村庄 | （无新增） | ["塞斯塔代姆", "Sestadaim", "村庄", "castle_village_ES6_1"] | ["塞斯塔代姆", "Sestadaim", "castle_village_ES6_1"] |
| 337 | `geography.villages-shapeshte` | 村庄 | （无新增） | ["沙佩什特", "Shapeshte", "村庄", "village_K3_1"] | ["沙佩什特", "Shapeshte", "village_K3_1"] |
| 338 | `geography.villages-shibal-zumr` | 村庄 | （无新增） | ["希巴勒·祖姆尔", "Shibal Zumr", "村庄", "castle_village_A6_1"] | ["希巴勒·祖姆尔", "Shibal Zumr", "castle_village_A6_1"] |
| 339 | `geography.villages-simira` | 村庄 | （无新增） | ["西米拉", "Simira", "村庄", "castle_village_K7_1"] | ["西米拉", "Simira", "castle_village_K7_1"] |
| 340 | `geography.villages-sirindac` | 村庄 | （无新增） | ["西岚达克", "Sirindac", "村庄", "castle_village_V5_2"] | ["西岚达克", "Sirindac", "castle_village_V5_2"] |
| 341 | `geography.villages-skorin` | 村庄 | （无新增） | ["斯科林", "Skorin", "村庄", "village_S3_2"] | ["斯科林", "Skorin", "village_S3_2"] |
| 342 | `geography.villages-spotia` | 村庄 | （无新增） | ["斯珀提亚", "Spotia", "村庄", "village_ES6_2"] | ["斯珀提亚", "Spotia", "village_ES6_2"] |
| 343 | `geography.villages-stathymos` | 村庄 | （无新增） | ["斯塔堤摩斯", "Stathymos", "村庄", "village_EN1_2"] | ["斯塔堤摩斯", "Stathymos", "village_EN1_2"] |
| 344 | `geography.villages-swenryn` | 村庄 | （无新增） | ["斯温林", "Swenryn", "村庄", "village_B4_4"] | ["斯温林", "Swenryn", "village_B4_4"] |
| 345 | `geography.villages-syratos` | 村庄 | （无新增） | ["叙拉托斯", "Syratos", "村庄", "castle_village_EN8_1"] | ["叙拉托斯", "Syratos", "castle_village_EN8_1"] |
| 346 | `geography.villages-takor` | 村庄 | （无新增） | ["塔科尔", "Takor", "村庄", "castle_village_S6_1"] | ["塔科尔", "Takor", "castle_village_S6_1"] |
| 347 | `geography.villages-talivel` | 村庄 | （无新增） | ["塔利维尔", "Talivel", "村庄", "castle_village_V7_1"] | ["塔利维尔", "Talivel", "castle_village_V7_1"] |
| 348 | `geography.villages-tamnuh` | 村庄 | （无新增） | ["坦姆努", "Tamnuh", "村庄", "castle_village_A8_1"] | ["坦姆努", "Tamnuh", "castle_village_A8_1"] |
| 349 | `geography.villages-tarcutis` | 村庄 | （无新增） | ["塔耳库提斯", "Tarcutis", "村庄", "castle_village_EW3_2"] | ["塔耳库提斯", "Tarcutis", "castle_village_EW3_2"] |
| 350 | `geography.villages-tasheba` | 村庄 | （无新增） | ["塔舍巴", "Tasheba", "村庄", "village_A1_1"] | ["塔舍巴", "Tasheba", "village_A1_1"] |
| 351 | `geography.villages-tegresos` | 村庄 | （无新增） | ["泰格瑞索斯", "Tegresos", "村庄", "village_ES1_3"] | ["泰格瑞索斯", "Tegresos", "village_ES1_3"] |
| 352 | `geography.villages-tememos` | 村庄 | （无新增） | ["特美摩斯", "Tememos", "村庄", "castle_village_EN8_2"] | ["特美摩斯", "Tememos", "castle_village_EN8_2"] |
| 353 | `geography.villages-tepes` | 村庄 | （无新增） | ["泰佩斯", "Tepes", "村庄", "castle_village_K4_1"] | ["泰佩斯", "Tepes", "castle_village_K4_1"] |
| 354 | `geography.villages-tevea` | 村庄 | （无新增） | ["特维亚", "Tevea", "村庄", "village_ES3_2"] | ["特维亚", "Tevea", "village_ES3_2"] |
| 355 | `geography.villages-themys` | 村庄 | （无新增） | ["忒密斯", "Themys", "村庄", "castle_village_EN4_2"] | ["忒密斯", "Themys", "castle_village_EN4_2"] |
| 356 | `geography.villages-thersenion` | 村庄 | （无新增） | ["忒耳塞尼翁", "Thersenion", "村庄", "village_EW6_3"] | ["忒耳塞尼翁", "Thersenion", "village_EW6_3"] |
| 357 | `geography.villages-thorios` | 村庄 | （无新增） | ["托里俄斯", "Thorios", "村庄", "castle_village_EW2_1"] | ["托里俄斯", "Thorios", "castle_village_EW2_1"] |
| 358 | `geography.villages-thractorae` | 村庄 | （无新增） | ["色雷刻托", "Thractorae", "村庄", "castle_village_EW4_1"] | ["色雷刻托", "Thractorae", "castle_village_EW4_1"] |
| 359 | `geography.villages-tirby` | 村庄 | （无新增） | ["蒂尔比", "Tirby", "村庄", "castle_village_V5_1"] | ["蒂尔比", "Tirby", "castle_village_V5_1"] |
| 360 | `geography.villages-tismil` | 村庄 | （无新增） | ["帖斯密勒", "Tismil", "村庄", "village_K2_2"] | ["帖斯密勒", "Tismil", "village_K2_2"] |
| 361 | `geography.villages-tor-leiad` | 村庄 | （无新增） | ["托·莱阿德", "Tor Leiad", "村庄", "village_B3_3"] | ["托·莱阿德", "Tor Leiad", "village_B3_3"] |
| 362 | `geography.villages-tor-melina` | 村庄 | （无新增） | ["托·梅利纳", "Tor Melina", "村庄", "castle_village_B3_2"] | ["托·梅利纳", "Tor Melina", "castle_village_B3_2"] |
| 363 | `geography.villages-tubilis` | 村庄 | （无新增） | ["突比力斯", "Tubilis", "村庄", "castle_village_A1_1"] | ["突比力斯", "Tubilis", "castle_village_A1_1"] |
| 364 | `geography.villages-ulaan` | 村庄 | （无新增） | ["乌兰", "Ulaan", "村庄", "village_K1_2"] | ["乌兰", "Ulaan", "village_K1_2"] |
| 365 | `geography.villages-uqba` | 村庄 | （无新增） | ["乌格巴", "Uqba", "村庄", "castle_village_A7_1"] | ["乌格巴", "Uqba", "castle_village_A7_1"] |
| 366 | `geography.villages-urikskala` | 村庄 | （无新增） | ["乌里克斯卡拉", "Urikskala", "村庄", "castle_village_S7_1"] | ["乌里克斯卡拉", "Urikskala", "castle_village_S7_1"] |
| 367 | `geography.villages-urunjan` | 村庄 | （无新增） | ["乌伦占", "Urunjan", "村庄", "village_K4_4"] | ["乌伦占", "Urunjan", "village_K4_4"] |
| 368 | `geography.villages-usanc` | 村庄 | （无新增） | ["于桑克", "Usanc", "村庄", "castle_village_V1_1"] | ["于桑克", "Usanc", "castle_village_V1_1"] |
| 369 | `geography.villages-usek` | 村庄 | （无新增） | ["乌赛克", "Usek", "村庄", "castle_village_K1_1"] | ["乌赛克", "Usek", "castle_village_K1_1"] |
| 370 | `geography.villages-ustokol` | 村庄 | （无新增） | ["乌斯托科", "Ustokol", "村庄", "castle_village_S1_1"] | ["乌斯托科", "Ustokol", "castle_village_S1_1"] |
| 371 | `geography.villages-uthelaim` | 村庄 | （无新增） | ["乌瑟莱姆", "Uthelaim", "村庄", "castle_village_B8_1"] | ["乌瑟莱姆", "Uthelaim", "castle_village_B8_1"] |
| 372 | `geography.villages-valanby` | 村庄 | （无新增） | ["瓦朗比", "Valanby", "村庄", "castle_village_V3_2"] | ["瓦朗比", "Valanby", "castle_village_V3_2"] |
| 373 | `geography.villages-varagos` | 村庄 | （无新增） | ["瓦拉戈斯", "Varagos", "村庄", "castle_village_EN1_1"] | ["瓦拉戈斯", "Varagos", "castle_village_EN1_1"] |
| 374 | `geography.villages-vargornis` | 村庄 | （无新增） | ["瓦耳戈尼斯", "Vargornis", "村庄", "village_ES7_2"] | ["瓦耳戈尼斯", "Vargornis", "village_ES7_2"] |
| 375 | `geography.villages-vathea` | 村庄 | （无新增） | ["瓦忒亚", "Vathea", "村庄", "castle_village_EW8_2"] | ["瓦忒亚", "Vathea", "castle_village_EW8_2"] |
| 376 | `geography.villages-vealos` | 村庄 | （无新增） | ["威亚罗斯", "Vealos", "村庄", "village_EN5_1"] | ["威亚罗斯", "Vealos", "village_EN5_1"] |
| 377 | `geography.villages-verecsand` | 村庄 | （无新增） | ["韦雷克桑", "Verecsand", "村庄", "castle_village_V8_1"] | ["韦雷克桑", "Verecsand", "castle_village_V8_1"] |
| 378 | `geography.villages-veron` | 村庄 | （无新增） | ["维戎", "Veron", "村庄", "castle_village_EW5_1"] | ["维戎", "Veron", "castle_village_EW5_1"] |
| 379 | `geography.villages-vesin` | 村庄 | （无新增） | ["韦桑", "Vesin", "村庄", "village_V7_2"] | ["韦桑", "Vesin", "village_V7_2"] |
| 380 | `geography.villages-vinela` | 村庄 | （无新增） | ["维尼拉", "Vinela", "村庄", "village_EW4_3"] | ["维尼拉", "Vinela", "village_EW4_3"] |
| 381 | `geography.villages-visibrot` | 村庄 | （无新增） | ["维西布罗特", "Visibrot", "村庄", "village_S5_1"] | ["维西布罗特", "Visibrot", "village_S5_1"] |
| 382 | `geography.villages-vladiv` | 村庄 | （无新增） | ["弗拉基夫", "Vladiv", "村庄", "castle_village_S8_1"] | ["弗拉基夫", "Vladiv", "castle_village_S8_1"] |
| 383 | `geography.villages-wadar` | 村庄 | （无新增） | ["瓦达尔", "Wadar", "村庄", "castle_village_A9_2"] | ["瓦达尔", "Wadar", "castle_village_A9_2"] |
| 384 | `geography.villages-waltas` | 村庄 | （无新增） | ["瓦勒塔斯", "Waltas", "村庄", "village_A5_3"] | ["瓦勒塔斯", "Waltas", "village_A5_3"] |
| 385 | `geography.villages-yangutum` | 村庄 | （无新增） | ["扬古图姆", "Yangutum", "村庄", "village_S4_4"] | ["扬古图姆", "Yangutum", "village_S4_4"] |
| 386 | `geography.villages-zalm` | 村庄 | （无新增） | ["扎勒姆", "Zalm", "村庄", "village_A7_4"] | ["扎勒姆", "Zalm", "village_A7_4"] |
| 387 | `geography.villages-zeocorys` | 村庄 | （无新增） | ["泽俄科律斯", "Zeocorys", "村庄", "village_EW2_4"] | ["泽俄科律斯", "Zeocorys", "village_EW2_4"] |
| 388 | `geography.villages-zestea` | 村庄 | （无新增） | ["泽斯特亚", "Zestea", "村庄", "village_ES3_3"] | ["泽斯特亚", "Zestea", "village_ES3_3"] |
| 389 | `geography.villages-zhemyan` | 村庄 | （无新增） | ["哲米扬", "Zhemyan", "村庄", "castle_village_S1_2"] | ["哲米扬", "Zhemyan", "castle_village_S1_2"] |
