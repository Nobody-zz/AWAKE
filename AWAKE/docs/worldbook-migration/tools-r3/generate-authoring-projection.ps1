# Authoring 投影生成器 — W1.5 来源登记 + W2/W3/W4 YAML 产出
# 输入：r3-revision\documents\*.r3.json（唯一权威输入）+ v1 variant 原文 dump + 两登记表
# 输出：workspace\authoring\sources\*.txt|*.yaml（来源登记）+ projection\authoring-out\*.yaml（12 档）
# 全部档案 needs_review；quote_hash=Sha256Text(quote)；引文逐字校验（必须在 variant 原文中）。
$ErrorActionPreference = 'Stop'
$DocsDir   = 'D:\AWAKE-Archive\worldbook-migration-content\semantic-rewrite-batch-download-20260903\r3-revision\documents'
$DumpFile  = 'D:\AWAKE-Archive\variant-texts-dump.txt'
$PlanDir   = 'D:\AWAKE-Dev\AWAKE\docs\worldbook-studio-plan'
$OutYaml   = 'D:\AWAKE-Dev\AWAKE\docs\worldbook-migration\projection\authoring-out'
$OutSrc    = 'D:\AWAKE-Dev\AWAKE\docs\worldbook-migration\projection\sources-out'
$WsSources = 'D:\AWAKE-Dev\AWAKE\tools\worldbook-studio\workspace\authoring\sources'

# ---------- variant 原文 ----------
$variants = @{}
$cur = $null
foreach ($line in [IO.File]::ReadAllLines($DumpFile)) {
    if ($line -like '@@@ *') { $cur = $line.Substring(4).Trim(); $variants[$cur] = ''; }
    elseif ($cur -and $line.Trim()) { $variants[$cur] += $line }
}
function V([string]$key) { return $variants[$key] }

