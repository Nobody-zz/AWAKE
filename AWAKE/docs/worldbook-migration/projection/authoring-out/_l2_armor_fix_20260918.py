# -*- coding: utf-8 -*-
"""护甲批 L2 两处缺陷的定点修复（2026-09-18，验收矩阵探针时发现）。

缺陷 1（缺层）：`torso-ring` 与 `shield-wicker` 没有**通用 detail 表达** ——
  19/21 张卡是「a1 = rumor + 通用 detail、a2 = 文化 detail」的写法，这两张例外：
  · torso-ring 只有 1 个断言，a1 = rumor + **aserai**-detail（文化句占了通用位）
  · shield-wicker 两个断言的 detail **都是**文化限定（khuzait / aserai）
  ⇒ 一个不带文化的商人/头人/士兵/贵族问「环甲」「柳条盾」时，只拿得到 rumor 层，
    探针读数 = partial，且因为 partial 不渲染标题，正文变成一段没有出处的口语 + 别的条目。
    这正是"答非所问"的形态。（矩阵 IWA6 / IWA19 两条红的根因。）

缺陷 2（重名）：5 张卡两个断言各生成一条 `<slug>-detail`，**表达 id 因此重复**
  （torso-civil / torso-gambeson / torso-mail / cape-mantle / hands）。
  表达 id 取自 L2 的 `tag`（生成器 `_rollout_armor_gen_20260918.py:142`），
  同名 ⇒ 上线件同一张卡里躺着两条同 id 表达 —— 谁按 id 去取都只会拿到第一条，
  是"静默失效"那一类。⇒ 把第二次出现的 tag 改成各自专名。

两处都是在**不改任何数字**的前提下改的：只增一句通用 detail、只改 tag 名。
用法: python _l2_armor_fix_20260918.py          （只打印将要做的改动）
      python _l2_armor_fix_20260918.py --apply  （落盘）
"""
import io, json, os, sys, collections, hashlib

HERE = os.path.dirname(os.path.abspath(__file__))
P = os.path.join(HERE, '_l2_armor_20260918.json')
APPLY = '--apply' in sys.argv

# ---- 缺陷 2：重名 tag → 专名（新名描述该断言的另一路内容，与 a2 正文对应）----
RENAME = {
    'torso-civil': ('studded-detail',),        # a2 = 镶钉皮马甲 / 林地服
    'torso-gambeson': ('leatherstrip-detail',),  # a2 = 皮革条甲衬填充袍
    'torso-mail': ('surcoat-detail',),         # a2 = 罩袍衬链甲 / 缝进皮里
    'cape-mantle': ('pad-detail',),            # a2 = 皮护肩与编织垫肩
    'hands': ('padded-detail',),               # a2 = 填充护手
}

# ---- 缺陷 1：补通用 detail（数字全部取本卡 assert 原文，未新增数字）----
RING_DETAIL = (
    "环甲按底衬与铁环的密度分档：布底轻，皮底耐，环缝得越密越顶事。"
    "护值落在十三到二十——同重之下比锁甲低一档，赢在做起来快、破一个补一个，"
    "不必像链甲那样一断就整片拆。养护上正好反过来：皮底怕晒，晒久了发硬要上油，"
    "比链甲费心；可修起来比链甲省事。"
)
WICKER_DETAIL = (
    "柳条盾按编层用料与蒙皮分档：藤条轻、木条经打，外面蒙的那层皮是为防潮。"
    "长度一律在九十到九十五之间，护值一到五——它从头到尾不算护具，是给弓手挡箭用的。"
    "耐久差得最远，从一百六十到五百一十，三倍还多。"
)


def main():
    raw = io.open(P, encoding='utf-8').read()
    print('修复前 md5:', hashlib.md5(raw.encode('utf-8')).hexdigest(),
          len(raw.encode('utf-8')), 'B')
    d = json.loads(raw)
    cards = {c['slug']: c for c in d['cards']}
    changed = []

    # 缺陷 2
    for slug, (newtag,) in RENAME.items():
        c = cards[slug]
        hits = [e for a in c['asserts'] for e in a['exprs'] if e['tag'] == 'detail']
        assert len(hits) == 2, (slug, len(hits))
        # 改**第二个**断言里的那条（a1 的 detail 是主档，保留原名，与其余 14 张卡一致）
        a2 = c['asserts'][1]
        tgt = [e for e in a2['exprs'] if e['tag'] == 'detail']
        assert len(tgt) == 1, (slug, len(tgt))
        tgt[0]['tag'] = newtag
        changed.append('%s: a2 tag detail -> %s' % (slug, newtag))

    # 缺陷 1a：torso-ring —— 在 a1 的 rumor 之后插一条通用 detail（留在同一个断言里，
    # 与 cape-fur / cape-shoulders / shield-heater 的「a1 里三种层并存」写法一致）
    ring = cards['torso-ring']
    assert len(ring['asserts']) == 1
    tags = [e['tag'] for e in ring['asserts'][0]['exprs']]
    assert tags == ['rumor', 'aserai-detail'], tags
    ring['asserts'][0]['exprs'].insert(1, {
        'tag': 'detail', 'layer': 'detail', 'grants': 'detail', 'text': RING_DETAIL})
    changed.append('torso-ring: a1 插入通用 detail 表达（原：rumor + aserai-detail）')

    # 缺陷 1b：shield-wicker —— a2（杏形盾）补一条通用 detail，与 aserai-detail 并存
    wick = cards['shield-wicker']
    a2 = wick['asserts'][1]
    assert [e['tag'] for e in a2['exprs']] == ['aserai-detail'], [e['tag'] for e in a2['exprs']]
    a2['exprs'].append({
        'tag': 'detail', 'layer': 'detail', 'grants': 'detail', 'text': WICKER_DETAIL})
    changed.append('shield-wicker: a2 补通用 detail 表达（原：仅 aserai-detail）')

    # ---- 改后自检：每卡 tag 唯一 + 至少一条不带 culture 的 detail 层表达 ----
    print()
    bad = []
    for c in d['cards']:
        tg = [e['tag'] for a in c['asserts'] for e in a['exprs']]
        dup = {k: v for k, v in collections.Counter(tg).items() if v > 1}
        generic_detail = [e for a in c['asserts'] for e in a['exprs']
                          if e['layer'] == 'detail' and not e.get('culture')
                          and e['grants'] == 'detail']
        flag = ''
        if dup:
            bad.append('%s tag 重复 %s' % (c['slug'], dup)); flag += ' ★tag重复'
        if not generic_detail:
            bad.append('%s 缺通用 detail 层' % c['slug']); flag += ' ★缺通用detail'
        print('  %-16s exprs=%d tag唯一=%s 通用detail=%d%s'
              % (c['slug'], len(tg), '是' if not dup else '否', len(generic_detail), flag))

    print()
    for m in changed:
        print('  改动:', m)
    print('  自检:', 'PASS' if not bad else ('FAIL ' + '; '.join(bad)))
    if bad:
        print('有 FAIL，不落盘'); return 1

    if not APPLY:
        print('\n（演练，未落盘。加 --apply 才写。）'); return 0

    out = json.dumps(d, ensure_ascii=False, indent=1)
    io.open(P, 'w', encoding='utf-8', newline='\n').write(out)
    raw2 = io.open(P, encoding='utf-8').read()
    print('\n修复后 md5:', hashlib.md5(raw2.encode('utf-8')).hexdigest(),
          len(raw2.encode('utf-8')), 'B')
    print('已落盘:', P)
    return 0


if __name__ == '__main__':
    sys.exit(main())
