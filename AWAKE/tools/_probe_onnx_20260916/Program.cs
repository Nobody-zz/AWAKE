using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

// 端到端验收：AF 随包分发的那套 ONNX 运行库 + 导出的 vocab.txt + 官方 BertTokenizer
// 能不能真把 bge-small-zh 跑起来，并且「两句不同的话算出不同的向量」。
//
// 这是本项目 08-15 记下的那条坑的正面判据：
//   自写分词出错 ⇒ 静默退化成常量向量、不抛错。
//   ⇒ 所以「没报错」不算通过，必须看句子之间向量是否真的分得开。
Console.OutputEncoding = System.Text.Encoding.UTF8;

string tokRoot = @"D:\AWAKE-Dev\AWAKE\tools\_af_tokenizer_20260916";
string onnxRoot = @"D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\AnimusForge\ONNX";
string vocab = Path.Combine(tokRoot, "vocab.txt");
string model = Path.Combine(onnxRoot, "model.onnx");

var jsonOpt = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

// ---- 1. 分词器 ----
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
BertTokenizer tk = BertTokenizer.Create(vocab, options);
Console.WriteLine("分词器 ok  [CLS]=" + tk.ClassificationTokenId + " [SEP]=" + tk.SeparatorTokenId
                  + " [UNK]=" + tk.UnknownTokenId);

// ---- 2. 运行库 ----
Console.WriteLine("ONNX Runtime 版本 = " + OrtEnv.Instance().GetVersionString());
using var session = new InferenceSession(model);
Console.WriteLine("模型已加载，输入 = " + string.Join(", ", session.InputMetadata.Keys));
Console.WriteLine("           输出 = " + string.Join(", ", session.OutputMetadata.Keys));
Console.WriteLine();

// ---- 3. 探针句子：必须两两不同，且要有「意思相近」「意思无关」两类 ----
string[] probes =
{
    "圆顶锅盔是什么",
    "圆顶锅盔多少钱",
    "斯特吉亚的军队怎么打仗",
    "怎么做面包",
    "阿雷尼科斯是谁",
    "今天天气不错",
};

int maxLen = probes.Max(p => tk.EncodeToIds(p, true, true, true).Count);
Console.WriteLine("各句 token 数（含 [CLS]/[SEP]）：");
foreach (string p in probes)
    Console.WriteLine("  " + p.PadRight(16) + " " + tk.EncodeToIds(p, true, true, true).Count);
Console.WriteLine("批长度 = " + maxLen);
Console.WriteLine();

// 手动组 batch：input_ids / attention_mask / token_type_ids，右填充。
// DenseTensor 只认扁平数组 + 形状，所以按 i*maxLen+j 手工铺。
int n = probes.Length;
var ids = new long[n * maxLen];
var mask = new long[n * maxLen];
var types = new long[n * maxLen];
var shape = new[] { n, maxLen };

int padId = tk.PaddingTokenId;
for (int i = 0; i < n; i++)
{
    IReadOnlyList<int> seq = tk.EncodeToIds(probes[i], true, true, true);
    for (int j = 0; j < maxLen; j++)
    {
        int k = i * maxLen + j;
        if (j < seq.Count)
        {
            ids[k] = seq[j];
            mask[k] = 1;
        }
        else
        {
            ids[k] = padId;
            mask[k] = 0;
        }
        types[k] = 0;
    }
}

var inputs = new List<NamedOnnxValue>
{
    NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(ids, shape)),
    NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(mask, shape)),
};
if (session.InputMetadata.ContainsKey("token_type_ids"))
{
    inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", new DenseTensor<long>(types, shape)));
}

using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = session.Run(inputs);

// ---- 4. 取 [CLS] 向量（bge 系列用 [CLS]，等价于 pooler 输出）----
var outName = session.OutputMetadata.ContainsKey("last_hidden_state")
    ? "last_hidden_state"
    : session.OutputMetadata.Keys.First();
var hidden = results.First(r => r.Name == outName).AsTensor<float>();
Console.WriteLine("输出张量 " + outName + " 形状 = [" + string.Join(", ", hidden.Dimensions.ToArray()) + "]");