# ---------- claim hex ID → 语义键 ----------
$KEY = @{
'2da527808a3b5f26b6bc4690'='dawn-loc';'dc592556b2e4c247d50cd40a'='dawn-foot';'1c70c804f9d583f4e8d33c96'='dawn-edge';'8e71486bbda76901237134dd'='dawn-taboo';'17ddf9219131f13632e45320'='dawn-legend';'8b0d5629862f3c72dad1c7f1'='dawn-stew';'fd78df11d0b49237fa8d34dd'='dawn-ctrl';
'0d2520893b3a115035a9a1a0'='lac-loc';'80fd2b2477dad8dd01375064'='lac-riv';'c9ff734b8dd6d7968fb1a954'='lac-nav';'c4672cffe65c5125bb1d00e7'='lac-live';'d2c7e2dc921eafdf047a31c8'='lac-trade';'e1158ae544d37d2f31aeed53'='lac-imp';'7707292541174752ca88df88'='lac-rumor';'c829e454ee3264a31fe6c451'='lac-rust';
'3b9d7b54dab5b10399c69bcf'='sara-bay';'59cc0718f038c2b4a0c3e0a6'='sara-isles';'4d959427d53c21b06f4b7db8'='sara-port';'b3f620e228052b0073d624b5'='sara-strait';'2bd862c0fc76a6b093c72d40'='sara-cradle';'cf3972ba2c60a3234be103d3'='sara-land';'7889b88429c064b45cd4a68b'='sara-tales';
'1ddf7e7efeaa259eff44a5bf'='kach-pos';'bb010f8c526d3be41857a8c7'='kach-terra';'a48e49e8bc9c8d3cc77b3bc2'='kach-harbor';'42d80bdb7b7cc690dd16e45a'='kach-war';'9dddd4ae617f78441f817f84'='kach-khu';'a1168db44a5367611e4677d3'='kach-succ';'9644824420c36a47d41eae1e'='kach-unres';'2a766fe18cf768014da363d2'='kach-bat';'29471fc14b7ee2c3e7b7be74'='kach-nor';'19d09a9134841be233413cb2'='kach-stu';'47fbec18d69a1cc10a9ed2c5'='kach-conf';
'aaffcbbc1deebd4394117f51'='der-vill';'575e2811c363e513120d23f7'='der-furs';'6fde897b4e66fee72843b855'='der-oil';'57e81a172cc6d39dbebffae4'='der-trade';'8578161db710e16e0c4ec922'='der-cloak';'d4853fc7e987012ee50ace18'='der-lamp';'a2958bbb9490ce195a755e68'='der-road'
}
# ---------- 引文表（doc_key/claim_key → variant + 逐字引文） ----------
$Q = @{
 'lac-lake|lac-loc'   = @{v='拉科尼斯湖 V2'; q='拉科尼斯湖是卡拉迪亚大陆北部最大、最深的封闭型内陆淡水湖'}
 'lac-lake|lac-riv'   = @{v='拉科尼斯湖 V2'; q='两大注入河流——弥戎河与喀拉卡兹河——分别从南方和东北方汇入'}
 'lac-lake|lac-nav'   = @{v='拉科尼斯湖 V0'; q='这湖冬天不冻，这是最要紧的事'}
 'lac-lake|lac-live'  = @{v='拉科尼斯湖 V0'; q='渔民、船工、码头上扛货的，全指着这片水'}
 'lac-lake|lac-trade' = @{v='拉科尼斯湖 V0'; q='把帝国的麦子往斯特吉亚的蒂亚尔运，再把斯特吉亚的毛皮往回拉'}
 'lac-lake|lac-imp'   = @{v='拉科尼斯湖 V1'; q='谁控制了湖上的舰队，谁就能在冬季掌握战略主动'}
 'lac-tales|lac-rumor'= @{v='拉科尼斯湖 V0'; q='至于那个什么德律亚人的传说——说湖水以前是红的，染过血——老辈人讲起来就沉默'}
 'lac-tales|lac-rust' = @{v='拉科尼斯湖 V2'; q='浅滩区域确实存在高浓度的铁质氧化层，这可能是浅水区有机质大量腐烂后形成的自然沉积'}
 'sara-bay|sara-bay'  = @{v='沙拉斯湾 V2'; q='沙拉斯湾是西大洋深入卡拉迪亚大陆西海岸的一个半封闭海域'}
 'sara-bay|sara-isles'= @{v='沙拉斯湾 V2'; q='南部密集的群岛形成了一道天然的防波屏障，有效削弱了大洋传来的长涌浪'}
 'sara-bay|sara-strait'= @{v='沙拉斯湾 V2'; q='加隆托海峡的潮流流速较快，是湾内外水体交换的主要通道，也是航运的关键节点'}
 'sara-bay|sara-port' = @{v='沙拉斯湾 V2'; q='北部沙拉斯港的持续繁荣正是得益于这一水文庇护'}
 'sara-tales|sara-cradle' = @{v='沙拉斯湾 V1'; q='这句话在帝国的史书里写着，在元老院的颂词里念着'}
 'sara-tales|sara-land'   = @{v='沙拉斯湾 V2'; q='关于卡拉德人经由这些岛屿渡海登陆的传说，在考古学和移民史领域尚未得到确证'}
 'sara-tales|sara-tales'  = @{v='沙拉斯湾 V0'; q='老一辈跑船的说那些岛有古怪——雾天的时候岛的形状会变，罗盘靠近某些礁石会乱转'}
 'kach-land|kach-pos'   = @{v='卡恰尔半岛 V0'; q='卡恰尔半岛是斯特吉亚东边一块从伊卡拉荒原延伸出去的狭长陆地'}
 'kach-land|kach-terra' = @{v='卡恰尔半岛 V0'; q='它的地形怪石嶙峋，海崖陡峭'}
 'kach-land|kach-harbor'= @{v='卡恰尔半岛 V0'; q='适合建港口的地方不多'}
 'kach-land|kach-war'   = @{v='卡恰尔半岛 V0'; q='一旦占住了，就能同时扼住北边诺德人的比尔里海和南边巴旦尼亚人的海域'}
 'kach-land|kach-khu'   = @{v='卡恰尔半岛 V7'; q='地面全是石头和海崖，马跑不起来'}
 'kach-own|kach-succ'   = @{v='卡恰尔半岛 V8'; q='这片半岛历史上换了三次主人'}
 'kach-own|kach-unres'  = @{v='卡恰尔半岛 V8'; q='据说当年诺德人先占了那里，巴旦尼亚人雇斯特吉亚人把他们赶走，结果斯特吉亚人自己留下了'}
 'kach-tales|kach-bat'  = @{v='卡恰尔半岛 V5'; q='戴恩玛雇了斯特吉亚人来清理门户，结果斯特吉亚人打完仗不走了，把整片半岛都吞了'}
 'kach-tales|kach-nor'  = @{v='卡恰尔半岛 V4'; q='巴旦尼亚人耍了花招，雇了斯特吉亚的斧头来赶我们'}
 'kach-tales|kach-stu'  = @{v='卡恰尔半岛 V1'; q='他们互相咬，最后把肉送到了我们嘴里'}
 'kach-tales|kach-conf' = @{v='卡恰尔半岛 V7'; q='听说那片半岛的历史全是背叛和趁火打劫'}
 'dawn-mtn|dawn-loc'  = @{v='黎明山脉 V0'; q='黎明山脉，全称柯希·罗希尼——黎明山脉，坐落于德夫赛格高原东缘'}
 'dawn-mtn|dawn-edge' = @{v='黎明山脉 V0'; q='山脉主体呈南北走向'}
 'dawn-mtn|dawn-foot' = @{v='黎明山脉 V0'; q='山麓地带则覆盖着茂密的针叶林与高山草甸'}
 'dawn-taboo|dawn-legend' = @{v='黎明山脉 V0'; q='相传阿赫哈克的双肩生有蝮蛇，其御所即建于山脉深处'}
 'dawn-taboo|dawn-taboo'  = @{v='黎明山脉 V0'; q='山脉核心区域历来对外来者封闭，任何未经允许的擅入均被视为对山灵与先祖的严重亵渎'}
 'dawn-stew|dawn-stew' = @{v='黎明山脉 V0'; q='在库赛特汗国西征之后，黎明山脉地区被划归合儿必特部管辖'}
 'dawn-stew|dawn-ctrl' = @{v='黎明山脉 V1'; q='这片山划给了合儿必特部'}
 'der-vill|der-vill' = @{v='德里亚特 V0'; q='德里亚特是卡琉斯堡附近的一座村庄，位于瓦尔切格湾与埃博半岛的山脊之间'}
 'der-furs|der-furs' = @{v='德里亚特 V0'; q='当地村民在山上捕获河狸与水貂，有时也会在海湾水域捉海豹，毛皮和油脂是他们的主要产出来源'}
 'der-furs|der-oil'  = @{v='德里亚特 V0'; q='毛皮和油脂是他们的主要产出来源'}
 'der-furs|der-trade'= @{v='德里亚特 V2'; q='德里亚特的海豹皮在瓦尔切格湾一带算是一绝'}
 'der-furs|der-cloak'= @{v='德里亚特 V1'; q='瓦尔切格湾那一带的海豹皮挺值钱，又厚又滑，做斗篷防水。卡琉斯堡的守军冬天穿的皮袄，有些料子就是从德里亚特收来的'}
 'der-furs|der-lamp' = @{v='德里亚特 V3'; q='那里的海豹油点灯特别好，烟少、火亮'}
 'der-furs|der-road' = @{v='德里亚特 V2'; q='不过那地方路不好走，在山脊和海湾之间，进去一趟得费不少功夫'}
}

