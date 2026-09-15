import sqlite3, sys

con = sqlite3.connect(":memory:")
try:
    con.execute("CREATE VIRTUAL TABLE t USING fts5(content, tokenize='unicode61')")
except Exception as e:
    print("FTS5 不可用:", e); sys.exit(0)

docs = [
    "卡拉迪亚是一块什么样的大陆，骑马与砍杀的世界线讲了怎样的故事",
    "涅雷采斯帝国在阿雷尼科斯死后分裂为三个继承国",
    "德洛修斯是瓦兰迪亚的国王",
]
for d in docs:
    con.execute("INSERT INTO t(content) VALUES (?)", (d,))

# 看一下 unicode61 到底把中文切成了什么
print("=== unicode61 分词结果 ===")
try:
    rows = con.execute("SELECT * FROM fts5vocab(t,'row') LIMIT 40").fetchall()
    print("term 数:", len(rows))
    for r in rows[:25]:
        print("  ", r)
except Exception as e:
    print("  fts5vocab 不可用:", e)

print()
print("=== 检索测试（中文字串）===")
for q in ["卡拉迪亚", "德洛修斯", "阿雷尼科斯", "卡拉迪亚是一块"]:
    try:
        n = con.execute("SELECT count(*) FROM t WHERE t MATCH ?", (q,)).fetchone()[0]
        print(f"  MATCH {q!r} -> {n} 命中")
    except Exception as e:
        print(f"  MATCH {q!r} -> 报错: {e}")

print()
print("=== 前缀检索 ===")
for q in ["卡拉*", "德洛*"]:
    try:
        n = con.execute("SELECT count(*) FROM t WHERE t MATCH ?", (q,)).fetchone()[0]
        print(f"  MATCH {q!r} -> {n} 命中")
    except Exception as e:
        print(f"  MATCH {q!r} -> 报错: {e}")

print()
print("=== 对照：trigram 分词器（SQLite 3.34+）===")
print("sqlite 版本:", sqlite3.sqlite_version)
try:
    con.execute("CREATE VIRTUAL TABLE t2 USING fts5(content, tokenize='trigram')")
    for d in docs:
        con.execute("INSERT INTO t2(content) VALUES (?)", (d,))
    for q in ["卡拉迪亚", "德洛修斯"]:
        n = con.execute("SELECT count(*) FROM t2 WHERE t2 MATCH ?", (q,)).fetchone()[0]
        print(f"  trigram MATCH {q!r} -> {n} 命中")
except Exception as e:
    print("  trigram 不可用:", e)