int dim = hidden.Dimensions[2];
var vecs = new float[n][];
for (int i = 0; i < n; i++)
{
    var v = new float[dim];
    for (int d = 0; d < dim; d++) v[d] = hidden[i, 0, d];
    // 归一化（检索要余弦，先归一化省事）
    double norm = Math.Sqrt(v.Sum(x => (double)x * x));
    for (int d = 0; d < dim; d++) v[d] = (float)(v[d] / norm);
    vecs[i] = v;
}

// ---- 5. 判据 ----
// 判据 A：不能所有句子算出同一个向量（静默退化成常量向量）
int uniq = vecs.Select(v => string.Join(",", v.Select(x => x.ToString("F6")))).Distinct().Count();
Console.WriteLine();
Console.WriteLine("判据A 不同向量数 = " + uniq + " / " + n + "  => " + (uniq > 1 ? "PASS" : "FAIL"));

// 判据 B：向量本身不能全 0 / 全 NaN
bool degenerate = vecs.Any(v => v.All(x => x == 0f) || v.Any(float.IsNaN));
Console.WriteLine("判据B 无全零/NaN 向量 = " + (!degenerate ? "PASS" : "FAIL"));

// 判据 C：相近句子的相似度要高于无关句子
double Cos(float[] a, float[] b)
{
    double s = 0;
    for (int d = 0; d < a.Length; d++) s += (double)a[d] * b[d];
    return s;
}
double s_near = Cos(vecs[0], vecs[1]);    // 圆顶锅盔是什么 / 圆顶锅盔多少钱
double s_far = Cos(vecs[0], vecs[5]);     // 圆顶锅盔是什么 / 今天天气不错
Console.WriteLine("判据C 相近句相似度 = " + s_near.ToString("F4")
                  + "，无关句相似度 = " + s_far.ToString("F4")
                  + "  => " + (s_near > s_far ? "PASS" : "FAIL"));

// 判据 D：语义应当正相关但不恒等 —— 同一对句子的相似度不能是 1.0（那就是常量向量）
Console.WriteLine("判据D 相近句相似度 < 1.0 = " + (s_near < 0.9999 ? "PASS" : "FAIL"));

Console.WriteLine();
Console.WriteLine("两两余弦（归一化后即点积）：");
Console.WriteLine("        " + string.Join(" ", probes.Select((_, i) => ("[" + i + "]").PadLeft(7))));
for (int i = 0; i < n; i++)
{
    var row = new List<string> { "[" + i + "] " + probes[i].PadRight(9) };
    for (int j = 0; j < n; j++) row.Add(Cos(vecs[i], vecs[j]).ToString("F3").PadLeft(7));
    Console.WriteLine(string.Join(" ", row));
}

// ---- 6. 附：模型自带 sentence_embedding 出口，如果和 [CLS] 一致就没必要自己池化 ----
Console.WriteLine();
if (session.OutputMetadata.ContainsKey("sentence_embedding"))
{
    var se = results.First(r => r.Name == "sentence_embedding").AsTensor<float>();
    Console.WriteLine("sentence_embedding 形状 = [" + string.Join(", ", se.Dimensions.ToArray()) + "]");
    int seDim = se.Dimensions[se.Dimensions.Length - 1];
    var sv = new float[seDim];
    for (int d = 0; d < seDim; d++) sv[d] = se[0, d];
    double sn = Math.Sqrt(sv.Sum(x => (double)x * x));
    for (int d = 0; d < seDim; d++) sv[d] = (float)(sv[d] / sn);

    var cv = new float[dim];
    for (int d = 0; d < dim; d++) cv[d] = hidden[0, 0, d];
    double cn = Math.Sqrt(cv.Sum(x => (double)x * x));
    for (int d = 0; d < dim; d++) cv[d] = (float)(cv[d] / cn);

    Console.WriteLine("sentence_embedding 与 [CLS] 的余弦 = " + Cos(sv, cv).ToString("F6")
                      + "  => " + (Cos(sv, cv) > 0.9999 ? "同一路，可直接用它省掉手工池化" : "不同，需确认该用哪个"));
}
else
{
    Console.WriteLine("模型没有 sentence_embedding 出口 ⇒ 只能自己取 [CLS] 或做均值池化。");
}

