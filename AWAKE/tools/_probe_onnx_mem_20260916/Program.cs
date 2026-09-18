using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

// 内存扫描探针（通用）。一次只跑一个 (模型, batch, length) 组合，然后退出。
// 由外层脚本逐组合起新进程 —— 因为 ORT 的 arena 不保证把内存还给系统，
// 同一进程连着跑多个组合，后面的数会被前面的脏内存污染。
//
// 用法： probe_onnx_mem <small|reranker> <batch> <length>
Console.OutputEncoding = System.Text.Encoding.UTF8;

string onnxRoot = @"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AnimusForge\ONNX";

string which = args.Length > 0 ? args[0] : "small";
int batch = args.Length > 1 ? int.Parse(args[1]) : 20;
int len = args.Length > 2 ? int.Parse(args[2]) : 512;

// 大词表模型要一个落在范围内的假 token id；这里只用内存，不关心语义对不对。
string model, label;
int filler;
if (which == "reranker")
{
    model = Path.Combine(onnxRoot, "reranker", "model.onnx");
    label = "重排器 XLM-R base";
    filler = 1000;          // 词表 250,002，落在范围内
}
else if (which.StartsWith("path:"))
{
    // 任意模型文件：probe_mem path:<绝对路径> <batch> <len> [repeat] [filler]
    model = which.Substring(5);
    label = Path.GetFileName(Path.GetDirectoryName(model)) + "/" + Path.GetFileName(model);
    filler = args.Length > 4 ? int.Parse(args[4]) : 7222;
}
else
{
    model = Path.Combine(onnxRoot, "model.onnx");
    label = "向量模型 bge-small-zh";
    filler = 7222;          // 「锅」
}

long Mem()
{
    using var p = Process.GetCurrentProcess();
    p.Refresh();
    return p.PrivateMemorySize64;
}
string MB(long b) => (b / 1024.0 / 1024).ToString("F0");
string GB(long b) => (b / 1024.0 / 1024 / 1024).ToString("F2");

GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
long beforeLoad = Mem();

var swLoad = Stopwatch.StartNew();
var session = new InferenceSession(model);
swLoad.Stop();

GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
long afterLoad = Mem();

long weightBytes = new FileInfo(model).Length;
if (File.Exists(model + "_data")) weightBytes += new FileInfo(model + "_data").Length;

// 造 batch × len 的输入；对每个声明的输入都按形状填
var inputs = new List<NamedOnnxValue>();
foreach (var kv in session.InputMetadata)
{
    string name = kv.Key;
    var dims = kv.Value.Dimensions;
    bool isMask = name.Contains("mask", StringComparison.OrdinalIgnoreCase);
    bool isTypes = name.Contains("type", StringComparison.OrdinalIgnoreCase);
    var flat = new long[batch * len];
    for (int i = 0; i < flat.Length; i++)
        flat[i] = isTypes ? 0 : (isMask ? 1 : filler);
    for (int b = 0; b < batch; b++)
    {
        if (isMask) { flat[b * len + len - 1] = 1; }
    }
    inputs.Add(NamedOnnxValue.CreateFromTensor(name, new DenseTensor<long>(flat, new[] { batch, len })));
}

long beforeRun = Mem();
var sw = Stopwatch.StartNew();
int repeat = args.Length > 3 ? int.Parse(args[3]) : 1;
long prev = beforeRun;
for (int r = 1; r <= repeat; r++)
{
    sw.Restart();
    using (var outv = session.Run(inputs))
    {
        sw.Stop();
        long peak = Mem();
        Console.WriteLine(string.Join("\t", new[]
        {
            "RUN" + r,
            label,
            "batch=" + batch,
            "len=" + len,
            "权重MB=" + MB(weightBytes),
            "加载增量MB=" + MB(afterLoad - beforeLoad),
            "加载秒=" + (swLoad.ElapsedMilliseconds / 1000),
            "算前MB=" + MB(prev),
            "算后MB=" + MB(peak),
            "算增量MB=" + MB(peak - prev),
            "算增量GB=" + GB(peak - prev),
            "耗时ms=" + sw.ElapsedMilliseconds,
        }));
        prev = peak;
    }
}

session.Dispose();
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
Console.WriteLine("AFTER_DISPOSE\t" + label + "\tbatch=" + batch + "\tlen=" + len
                  + "\t进程MB=" + MB(Mem()));
