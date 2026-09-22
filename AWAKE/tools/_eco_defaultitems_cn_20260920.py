# -*- coding: utf-8 -*-
"""取 DefaultItems.cs 那 16 条的官方中文名。"""
import sqlite3, re

DB = r'C:\Users\26811\Downloads\20260612093225539\BannerlordSage-main\dist\games\bannerlord\bannerlord.db'
con = sqlite3.connect('file:%s?mode=ro' % DB.replace('\\', '/'), uri=True)
con.row_factory = sqlite3.Row
cur = con.cursor()

def cn(key):
    c2 = con.cursor()
    c2.execute("SELECT text FROM localization_entries WHERE language='CNs' AND stringId=? LIMIT 1", (key,))
    r = c2.fetchone()
    if not r: return "!! 无 CNs"
    t = re.sub(r'\{@Plural\}.*?\{\\@\}', '', r[0], flags=re.S)
    t = re.sub(r'\{[^}]*\}', '', t)
    return t.strip()

ROWS = [
 ("grain","Itv3fgJm","Grain",10,10), ("meat","LmwhFv5p","Meat",30,10),
 ("planks","5ac8Boz1","Planks",180,10), ("felt","hNwjpCVP","Felt",230,10),
 ("hides","4kvKQuXM","Hides",50,10), ("tools","n3cjEB0X","Tools",250,10),
 ("iron","Kw6BkhIf","Iron Ore",50,10), ("hardwood","ExjMoUiT","Hardwood",25,10),
 ("charcoal","iQadPYNe","Charcoal",50,5),
 ("ironIngot1","gOpodlt1","Crude Iron",20,0.5), ("ironIngot2","7HvtT8bm","Wrought Iron",30,0.5),
 ("ironIngot3","XHmmbnbB","Iron",60,0.5), ("ironIngot4","UfuLKuaI","Steel",100,0.5),
 ("ironIngot5","azjMBa86","Fine Steel",160,0.5), ("ironIngot6","vLVAfcta","Thamaskene Steel",260,0.5),
 ("_itemTrash","ZvZN6UkU","Trash Item",1,1),
]
print("%-12s %-18s %-8s %-6s %s" % ("id", "英文", "标价", "重量", "官方中文名"))
for i, k, en, v, w in ROWS:
    print("%-12s %-18s %-8s %-6s %s" % (i, en, v, w, cn(k)))
con.close()