File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "embedding.json"),
    JsonSerializer.Serialize(new
    {
        ort_version = OrtEnv.Instance().GetVersionString(),
        inputs = session.InputMetadata.Keys.ToArray(),
        outputs = session.OutputMetadata.Keys.ToArray(),
        dim,
        probes,
        pairwise_cosine = Enumerable.Range(0, n)
            .Select(i => Enumerable.Range(0, n).Select(k => Math.Round(Cos(vecs[i], vecs[k]), 4)).ToArray())
            .ToArray(),
        first16 = vecs[0].Take(16).Select(x => Math.Round(x, 6)).ToArray(),
    }, jsonOpt));
Console.WriteLine();
Console.WriteLine("已写 embedding.json  (" + dim + " 维，取 [CLS])");

// ---- 7. 内存账：权重常驻 + 运行时开销 + 大批量瞬时峰值 ----
// 用来回答「重排器对内存要求大不大」。
session.Dispose();
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();

long Mem()
{
    using var p = System.Diagnostics.Process.GetCurrentProcess();
    p.Refresh();
    return p.PrivateMemorySize64;
}

Console.WriteLine();
Console.WriteLine("=== 内存账（本进程私有内存）===");
long baseMem = Mem();
Console.WriteLine("  起跑基线（已释放上一个会话，GC 后）  " + (baseMem / 1024.0 / 1024).ToString("F1") + " MB");

long beforeLoad = Mem();
var s2 = new InferenceSession(model);
long afterLoad = Mem();
Console.WriteLine("  加载模型后                          " + (afterLoad / 1024.0 / 1024).ToString("F1") + " MB"
                  + "   （增量 " + ((afterLoad - beforeLoad) / 1024.0 / 1024).ToString("F1") + " MB）");

// 权重文件本身多大，对照一下"增量里有多少是权重、多少是运行时"
long weightBytes = new FileInfo(model).Length
                 + (File.Exists(model + "_data") ? new FileInfo(model + "_data").Length : 0);
Console.WriteLine("  权重文件本身                        " + (weightBytes / 1024.0 / 1024).ToString("F1") + " MB"
                  + "   ⇒ 运行时额外开销 "
                  + ((afterLoad - beforeLoad - weightBytes) / 1024.0 / 1024).ToString("F1") + " MB");

// 模拟重排器那种"一次过 20 条候选、每条满 512 token"的批量
int rerankBatch = 20, rerankLen = 512;
var bigIds = new long[rerankBatch * rerankLen];
var bigMask = new long[rerankBatch * rerankLen];
var bigTypes = new long[rerankBatch * rerankLen];
var bigShape = new[] { rerankBatch, rerankLen };
for (int i = 0; i < rerankBatch * rerankLen; i++) { bigIds[i] = 7222; bigMask[i] = 1; }
bigIds[0] = tk.ClassificationTokenId;
bigIds[rerankLen - 1] = tk.SeparatorTokenId;

long beforeRun = Mem();
var bigIn = new List<NamedOnnxValue>
{
    NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(bigIds, bigShape)),
    NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(bigMask, bigShape)),
};
if (s2.InputMetadata.ContainsKey("token_type_ids"))
    bigIn.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", new DenseTensor<long>(bigTypes, bigShape)));

using (var bigOut = s2.Run(bigIn))
{
    long peak = Mem();
    Console.WriteLine("  跑 " + rerankBatch + " 条 × " + rerankLen + " token 时          "
                      + (peak / 1024.0 / 1024).ToString("F1") + " MB"
                      + "   （相对加载后增量 " + ((peak - afterLoad) / 1024.0 / 1024).ToString("F1") + " MB）");
    Console.WriteLine("     输出张量维数 = " + bigOut.First(
        r => r.Name == outName).AsTensor<float>().Dimensions[2]);
}

long beforeDispose = Mem();
s2.Dispose();
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
Console.WriteLine("  释放后                              " + (Mem() / 1024.0 / 1024).ToString("F1") + " MB"
                  + "   （未回落 "
                  + ((beforeDispose - Mem()) / 1024.0 / 1024).ToString("F1") + " MB 由运行时池持有）");

