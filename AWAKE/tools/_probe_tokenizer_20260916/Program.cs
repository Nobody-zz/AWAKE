using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.ML.Tokenizers;

// 分词验收台（C# 侧）。探针从 probes.json 读，避免把中文塞进源码。
// 目的：证明「导出的 vocab.txt + 官方 BertTokenizer」这条路，
//       对真实查询是否与模型的原始分词（HF 原生）一致。
Console.OutputEncoding = System.Text.Encoding.UTF8;

string root = @"D:\AWAKE-Dev\AWAKE\tools\_af_tokenizer_20260916";
string vocab = Path.Combine(root, "vocab.txt");
string probesPath = Path.Combine(root, "probes.json");

var jsonIn = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
string[] probes = JsonSerializer.Deserialize<string[]>(File.ReadAllText(probesPath), jsonIn)!;

// 这几项必须与 tokenizer_config.json 一一对应，否则分词会静默走偏。
var options = new BertOptions
{
    LowerCaseBeforeTokenization = false,   // tokenizer_config.json: do_lower_case = false
    ApplyBasicTokenization = true,         // tokenizer_config.json: do_basic_tokenize = true
    IndividuallyTokenizeCjk = true,        // tokenizer_config.json: tokenize_chinese_chars = true
    ClassificationToken = "[CLS]",
    SeparatorToken = "[SEP]",
    PaddingToken = "[PAD]",
    MaskingToken = "[MASK]",
    UnknownToken = "[UNK]",
};

BertTokenizer tk = BertTokenizer.Create(vocab, options);
Console.WriteLine("分词器已装配");
Console.WriteLine("  LowerCaseBeforeTokenization = " + tk.LowerCaseBeforeTokenization + "  (期望 False)");
Console.WriteLine("  ApplyBasicTokenization      = " + tk.ApplyBasicTokenization + "  (期望 True)");
Console.WriteLine("  IndividuallyTokenizeCjk     = " + tk.IndividuallyTokenizeCjk + "  (期望 True)");
Console.WriteLine("  UnknownTokenId              = " + tk.UnknownTokenId + "  (期望 100)");
Console.WriteLine("  ClassificationTokenId       = " + tk.ClassificationTokenId + "  (期望 101)");
Console.WriteLine("  SeparatorTokenId            = " + tk.SeparatorTokenId + "  (期望 102)");
Console.WriteLine("  MaxInputCharsPerWord        = " + tk.MaxInputCharsPerWord);
Console.WriteLine("  探针条数                    = " + probes.Length);
Console.WriteLine();

var first = new Dictionary<string, int[]>();
var second = new Dictionary<string, int[]>();
foreach (string text in probes)
{
    first[text] = tk.EncodeToIds(text, true, true, true).ToArray();
    second[text] = tk.EncodeToIds(text, true, true, true).ToArray();
}

// 判据 1：不能所有输入都算出同一个序列（防「静默退化成常量向量」）
int distinct = first.Values.Select(v => string.Join(",", v)).Distinct().Count();
Console.WriteLine("判据1 不同序列数 = " + distinct + " / " + probes.Length
                  + "  => " + (distinct > 1 ? "PASS" : "FAIL"));

// 判据 2：可复现
bool repro = probes.All(p => first[p].SequenceEqual(second[p]));
Console.WriteLine("判据2 同输入两次结果一致 = " + (repro ? "PASS" : "FAIL"));

// 判据 3：不能整句退化成 [UNK]
int unkCount = first.Values.Sum(v => v.Count(id => id == tk.UnknownTokenId));
Console.WriteLine("判据3 [UNK] 总数 = " + unkCount);

// 判据 4：单字符输入不能被整字吞掉（吞掉 = 不报错、不产生 [UNK]，就是没了）
int lost = 0;
foreach (string text in probes)
{
    int cps = 0;
    for (int i = 0; i < text.Length; i++)
    {
        cps += char.IsHighSurrogate(text[i]) ? 1 : (char.IsLowSurrogate(text[i]) ? 0 : 1);
    }
    if (cps != 1) continue;
    if (first[text].Length - 2 == 0) lost++;
}
Console.WriteLine("判据4 被整字吞掉的单字符输入 = " + lost + " 条（0 为最佳）");

File.WriteAllText(Path.Combine(root, "csharp.json"),
    JsonSerializer.Serialize(first, new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    }));
Console.WriteLine();
Console.WriteLine("已写 csharp.json");