# ---------- 断言文本（多 claim span 拆句 / layer span 用中性命题表述） ----------
$AT = @{
 'lac-loc'='拉科尼斯湖在卡拉迪亚北部，四面为山地与森林环抱，是一大片封闭的内陆淡水湖。'
 'lac-riv'='弥戎河与喀拉卡兹河从不同方向注入，把它接进北方的内河航运。'
 'lac-live'='湖岸的营生三样：打鱼、行船、码头扛活。'
 'lac-imp'='帝国兵志叙述将冬季航运视为北境防务与运兵之利。'
 'lac-rumor'='湖水变红的德律亚传说被叙述为湖区战争记忆的一部分，没有凭据。'
 'lac-rust'='走过北境的学士对浅滩锈色持有自然来由的解释，保留原文或然口气。'
 'sara-cradle'='帝国历史传统把沙拉斯湾解释为卡拉德先祖登陆的摇篮。'
 'sara-land'='先祖从群岛登陆的故事被标记为尚未得到考古与移民史确证的传说。'
 'sara-tales'='群岛的雾、罗盘和石刻故事属于船员传闻。'
 'kach-war'='卡恰尔半岛被部分叙述赋予设哨望海、把扼水路的战略用途。'
 'kach-khu'='库赛特叙述判断卡恰尔半岛地形不利于骑兵展开。'
 'kach-bat'='巴旦尼亚叙述将易主归因于借斯特吉亚人清除诺德人后反失其地。'
 'kach-nor'='诺德叙述将易主归因于巴旦尼亚人设局、雇军反噬。'
 'kach-stu'='斯特吉亚叙述将易主归因于两方相争而坐收其成。'
 'kach-conf'='三方易主叙述彼此存在不同解释，互不对账。'
 'dawn-legend'='阿赫哈克、黑魔法和古老仪式的故事属于当地传说与信仰叙述。'
 'dawn-taboo'='山脉腹地与山民习俗形成未经允许不得进入的社会边界。'
 'dawn-stew'='合儿必特部进入山地被来源解释为通过通婚、共俗和守护者身份融入当地。'
 'der-road'='德里亚特道路被商旅叙述描述为进入费功夫，运出成本未经直接确证。'
}

