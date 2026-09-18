using System.Diagnostics;
using System.Text;
using MarcusAwakeStorage;

// 验真实现：MarcusAwakeStorage 里那个 OnnxSentenceEmbedder，能不能在 C# 侧把 bge-small-zh 跑出
// 「相近句高、无关句低」的向量。
//
// 这不是"能跑就行"的探针 —— 本项目 08-15 记过一条坑：**分词出错 ⇒ 静默退化成常量向量、不抛错**。
// 所以判据必须是「句子之间真的分得开」，且要与 docs/DESIGN-20260916 §1.3 那份 Python/官方值对得上。
Console.OutputEncoding = Encoding.UTF8;

var modelDir = args.Length > 0
    ? args[0]
    : @"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AnimusForge\ONNX";
var vocabPath = args.Length > 1
    ? args[1]
    : @"D:\AWAKE-Dev\AWAKE\tools\_af_tokenizer_20260916\vocab.txt";

string[] probes =
{
    "圆顶锅盔是什么",
    "圆顶锅盔多少钱",
    "斯特吉亚的军队怎么打仗",
    "怎么做面包",
    "阿雷尼科斯是谁",
    "今天天气不错",
};

Console.WriteLine("模型目录 = " + modelDir);
Console.WriteLine("词表     = " + vocabPath);
Console.WriteLine();

var options = new OnnxEmbedderOptions
{
    ModelDirectory = modelDir,
    VocabularyPath = vocabPath,
    ModelId = "bge-small-zh-v1.5|d512|feed-D|v1",
};

using var embedder = new OnnxSentenceEmbedder(options);
Console.WriteLine("ModelId = " + embedder.ModelId);

var watch = Stopwatch.StartNew();
var vectors = embedder.Encode(probes, CancellationToken.None);
watch.Stop();
Console.WriteLine("维度 = " + embedder.Dimension + "，首跑（含建会话）" + watch.ElapsedMilliseconds + " ms");

watch.Restart();
embedder.Encode(probes, CancellationToken.None);
watch.Stop();
Console.WriteLine("第二次（会话已常驻）" + watch.ElapsedMilliseconds + " ms");

double Cos(float[] a, float[] b)
{
    double sum = 0;
    for (var index = 0; index < a.Length; index++) sum += (double)a[index] * b[index];
    return sum;
}

// 判据 A / B：不能退化成常量向量
var distinct = vectors.Select(v => string.Join(",", v.Select(x => x.ToString("F6")))).Distinct().Count();
var degenerate = vectors.Any(v => v.All(x => x == 0f) || v.Any(float.IsNaN));
Console.WriteLine();
Console.WriteLine("[A] 不同向量数 = " + distinct + "/" + probes.Length + "  => " + (distinct == probes.Length ? "PASS" : "FAIL"));
Console.WriteLine("[B] 无全零/NaN = " + (!degenerate ? "PASS" : "FAIL"));

// 判据 C：与 docs/DESIGN-20260916 §1.3 的官方值对齐（相近 0.856、无关 0.140）
var near = Cos(vectors[0], vectors[1]);
var far = Cos(vectors[0], vectors[5]);
Console.WriteLine("[C] 相近句 = " + near.ToString("F4") + "（官方 0.856），无关句 = " + far.ToString("F4") + "（官方 0.140）");
Console.WriteLine("    相近 > 无关  => " + (near > far ? "PASS" : "FAIL"));
Console.WriteLine("    与官方值同量级 => " + (Math.Abs(near - 0.856) < 0.02 && Math.Abs(far - 0.140) < 0.02 ? "PASS" : "FAIL"));

// 判据 D：归一化后自比必须 = 1（否则不是余弦空间）
var self = Cos(vectors[0], vectors[0]);
Console.WriteLine("[D] 自比 = " + self.ToString("F6") + "  => " + (Math.Abs(self - 1.0) < 1e-4 ? "PASS" : "FAIL"));

Console.WriteLine();
Console.WriteLine("两两余弦：");
Console.WriteLine("      " + string.Join(" ", probes.Select((_, i) => ("[" + i + "]").PadLeft(7))));
for (var i = 0; i < probes.Length; i++)
{
    var row = new List<string> { "[" + i + "] " + probes[i].PadRight(10) };
    for (var j = 0; j < probes.Length; j++) row.Add(Cos(vectors[i], vectors[j]).ToString("F3").PadLeft(7));
    Console.WriteLine(string.Join(" ", row));
}
