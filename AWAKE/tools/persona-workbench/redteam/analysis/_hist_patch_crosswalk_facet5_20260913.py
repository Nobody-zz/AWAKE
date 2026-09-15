# -*- coding: utf-8 -*-
"""给 crosswalk 的 facet_mapped 编码补上 "5" 动作（对齐其兄弟编码 facet_preserve_only）。
字节级外科编辑：只插入一处，不重排、不改变行尾（保持 CRLF）。"""
import json, hashlib, os, sys

P = r'D:\AWAKE-Dev\AWAKE\docs\persona-contract\persona-workbench-to-awake.crosswalk.v1.json'
raw = open(P, 'rb').read()
s = raw.decode('utf-8')

assert s.count('\r\n') > 0 and s.count('\n') == s.count('\r\n'), '行尾不是纯 CRLF，中止'

start = s.index('"id":  "facet_mapped"')
end = s.index('"id":  "facet_preserve_only"', start)
region = s[start:end]

old = ('                                               "4":  {\r\n'
       '                                                         "action":  "preserve_only"\r\n'
       '                                                     }\r\n'
       '                                           }')
new = ('                                               "4":  {\r\n'
       '                                                         "action":  "preserve_only"\r\n'
       '                                                     },\r\n'
       '                                               "5":  {\r\n'
       '                                                         "action":  "preserve_only"\r\n'
       '                                                     }\r\n'
       '                                           }')

n = region.count(old)
print('region 内命中 old 次数:', n)
if n != 1:
    print('!! 期望恰好 1 次，中止'); sys.exit(1)

region2 = region.replace(old, new)
s2 = s[:start] + region2 + s[end:]

# 校验 JSON 合法 + 目标已生效
doc = json.loads(s2)
fm = [e for e in doc['valueEncodings'] if e['id'] == 'facet_mapped'][0]
print('facet_mapped actions 键:', sorted(fm['actions'].keys()))
assert '5' in fm['actions'], '插入未生效'

open(P, 'wb').write(s2.encode('utf-8'))
print('已写入。新字节数:', len(s2.encode('utf-8')), '（原', len(raw), '）')
print('新 sha256:', hashlib.sha256(s2.encode('utf-8')).hexdigest().upper())
