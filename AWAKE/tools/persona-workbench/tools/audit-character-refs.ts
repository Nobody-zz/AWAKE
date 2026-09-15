// 角色卡 vs BannerlordSage 库 —— 三方一致性核验（只读）
// 核对每张 characters/*.persona.json 的 origins:
//   heroId → 库中 hero 的归属 clan / 王国 superFaction / 角色名（本地化换算中文）
//   与卡内 kingdomId / clanId 是否一致。
// 退出码: 0 = 全部一致；1 = 存在缺失/不一致。
// DB 路径可用环境变量 BANNERSAGE_DB 覆盖，默认指向本机 BannerlordSage 库。
import { Database } from 'bun:sqlite'
import { existsSync, readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'

const DEFAULT_DB = 'C:/Users/26811/Downloads/20260612093225539/BannerlordSage-main/dist/games/bannerlord/bannerlord.db'
const dbPath = process.env.BANNERSAGE_DB || DEFAULT_DB
const charDir = join(import.meta.dir, '..', 'characters')
const ZH = 'CNs'

if (!existsSync(dbPath)) {
  console.error('BANNERSAGE_DB not found:', dbPath)
  console.error('Set BANNERSAGE_DB to your BannerlordSage dist/games/bannerlord/bannerlord.db')
  process.exit(2)
}
if (!existsSync(charDir)) {
  console.error('角色卡目录不存在:', charDir)
  process.exit(2)
}

const db = new Database(dbPath, { readonly: true })

function zh(id: string): string | null {
  const r = db.query('SELECT text FROM localization_entries WHERE stringId = $id AND language = $l LIMIT 1').get({ $id: id, $l: ZH }) as any
  return r?.text ?? null
}

const tokenRe = /\{=([A-Za-z0-9_.\-]+)\}/g
function humanize(s: string | null | undefined): string {
  if (!s) return ''
  return s.replace(tokenRe, (m, id: string) => zh(id)?.trim() || m)
}

function heroNameZh(heroId: string): string {
  const npc = db.query(`SELECT name FROM xml_entities WHERE entityId = $id AND entityType = 'NPCCharacter' LIMIT 1`).get({ $id: heroId }) as any
  const zhName = humanize(npc?.name ?? '')
  const run = /[\u3400-\u9fff][\u3400-\u9fff0-9，。；、：·()（）\s]*(?=[A-Za-z]|\s|$)/.exec(zhName)
  return run ? run[0].trim() : zhName.trim()
}

interface Origins { heroId?: string; kingdomId?: string; clanId?: string }

const files = readdirSync(charDir).filter(f => f.endsWith('.persona.json')).sort()
let issues = 0
const statuses = new Map<string, string>()

console.log('== 角色卡 × BannerlordSage 库 一致性核验 ==')
console.log('卡片'.padEnd(22) + 'heroId'.padEnd(11) + '库中文名'.padEnd(9) + '库归属clan'.padEnd(26) + '库王国'.padEnd(12) + '卡kingdomId'.padEnd(13) + '卡clanId    状态')
for (const f of files) {
  const card = f.replace('.persona.json', '')
  // heroId/kingdomId/clanId 以 .origins.json 侧车为准；卡内嵌 origins 保留作兜底
  const sidePath = join(charDir, card + '.origins.json')
  let o: Origins = {}
  if (existsSync(sidePath)) {
    const s = JSON.parse(readFileSync(sidePath, 'utf8')) as Origins
    if (s.heroId || s.kingdomId || s.clanId) o = s
  }
  if (!o.heroId) {
    const j = JSON.parse(readFileSync(join(charDir, f), 'utf8')) as any
    if (j?.origins && (j.origins.heroId || j.origins.kingdomId || j.origins.clanId)) o = j.origins as Origins
  }

  let dbClan = '', dbKingdom = '', dbName = '', status = 'OK'
  if (!o.heroId) { status = 'MISSING_HERO'; issues++ }
  else {
    const hero = db.query('SELECT faction FROM bannerlord_heroes WHERE heroId = $id LIMIT 1').get({ $id: o.heroId }) as any
    if (!hero?.faction) { status = 'HERO_UNKNOWN'; issues++ }
    else {
      dbClan = String(hero.faction).replace('Faction.', '')
      const clan = db.query('SELECT superFaction FROM bannerlord_clans WHERE clanId = $id LIMIT 1').get({ $id: dbClan }) as any
      dbKingdom = clan?.superFaction ? String(clan.superFaction).replace('Kingdom.', '') : ''
      dbName = heroNameZh(o.heroId)
      if (o.kingdomId && dbKingdom !== o.kingdomId) { status = 'KINGDOM_MISMATCH'; issues++ }
      else if (o.clanId && dbClan !== o.clanId) { status = 'CLAN_MISMATCH'; issues++ }
    }
  }
  statuses.set(card, status)
  console.log(
    card.padEnd(22)
    + (o.heroId || '').padEnd(11)
    + dbName.slice(0, 8).padEnd(9)
    + dbClan.padEnd(26)
    + dbKingdom.padEnd(12)
    + (o.kingdomId || '').padEnd(13)
    + (o.clanId || '').padEnd(14)
    + status
  )
}

console.log('\n结论: ' + (issues === 0
  ? '全部一致 ✓（' + files.length + ' 张角色卡）'
  : issues + ' 处不一致/缺失，请见上方置标行（退出码 1）'))
process.exit(issues === 0 ? 0 : 1)