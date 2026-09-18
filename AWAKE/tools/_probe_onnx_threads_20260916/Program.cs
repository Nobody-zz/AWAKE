using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

// 量「推理会吃几个 CPU 核」，以及限线程后慢多少。
// 进程外跑只解决了内存，解决不了 CPU —— 这是我们唯一剩下的「会不会卡游戏」的风险点。
//
// 用法： probe_onnx_threads <batch> <len> <intraThreads>   (intraThreads=0 表示用引擎默认)
Console.OutputEncoding = System.Text.Encoding.UTF8;

string tokRoot = @"D:\AWAKE-Dev\AWAKE\tools\_af_tokenizer_20260916";
string onnxRoot = @"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AnimusForge\ONNX";
string model = Path.Combine(onnxRoot, "model.onnx");

int batch = args.Length > 0 ? int.Parse(args[0]) : 20;
int len = args.Length > 1 ? int.Parse(args[1]) : 512;
int intra = args.Length > 2 ? int.Parse(args[2]) : 0;

int logical = Environment.ProcessorCount;

var so = new SessionOptions();
if (intra > 0)
{
    so.IntraOpNumThreads = intra;
    so.InterOpNumThreads = 1;
}

var session = new InferenceSession(model, so);

var options = new BertOptions
{
    LowerCaseBeforeTokenization = false,
    ApplyBasicTokenization = true,
    IndividuallyTokenizeCjk = true,
    ClassificationToken = "[CLS]",
    SeparatorToken = "[SEP]",
    PaddingToken = "[PAD]",
    MaskingToken = "[MASK]",
    UnknownToken = "[UNK]",
};
var tk = BertTokenizer.Create(Path.Combine(tokRoot, "vocab.txt"), options);

var ids = new long[batch * len];
var mask = new long[batch * len];
var types = new long[batch * len];
for (int i = 0; i < ids.Length; i++) { ids[i] = 7222; mask[i] = 1; }
for (int b = 0; b < batch; b++) { ids[b * len] = tk.ClassificationTokenId; ids[b * len + len - 1] = tk.SeparatorTokenId; }

var inputs = new List<NamedOnnxValue>
{
    NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(ids, new[] { batch, len })),
    NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(mask, new[] { batch, len })),
};
if (session.InputMetadata.ContainsKey("token_type_ids"))
    inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", new DenseTensor<long>(types, new[] { batch, len })));

int ThreadCount()
{
    using var p = Process.GetCurrentProcess();
    p.Refresh();
    return p.Threads.Count;
}

(TimeSpan cpu, double wall) RunOnce()
{
    using var p = Process.GetCurrentProcess();
    p.Refresh();
    var c0 = p.TotalProcessorTime;
    var w = Stopwatch.StartNew();
    using var _ = session.Run(inputs);
    w.Stop();
    p.Refresh();
    return (p.TotalProcessorTime - c0, w.Elapsed.TotalSeconds);
}

int threadsBefore = ThreadCount();
// 预热一次（首次会建线程池、分配 arena）
RunOnce();
int threadsAfterWarm = ThreadCount();

// 正式量 3 次
var cpuList = new List<double>();
var wallList = new List<double>();
for (int i = 0; i < 3; i++)
{
    var (cpu, wall) = RunOnce();
    cpuList.Add(cpu.TotalSeconds);
    wallList.Add(wall);
}

double cpuAvg = cpuList.Average();
double wallAvg = wallList.Average();
double coresUsed = cpuAvg / wallAvg;

Console.WriteLine(string.Join("\t", new[]
{
    "THREADS",
    "逻辑核=" + logical,
    "设定intra=" + (intra == 0 ? "默认" : intra.ToString()),
    "建会话前线程=" + threadsBefore,
    "预热后线程=" + threadsAfterWarm,
    "线程增量=" + (threadsAfterWarm - threadsBefore),
    "CPU秒=" + cpuAvg.ToString("F2"),
    "墙钟秒=" + wallAvg.ToString("F3"),
    "占用核数=" + coresUsed.ToString("F2"),
    "占逻辑核比=" + (100.0 * coresUsed / logical).ToString("F0") + "%",
}));

session.Dispose();