# ---------- grants 映射（layer span → profile/scope/min_detail） ----------
$G = @{
 'lac-lake|lac-imp'=@{p='profile.noble_high_steward';s='elite';m='detail'}
 'lac-tales|lac-rumor'=@{p='profile.villager';s='local';m='summary'}
 'lac-tales|lac-rust'=@{p='profile.notable';s='regional';m='detail'}
 'sara-tales|sara-cradle'=@{p='profile.notable';s='regional';m='detail'}
 'sara-tales|sara-land'=@{p='profile.notable';s='regional';m='summary'}
 'sara-tales|sara-tales'=@{p='profile.villager';s='local';m='summary'}
 'kach-land|kach-war'=@{p='profile.townsfolk';s='regional';m='detail'}
 'kach-land|kach-khu'=@{p='profile.merchant';s='national';m='summary'}
 'kach-tales|kach-bat'=@{p='profile.townsfolk';s='regional';m='detail'}
 'kach-tales|kach-nor'=@{p='profile.townsfolk';s='regional';m='detail'}
 'kach-tales|kach-stu'=@{p='profile.townsfolk';s='regional';m='detail'}
 'kach-tales|kach-conf'=@{p='profile.townsfolk';s='regional';m='detail'}
 'dawn-taboo|dawn-legend'=@{p='profile.villager';s='local';m='summary'}
 'dawn-taboo|dawn-taboo'=@{p='profile.villager';s='local';m='detail'}
 'dawn-stew|dawn-stew'=@{p='profile.notable';s='regional';m='detail'}
 'der-furs|der-road'=@{p='profile.merchant';s='regional';m='detail'}
}

