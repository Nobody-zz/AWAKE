import { Database } from 'bun:sqlite'
import { existsSync, readFileSync } from 'node:fs'

const DEFAULT_DB = 'C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db'
const dbPath = process.env.BANNERSAGE_DB || DEFAULT_DB
const ZHS = 'CNs'
const ZHT = 'CNt'

if (!existsSync(dbPath)) {
  console.error('BANNERSAGE_DB not found:', dbPath)
  console.error('Set BANNERSAGE_DB to your BannerlordSage dist/games/bannerlord/bannerlord.db')
  process.exit(2)
}

const db = new Database(dbPath, { readonly: true })
const cache = new Map<string, string | null>()

function lookup(id: string, lang: string): string | null {
  const k = lang + ':' + id
  if (cache.has(k)) return cache.get(k)!
  const row = db.query('SELECT text FROM localization_entries WHERE stringId = $id AND language = $lang LIMIT 1').get({ $id: id, $lang: lang }) as any
  const v = row ? row.text : null
  cache.set(k, v)
  return v
}

function zh(id: string): string | null { return lookup(id, ZHS) ?? lookup(id, ZHT) }

const tokenRe = /\{=([A-Za-z0-9_.\-]+)\}/g
function humanize(s: string | null | undefined): string | null {
  if (!s) return s
  return s.replace(tokenRe, (m, id: string) => {
    const t = zh(id)
    return t != null && t.length ? t.trim() : m
  })
}

function resolveRefs(id: string): string[] {
  const res: string[] = []
  const base = '%' + id + '%'
  for (const r of db.query('SELECT heroId FROM bannerlord_heroes WHERE isTemplate = 0 AND text LIKE $i LIMIT 4').all({ $i: base }) as any[])
    res.push('hero ' + r.heroId)
  for (const r of db.query('SELECT clanId, name FROM bannerlord_clans WHERE name LIKE $i LIMIT 2').all({ $i: base }) as any[])
    res.push(`clan ${r.clanId} (${humanize(r.name)})`)
  for (const r of db.query('SELECT kingdomId, name, title, rulerTitle FROM bannerlord_kingdoms WHERE name LIKE $i OR title LIKE $i OR rulerTitle LIKE $i LIMIT 2').all({ $i: base }) as any[])
    res.push(`kingdom ${r.kingdomId} (${humanize(r.name)})`)
  for (const r of db.query('SELECT settlementId, name FROM bannerlord_settlements WHERE name LIKE $i LIMIT 2').all({ $i: base }) as any[])
    res.push(`settlement ${r.settlementId} (${humanize(r.name)})`)
  const ENT = ['Hero','Kingdom','Faction','Settlement','Culture','Town','Village','Castle','NPCCharacter']
  const params: any = { $i: base }
  const inList = ENT.map((v, i) => { params['$e' + i] = v; return '$e' + i }).join(',')
  for (const r of db.query(`SELECT entityType, entityId, name FROM xml_entities WHERE name LIKE $i AND entityType IN (${inList}) LIMIT 4`).all(params) as any[])
    res.push(`xml ${r.entityType} ${r.entityId} (${humanize(r.name)})`)
  return [...new Set(res)]
}

const cjkRe = /[\u4e00-\u9fff\u3400-\u4dbf]/

function resolveName(ref: string): string {
  const target = (ref || '').trim()
  if (!target) return ''
  let id = target
  let kinds: string[] = []
  if (/^Hero\./.test(target)) { id = target.slice('Hero.'.length); kinds = ['NPCCharacter', 'Hero'] }
  else if (/^Faction\./.test(target)) { id = target.slice('Faction.'.length); kinds = ['Faction'] }
  else if (/^Kingdom\./.test(target)) { id = target.slice('Kingdom.'.length); kinds = ['Kingdom'] }
  else if (/^Settlement\./.test(target)) { id = target.slice('Settlement.'.length); kinds = ['Settlement', 'Town', 'Castle', 'Village'] }
  else if (/^Culture\./.test(target)) { id = target.slice('Culture.'.length); kinds = ['Culture'] }
  else if (/^clan_/.test(id)) kinds = ['Faction', 'Clan']
  else if (/^(town|castle|village)_/.test(id)) kinds = ['Settlement']
  else if (/^(lord|dead|lady|companion|hero|girl|banner|rebel|char_|npc_name)_/.test(id)) kinds = ['NPCCharacter', 'Hero']
  else kinds = ['Kingdom', 'Culture', 'Faction', 'Settlement']
  for (const kind of kinds) {
    const r = db.query('SELECT name FROM xml_entities WHERE entityId = $id AND entityType = $kind LIMIT 1').get({ $id: id, $kind: kind }) as any
    if (!r?.name) continue
    const h = humanize(r.name) || ''
    const run = /[\u3400-\u9fff0-9，。；、：·“”‘’（）\s]+/.exec(h)
    const name = run ? run[0].trim() : h.trim()
    if (name) return name
  }
  return id
}

