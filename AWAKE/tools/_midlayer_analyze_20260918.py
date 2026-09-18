import json, io, sys, collections

P = r"D:\AWAKE-Dev\AWAKE\tools\_midlayer_probe_result_20260918.json"
OUT = r"D:\AWAKE-Dev\AWAKE\tools\_midlayer_analyze_20260918.txt"

d = json.load(open(P, encoding="utf-8"))
stats = d["stats"]

lines = []
def w(s=""):
    lines.append(s)

w("packageEntries=%s  sameAsProductAtK3=%s  controlOk=%s  denom=%s"
  % (d["packageEntries"], d["sameAsProductAtK3"], d["controlOk"], d["denominator"]))
w()

# per-K cumulative hits
w("== cumulative hit@N per K (denominator %d) ==" % d["denominator"])
w("%-6s %-8s %-8s %-8s %-8s %-8s %-10s %-12s" % ("K", "hit@1", "hit@2", "hit@3", "hit@4", "hit@5", "miss", "lenAvg/max"))
ranks = {}
for s in stats:
    k = s["k"]
    cs = [c for c in s["cases"] if c["countInGate"]]
    den = len(cs)
    def hit(n):
        return sum(1 for c in cs if 1 <= c["rank"] <= n)
    miss = sum(1 for c in cs if c["rank"] == 0)
    w("%-6d %-8s %-8s %-8s %-8s %-8s %-10d %-12s"
      % (k, "%d/%d" % (hit(1), den), "%d/%d" % (hit(2), den), "%d/%d" % (hit(3), den),
         "%d/%d" % (hit(4), den), "%d/%d" % (hit(5), den), miss,
         "%d/%d" % (s["candidateAvg"], s["candidateMax"])))
    ranks[k] = {(c["query"], c["target"]): c["rank"] for c in cs}
w()

base = ranks[3]
ks = sorted(ranks.keys())
w("== rank change vs K=3 (only cases that move) ==")
moved = 0
for key in base:
    seq = [ranks[k].get(key, 0) for k in ks]
    if any(r != seq[0] for r in seq):
        moved += 1
        w("  " + " | ".join("K=%d:%s" % (k, ("MISS" if r == 0 else r)) for k, r in zip(ks, seq)))
        w("     group=%s  query=%s" % (key[0] if False else "", key[0]))
        w("     target=%s" % key[1])
w("  moved cases: %d / %d" % (moved, len(base)))
w()

w("== cases still MISS at every K ==")
missing = [k for k in base if all(ranks[kk].get(k, -1) == 0 for kk in ks)]
for q, t in missing:
    w("  " + q + "  ->  " + t)
w()

w("== rank at K=10 .. K=100, cases whose rank <= 6 at any K ==")
for key in base:
    best = min([r for r in (ranks[k].get(key, 99) for k in ks) if r > 0] or [99])
    if best <= 6:
        w("  best=%d  %s" % (best, " | ".join("K=%d:%s" % (k, ("MISS" if ranks[k].get(key, 0) == 0 else ranks[k].get(key))) for k in ks)))
        w("     %s -> %s" % (key[0], key[1]))
w()

io.open(OUT, "w", encoding="utf-8", newline="\n").write("\n".join(lines))
print("written", OUT, len(lines), "lines")
