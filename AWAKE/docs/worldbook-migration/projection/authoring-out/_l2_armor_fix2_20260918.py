# -*- coding: utf-8 -*-
"""护甲批第二处定点修复（2026-09-18）：`shield-round` 正文混入编纂口吻。

谁抓到的：`awake-prose-qc`（世界书侧硬项，一票否决）。全库 482 档只此一处：
    weapons-armor-shield-round.yaml  ⛔「数据」x1 ＋ ⛔ 作者旁白「游戏数据」x1

原句（L2 assert1）：
    …直径多在六十到一百三十之间（一条写着四百三十五的，是游戏数据里的孤值）。这一路二十八件…
                                      ^^^^^^^^^^^^^^^^^^^^^^^^^^^^ 正文里不该有编纂口吻

那个 435 是什么（查过，不是笔误）：快照
    `armor.southern_oval_shield => 强化椭圆盾 | Shield | 盾类 大盾 | 护 1 | 长 435 | 耐久 360 | 属 aserai`
它是一件**椭圆盾**（归在"大盾"档），被本批的形制归并口径收进了 `shield-round` 这一路；
它的"长 435"与圆盾们的直径（六十到一百三十）不同量级，本就不该混在"圆盾直径"那句话里说。
⇒ 处置：**把数字从正文里拿掉**，只留"另有一路椭圆盾、归大盾档"这个形状事实。
   435 这个值本身是游戏真值、不丢——它留在快照与这里，供日后核。
   （若哪天真要写进正文，得先弄清 `长` 在椭圆盾上量的是什么，按现在的数字写会读成 4.35 米。）

用法: python _l2_armor_fix2_20260918.py [--apply]
"""
import io, json, os, sys, hashlib

HERE = os.path.dirname(os.path.abspath(__file__))
P = os.path.join(HERE, '_l2_armor_20260918.json')
APPLY = '--apply' in sys.argv

OLD = "（一条写着四百三十五的，是游戏数据里的孤值）"
NEW = "；另有一路椭圆盾，比圆盾抻长得多，归在大盾那一档。"
# 改前那句以"之间（…）。这一路"相连，改后要去掉多余的句号 → 连成整句
OLD_FULL = "之间（一条写着四百三十五的，是游戏数据里的孤值）。这一路"
NEW_FULL = "之间；另有一路椭圆盾，比圆盾抻长得多，归在大盾那一档。这一路"

# 编纂口吻词（本项目的硬项词表里与"元话语"相关的那几个）
META = ['数据', '快照', '字段', '节点', '游戏', '孤值', '索引', '条目', '样本', '统计']


def main():
    raw = io.open(P, encoding='utf-8').read()
    print('修复前 md5:', hashlib.md5(raw.encode('utf-8')).hexdigest(), len(raw.encode('utf-8')), 'B')
    d = json.loads(raw)
    card = [c for c in d['cards'] if c['slug'] == 'shield-round']
    assert len(card) == 1
    a1 = card[0]['asserts'][0]
    assert OLD_FULL in a1['text'], '原句没找到，先核对文本'
    a1['text'] = a1['text'].replace(OLD_FULL, NEW_FULL)
    print('\n改后 assert1：')
    print('  ' + a1['text'])

    # 自检：本卡全部正文不得再出现编纂口吻词
    bad = []
    for a in card[0]['asserts']:
        for w in META:
            if w in a['text']:
                bad.append(('assert', w))
        for e in a['exprs']:
            for w in META:
                if w in e['text']:
                    bad.append((e['tag'], w))
    print('\n自检（本卡正文编纂口吻词）:', 'PASS' if not bad else 'FAIL %s' % bad)
    if bad:
        print('有 FAIL，不落盘'); return 1

    # 顺带全批复查：21 张卡任何正文都不得有这些词
    allbad = []
    for c in d['cards']:
        for a in c['asserts']:
            if any(w in a['text'] for w in META):
                allbad.append(c['slug'])
        for e in a['exprs']:
            if any(w in e['text'] for w in META):
                allbad.append(c['slug'])
    print('全批 21 卡复查:', 'PASS' if not allbad else 'FAIL %s' % sorted(set(allbad)))
    if allbad:
        return 1

    if not APPLY:
        print('\n（演练，未落盘。加 --apply 才写。）'); return 0
    io.open(P, 'w', encoding='utf-8', newline='\n').write(json.dumps(d, ensure_ascii=False, indent=1))
    raw2 = io.open(P, encoding='utf-8').read()
    print('\n修复后 md5:', hashlib.md5(raw2.encode('utf-8')).hexdigest(), len(raw2.encode('utf-8')), 'B')
    print('已落盘:', P)
    return 0


if __name__ == '__main__':
    sys.exit(main())