# ---------- 档案级参数（slug/domain/subdomain/era/summary） ----------
$META = @{
 'lac-lake' = @{id='doc.geography.lakonis-lake';domain='geography';sub='terrain';era='current';cert='bounded';sum='拉科尼斯湖的自然形态、水系连接、冬季通航与沿岸生计、转运贸易，附帝国兵志的战略解读（带立场标记）。'}
 'lac-tales' = @{id='doc.culture.lakonis-lake-tales';domain='culture';sub='faith';era='historical';cert='unknown';sum='湖水变红的德律亚传说与浅滩锈色的学士解释：两种来由说法并置，均不作定论。'}
 'sara-bay' = @{id='doc.geography.shalas-bay';domain='geography';sub='sea_routes';era='current';cert='bounded';sum='沙拉斯湾的半封闭水域形态、群岛防波、加隆托海峡要道与沙拉斯港生计。'}
 'sara-tales' = @{id='doc.culture.shalas-origin-tales';domain='culture';sub='faith';era='historical';cert='unknown';sum='帝国“摇篮”史称、先祖登陆传说（未经确证）与船人怪谈：三种口径分层记录。'}
 'kach-land' = @{id='doc.geography.kachar-peninsula';domain='geography';sub='terrain';era='current';cert='bounded';sum='卡恰尔半岛的位置地形、港位稀少、用兵价值与库赛特远距判断。'}
 'kach-own' = @{id='doc.politics.kachar-ownership';domain='politics';sub='territories';era='historical';cert='approximate';sum='半岛先后由三方控制的归属沿革；易主次序与当前归属明确保持未决。'}
 'kach-tales' = @{id='doc.culture.kachar-three-tales';domain='culture';sub='identity';era='historical';cert='unknown';sum='巴旦尼亚、诺德、斯特吉亚三方对半岛易主的各自讲法，互不对账。'}
 'dawn-mtn' = @{id='doc.geography.dawn-mountains';domain='geography';sub='terrain';era='current';cert='bounded';sum='黎明山脉的位置、全称、南北走向与高原东缘边界属性、山麓植被。'}
 'dawn-taboo' = @{id='doc.culture.dawn-taboo';domain='culture';sub='faith';era='current';cert='approximate';sum='阿赫哈克传说与黑魔法信仰叙述，及由此形成的禁入社会边界。'}
 'dawn-stew' = @{id='doc.politics.dawn-stewardship';domain='politics';sub='territories';era='historical';cert='approximate';sum='合儿必特部西征后进入山地并被解释为融入守护的沿革；当前实际控制未决。'}
 'der-vill' = @{id='doc.geography.deriat-village';domain='geography';sub='settlements';era='current';cert='bounded';sum='德里亚特村庄的位置：卡琉斯堡附近、瓦尔切格湾与埃博半岛山脊之间。'}
 'der-furs' = @{id='doc.economy.deriat-furs';domain='economy';sub='trade';era='current';cert='bounded';sum='德里亚特的毛皮油脂产出、交易去向、斗篷皮袄用途、海豹油点灯与道路不便的商旅解读。'}
}

# ---------- kind 映射 ----------
function MapKind([string]$k) {
    switch ($k) { 'source_fact' {'fact'} 'relationship' {'relation'} 'state' {'state'} 'interpretation' {'interpretation'} 'rumor' {'rumor'} 'unresolved' {'interpretation'} default { throw "unknown kind $k" } }
}