function heroNameOf(ref: string): string | null { return resolveName(ref) }

const usage = `
BannerlordSage 本地取证查询器（persona-workbench 数据源，只读）

用法:  bun bannersage-query.ts <kind> <arg>     [可用，也可通过 ps1 包装调用]

  hero        <heroId>        例 lord_6_1 → 归属/亲属/生平(中)
  clan        <clanId>        例 clan_khuzait_1 → 家族/owner/文化/superFaction
  kingdom     <kingdomId>     例 khuzait → 王国/称号/owner/文化
  culture     <cultureId>     例 khuzait → 文化名/描述
  settlement  <settlementId>  例 town_K3 → 城/owner/文化/类型
  localize    <stringId>      例 CzM6y3MT → CNs/CNt 译文(查官方中文译名)
  search      <fragment>      在 hero正文/clan/kingdom/settlement/xml 实体里模糊查
`

function emit(title: string, pairs: [string, any][]) {
  console.log(`== ${title} ==`)
  if (!pairs.length) { console.log('  (no rows)'); return }
  for (const [k, v] of pairs) {
    if (v === null || v === undefined || v === '') continue
    console.log(`  ${k}: ${String(v)}`)
  }
}

const kind = process.argv[2]
const arg = process.argv[3]

if (!kind || !arg) { console.log(usage); process.exit(1) }

if (kind === 'hero') {
  const r = db.query('SELECT * FROM bannerlord_heroes WHERE heroId = $id').get({ $id: arg }) as any
  if (!r) { console.log('hero not found:', arg); process.exit(0) }
  emit('hero ' + arg, [
    ['heroId', r.heroId], ['faction', r.faction], ['clan_ref', r.clan],
    ['spouse', r.spouse ? `${r.spouse} (${heroNameOf(r.spouse)})` : null],
    ['father', r.father ? `${r.father} (${heroNameOf(r.father)})` : null],
    ['mother', r.mother ? `${r.mother} (${heroNameOf(r.mother)})` : null],
    ['alive', r.alive == null ? null : r.alive.toString()], ['isTemplate', r.isTemplate],
  ])
  console.log(`  --- 生平/身份(官方中文，key ${(/\{=([A-Za-z0-9_.\-]+)\}/.exec(r.text || '') || [])[1] || '?'}) ---`)
  console.log('  ' + (humanize(r.text) ?? ''))
}

if (kind === 'clan') {
  const r = db.query('SELECT * FROM bannerlord_clans WHERE clanId = $id').get({ $id: arg }) as any
  if (!r) { console.log('clan not found:', arg); process.exit(0) }
  const ownerName = r.owner ? heroNameOf(r.owner) : null
  emit('clan ' + arg, [
    ['name_en', r.name], ['name_zh', humanize(r.name)],
    ['owner', r.owner + (ownerName ? ' (' + ownerName + ')' : '')],
    ['culture', r.culture], ['superFaction', r.superFaction], ['tier', r.tier],
    ['isNoble', r.isNoble], ['isMinorFaction', r.isMinorFaction], ['isMercenary', r.isMercenary],
    ['initialHomeSettlement', r.initialHomeSettlement ? resolveName(r.initialHomeSettlement) : null],
    ['description_zh', humanize(r.descriptionText)],
  ])
}

if (kind === 'kingdom') {
  const r = db.query('SELECT * FROM bannerlord_kingdoms WHERE kingdomId = $id').get({ $id: arg }) as any
  if (!r) { console.log('kingdom not found:', arg); process.exit(0) }
  const ownerName = r.owner ? heroNameOf(r.owner) : null
  emit('kingdom ' + arg, [
    ['name_en', r.name], ['name_zh', humanize(r.name)], ['title_zh', humanize(r.title)],
    ['rulerTitle_zh', humanize(r.rulerTitle)], ['culture', r.culture],
    ['owner', r.owner + (ownerName ? ' (' + ownerName + ')' : '')],
    ['description_zh', humanize(r.descriptionText)],
  ])
}

