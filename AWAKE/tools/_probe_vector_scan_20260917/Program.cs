using System.Diagnostics;
using System.Text;

// 全量点积扫描探针：N 条向量 × D 维，一次 query 全量算分并取 top-K。
// 目的：回答「语料涨到千级/万级，还需要重排器（粗筛+精排）吗」——
// 量的是「全量精确排名」本身的代价，和语料条数的关系。
//
// 用法： probe_scan <N> [D] [K]
//   N = 语料条数（默认 1000）
//   D = 向量维度（默认 768，bge-base-zh-v1.5 是 768）
//   K = 取前几名（默认 5）
Console.OutputEncoding = Encoding.UTF8;

int n = args.Length > 0 ? int.Parse(args[0]) : 1000;
int d = args.Length > 1 ? int.Parse(args[1]) : 768;
int k = args.Length > 2 ? int.Parse(args[2]) : 5;

long Mem()
{
    using var p = Process.GetCurrentProcess();
    p.Refresh();
    return p.PrivateMemorySize64;
}
string MB(long b) => (b / 1024.0 / 1024.0).ToString("F1");

GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
long beforeAlloc = Mem();

// 1) 造语料：N × D 扁平数组（与真实实现同形：连续内存、按行索引）
var corpus = new float[(long)n * d];
var seed = new Random(20260917);
for (long i = 0; i < corpus.LongLength; i++) corpus[i] = (float)(seed.NextDouble() * 2 - 1);
// 逐条归一化（真实向量是归一化过的，余弦＝点积）
for (int i = 0; i < n; i++)
{
    double sum = 0;
    long baseIdx = (long)i * d;
    for (int j = 0; j < d; j++) sum += corpus[baseIdx + j] * corpus[baseIdx + j];
    float inv = (float)(1.0 / Math.Sqrt(sum));
    for (int j = 0; j < d; j++) corpus[baseIdx + j] *= inv;
}

// 2) 造 query = 第 target 条 ＋ 一点噪声（模拟"问的就是它，但换了说法"）
int target = n / 3;
var query = new float[d];
long tBase = (long)target * d;
for (int j = 0; j < d; j++) query[j] = corpus[tBase + j] + (float)(seed.NextDouble() * 2 - 1) * 0.15f;
{
    double sum = 0;
    for (int j = 0; j < d; j++) sum += query[j] * query[j];
    float inv = (float)(1.0 / Math.Sqrt(sum));
    for (int j = 0; j < d; j++) query[j] *= inv;
}

GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
long afterAlloc = Mem();

// 3) 全量扫描：点积 ＋ 维持 top-K（真实实现就是这一步）
var best = new (float Score, int Index)[k];
var sw = Stopwatch.StartNew();
for (int i = 0; i < n; i++)
{
    long b = (long)i * d;
    float s = 0;
    for (int j = 0; j < d; j++) s += corpus[b + j] * query[j];
    if (s <= best[k - 1].Score && best[k - 1].Index >= 0) continue;
    int pos = k - 1;
    while (pos > 0 && best[pos - 1].Score < s) { best[pos] = best[pos - 1]; pos--; }
    best[pos] = (s, i);
}
sw.Stop();

Console.WriteLine("N=" + n + "\tD=" + d + "\tK=" + k
    + "\t向量内存MB=" + MB(corpus.LongLength * 4)
    + "\t分配增量MB=" + MB(afterAlloc - beforeAlloc)
    + "\t全量扫描ms=" + sw.Elapsed.TotalMilliseconds.ToString("F3"));

var sb = new StringBuilder("top" + k + "=");
for (int i = 0; i < k; i++) sb.Append(" #").Append(best[i].Index).Append("(").Append(best[i].Score.ToString("F4")).Append(")");
sb.Append("\t含target(").Append(target).Append(")?=").Append(Array.Exists(best, x => x.Index == target));
Console.WriteLine(sb.ToString());

// 变异检验：把 target 的向量清零，第一名就不该再是它 —— 否则说明排序根本没在排序
for (int j = 0; j < d; j++) corpus[tBase + j] = 0f;
var best2 = new (float Score, int Index)[1];
best2[0] = (-2f, -1);
for (int i = 0; i < n; i++)
{
    long b = (long)i * d;
    float s = 0;
    for (int j = 0; j < d; j++) s += corpus[b + j] * query[j];
    if (s > best2[0].Score) best2[0] = (s, i);
}
Console.WriteLine("变异检验（target 清零后第一名）=" + best2[0].Index
    + "\t仍然是 target 吗=" + (best2[0].Index == target)
    + "\t← 必须是 False，否则排序是假的");