# ---------- 来源登记（5 个 rule 文件） ----------
$units = @('拉科尼斯湖','沙拉斯湾','卡恰尔半岛','黎明山脉','德里亚特')
$slugOf = @{ '拉科尼斯湖'='lakonis-lake'; '沙拉斯湾'='shalas-bay'; '卡恰尔半岛'='kachar-peninsula'; '黎明山脉'='dawn-mountains'; '德里亚特'='deriat-village' }
New-Item -ItemType Directory -Force -Path $OutSrc, $OutYaml, $WsSources | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead('D:\AWAKE-Archive\worldbook-v1_20260911.zip')
$sourceInfo = @{}
foreach ($u in $units) {
    $entry = $zip.Entries | Where-Object { $_.FullName -match ('rules.*rule_' + $u) } | Select-Object -First 1
    $sr = New-Object IO.StreamReader($entry.Open(), [Text.Encoding]::UTF8)
    $raw = $sr.ReadToEnd(); $sr.Close()
    $raw = $raw.Replace("`r`n", "`n")
    $fileName = 'chronicle-' + $slugOf[$u] + '.txt'
    $bytes = [Text.Encoding]::UTF8.GetBytes($raw)
    [IO.File]::WriteAllBytes((Join-Path $OutSrc $fileName), $bytes)
    [IO.File]::WriteAllBytes((Join-Path $WsSources $fileName), $bytes)
    $fileHash = ([BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($bytes)) -replace '-','').ToLower()
    $sourceInfo[$u] = @{ file = $fileName; hash = $fileHash; source_id = 'source.calradia.chronicle.' + $slugOf[$u] }
    $reg = [ordered]@{
        source_id = $sourceInfo[$u].source_id
        source_version = 'download-20260903'
        source_nature = 'chronicle'
        universe = 'awake_current'
        era = 'historical'
        locator_root = $fileName
        source_content_hash = $fileHash
        content_tier = 'base'
        license_status = 'permitted'
        use_status = 'active'
        valid_until = $null
        imported_at = '2026-09-12T00:00:00Z'
        normalization_version = 'utf8-lf-no-bom-v1'
    }
    $regPath = Join-Path $WsSources ('source-' + $slugOf[$u] + '.yaml')
    $yaml = ($reg.GetEnumerator() | ForEach-Object { if ($null -eq $_.Value) { "$($_.Key): null" } else { "$($_.Key): $($_.Value)" } }) -join "`n"
    [IO.File]::WriteAllText($regPath, $yaml + "`n", (New-Object Text.UTF8Encoding $false))
}
$zip.Dispose()
Write-Host 'SOURCES REGISTERED (staged + workspace): 5'

# ---------- 生成 12 档 YAML ----------
$bind = Get-Content 'D:\AWAKE-Dev\AWAKE\docs\worldbook-migration\projection\ANCHOR-BINDING-PLAN-20260912.json' -Raw -Encoding UTF8 | ConvertFrom-Json
$profileReg = Get-Content (Join-Path $PlanDir 'profile-registry.v1.json') -Raw -Encoding UTF8
$refReg = Get-Content (Join-Path $PlanDir 'referral-registry.v1.json') -Raw -Encoding UTF8
function Sha([string]$s) { $b=[Text.Encoding]::UTF8.GetBytes($s); ([BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($b)) -replace '-','').ToUpper() }
$pHash = Sha $profileReg; $rHash = Sha $refReg
$regBinding = "registry_bindings:`n  profile_registry_version: 1.0.0`n  profile_registry_hash: $pHash`n  referral_registry_version: 1.0.0`n  referral_registry_hash: $rHash"

