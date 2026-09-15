"""AWAKE src audit - exact counts (read-only, ASCII only)."""
import os
import re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "src"))
paths = set()
paths.add(ROOT)
for base, _d, names in os.walk(ROOT):
    for name in names:
        if name.endswith(".cs"):
            paths.add(os.path.join(base, name))
files = sorted(p for p in paths if p.endswith(".cs"))

ASYNC_VOID = re.compile(r"\basync\s+void\b")
ASYNC_METHOD = re.compile(r"\basync\s+(?:Task|ValueTask|IAsyncEnumerable)")
EMPTY_CATCH = re.compile(r"catch\s*(?:\([^)]*\))?\s*\{\s*\}", re.S)
CATCH_TOTAL = re.compile(r"\bcatch\b")

async_void = 0
async_methods = 0
empty_catches = 0
total_catches = 0

for path in files:
    with open(path, "r", encoding="utf-8", errors="replace") as handle:
        text = handle.read()
    async_void += len(ASYNC_VOID.findall(text))
    async_methods += len(ASYNC_METHOD.findall(text))
    empty_catches += len(EMPTY_CATCH.findall(text))
    total_catches += len(CATCH_TOTAL.findall(text))

print("files                : %d" % len(files))
print("async void           : %d" % async_void)
print("async Task/ValueTask : %d" % async_methods)
print("catch total          : %d" % total_catches)
print("catch with empty body: %d" % empty_catches)