if (kind === 'culture') {
  const r = db.query('SELECT * FROM bannerlord_cultures WHERE cultureId = $id').get({ $id: arg }) as any
  if (!r) { console.log('culture not found:', arg); process.exit(0) }
  emit('culture ' + arg, [
    ['name_en', r.name], ['name_zh', humanize(r.name)], ['isMainCulture', r.isMainCulture],
    ['maleNameCount', r.maleNameCount], ['femaleNameCount', r.femaleNameCount],
    ['description_zh', humanize(r.descriptionText)],
  ])
}

if (kind === 'settlement') {
  const r = db.query('SELECT * FROM bannerlord_settlements WHERE settlementId = $id').get({ $id: arg }) as any
  if (!r) { console.log('settlement not found:', arg); process.exit(0) }
  emit('settlement ' + arg, [
    ['name_en', r.name], ['name_zh', humanize(r.name)], ['settlementType', r.settlementType],
    ['owner', (r.owner ? resolveName(r.owner) + ' (' + r.owner + ')' : '')],
    ['culture', r.culture], ['description_zh', humanize(r.descriptionText)],
  ])
}

if (kind === 'localize') {
  const s = db.query(`SELECT language, text FROM localization_entries WHERE stringId = $id AND language IN ('CNs','CNt')`).all({ $id: arg }) as any[]
  if (!s.length) { console.log('stringId not found in CNs/CNt:', arg); process.exit(0) }
  for (const r of s) console.log(`[${r.language}] ${r.text}`)
}

if (kind === 'search') {
  const like = '%' + arg + '%'
  const out: string[] = []
  for (const r of db.query(`SELECT heroId, text FROM bannerlord_heroes WHERE isTemplate = 0 AND text LIKE $q`).all({ $q: like }) as any[])
    out.push(`hero ${r.heroId}: ${(humanize(r.text) || '').slice(0, 80)}`)
  const ENT = ['Hero','Kingdom','Faction','Settlement','Culture','Town','Village','Castle','Hideout','NPCCharacter','Skill','Concept','Policy','Trait','Project','Building']
  const params: any = { $q: like }
  const inList = ENT.map((v, i) => { params['$e' + i] = v; return '$e' + i }).join(',')
  for (const r of db.query(`SELECT entityType, entityId, name FROM xml_entities WHERE (name LIKE $q OR entityId LIKE $q) AND entityType IN (${inList})`).all(params) as any[])
    out.push(`xml ${r.entityType} ${r.entityId}: ${humanize(r.name)}`)
  for (const r of db.query(`SELECT clanId, name FROM bannerlord_clans WHERE name LIKE $q OR clanId LIKE $q`).all({ $q: like }) as any[])
    out.push(`clan ${r.clanId}: ${humanize(r.name)}`)
  for (const r of db.query(`SELECT kingdomId, name FROM bannerlord_kingdoms WHERE name LIKE $q OR kingdomId LIKE $q`).all({ $q: like }) as any[])
    out.push(`kingdom ${r.kingdomId}: ${humanize(r.name)}`)
  for (const r of db.query(`SELECT distinct settlementId, name FROM bannerlord_settlements WHERE name LIKE $q OR settlementId LIKE $q`).all({ $q: like }) as any[])
    out.push(`settlement ${r.settlementId}: ${humanize(r.name)}`)

  console.log(`== search "${arg}" (${out.length}) ==`)
  for (const o of out.slice(0, 40)) console.log('  ' + o)

  if (cjkRe.test(arg)) {
    console.log(`== 中文反查 ${arg}（CNs/CNt 文本 → stringId → 实体）==`)
    const zhRows = db.query(`SELECT DISTINCT stringId, language, text FROM localization_entries WHERE language IN ('CNs','CNt') AND text LIKE $q ORDER BY language LIMIT 30`).all({ $q: like }) as any[]
    if (!zhRows.length) { console.log('  (中文未命中任何本地化条目)'); }
    const seen = new Set<string>()
    for (const z of zhRows) {
      const key = z.stringId + '@' + z.language
      if (seen.has(key)) continue
      seen.add(key)
      console.log(`  [${z.language}] {=${z.stringId}} ${z.text.slice(0, 60)}`)
      for (const ref of resolveRefs(z.stringId)) console.log(`      ${ref}`)
    }
  }
}

db.close()