$count = 0
Get-ChildItem "$DocsDir\*.r3.json" | Sort-Object Name | ForEach-Object { try {
    $cand = [IO.File]::ReadAllText($_.FullName, [Text.Encoding]::UTF8) | ConvertFrom-Json
    $docKey = ($_.BaseName -replace '\.r3$','')
    $m = $META[$docKey]
    $unitName = ($cand.place_cluster)
    $si = $sourceInfo[$unitName]

    # claim 索引
    $claims = @{}
    foreach ($cl in $cand.claims) { $claims[$cl.claim_id] = $cl }
    $docSources = New-Object 'System.Collections.Generic.HashSet[string]'
    # span → (claim, layer) 已在候选中；断言=claim（40），表达=layer span（16）
    $assertLines = @()
    $aIdx = 0
    foreach ($sp in $cand.target_spans) {
        $isExpr = $sp.PSObject.Properties.Name -contains 'layer'
        foreach ($cid in $sp.claim_ids) {
            $cl = $claims[$cid]
            $short = $cid -replace '^authoring_provisional\.claim\.',''
            $sem = $KEY[$short]; if (-not $sem) { throw "unmapped claim $short" }; $qkey = "$docKey|$sem"
            if (-not $Q.ContainsKey($qkey)) { throw "missing quote for $qkey" }
            $qe = $Q[$qkey]
            $vtext = V $qe.v
            if (-not $vtext.Contains($qe.q)) { throw "quote not verbatim in $($qe.v): $($qe.q)" }
            $variantIdx = [int]($qe.v -replace '.* V','')
            $srcId = $si.source_id
            $locator = "$($si.file)#/Variants/$variantIdx/Content"
            $qHash = Sha $qe.q
            $srcEntry = "      - {source_id: $srcId, source_version: download-20260903, source_content_hash: $($si.hash), locator: $locator, quote_hash: $qHash, quote: `"$($qe.q)`"}"
            $text = if ($AT.ContainsKey($sem)) { $AT[$sem] } else { $cand.target_text.Substring($sp.text_start, $sp.text_end - $sp.text_start) }
            $aId = "assertion.$docKey-$($aIdx+1)"
            $assertLines += "  - id: $aId"
            $assertLines += "    revision: 1"
            $assertLines += "    kind: $(MapKind $cl.epistemic_kind)"
            $assertLines += "    text: {zh-CN: `"$text`"}"
            $assertLines += "    sources:"
            $assertLines += $srcEntry
            if ($isExpr) {
                $gr = $G[$qkey]
                $exprText = $cand.target_text.Substring($sp.text_start, $sp.text_end - $sp.text_start)
                $assertLines += "    expressions:"
                $assertLines += "      - id: expr.$docKey-$($aIdx+1)"
                $assertLines += "        revision: 1"
                $assertLines += "        layer: $($sp.layer)"
                $assertLines += "        text: {zh-CN: `"$exprText`"}"
                $assertLines += "        sources:"
                $assertLines += "          " + $srcEntry.TrimStart()
                $assertLines += "        grants: [{profile_id: $($gr.p), scope: $($gr.s), min_detail: $($gr.m)}]"
                $assertLines += "        denies: []"
                $null = $docSources.Add($srcEntry)
            } else {
                $assertLines += "    expressions:"
                $assertLines += "      - id: expr.$docKey-$($aIdx+1)-summary"
                $assertLines += "        revision: 1"
                $assertLines += "        layer: summary"
                $assertLines += "        text: {zh-CN: `"$text`"}"
                $assertLines += "        sources:"
                $assertLines += "          " + $srcEntry.TrimStart()
                $assertLines += "        grants: [{profile_id: profile.commoner, scope: regional, min_detail: summary}]"
                $assertLines += "        denies: []"
                $null = $docSources.Add($srcEntry)
            }
            $aIdx++
        }
    }
    $entityLine = ($bind.bindings | Where-Object { $_.doc_key -eq $docKey }).entity_ids -join ', '
    $srcBlock = ($docSources | Sort-Object | ForEach-Object { '  ' + $_.TrimStart() }) -join "`n"
    $yaml = @"
schema_version: awake.worldbook.authoring.v1
revision: 1
id: $($m.id)
title: {zh-CN: "$($cand.title)"}
status: needs_review
domain: $($m.domain)
subdomain: $($m.sub)
universe: awake_current
era: {key: $($m.era), certainty: $($m.cert)}
content_tier: base
entity_ids: [$entityLine]
summary: {zh-CN: "$($m.sum)"}
$regBinding
sources:
$($srcBlock)
authority: {owner: awake_canon, conflict_policy: canon_wins}
assertions:
$($assertLines -join "`n")
"@
    } catch { Write-Host ('ERR in ' + $_.InvocationInfo.PositionMessage); throw };    $path = Join-Path $OutYaml ($docKey + '.yaml')
    [IO.File]::WriteAllText($path, $yaml, (New-Object Text.UTF8Encoding $false))
    $count++
    Write-Host ("WROTE {0}  assertions={1}" -f $path, $aIdx)
}
Write-Host "DONE: $count authoring YAML files"
