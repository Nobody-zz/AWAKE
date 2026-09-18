using System.Runtime.Intrinsics.X86;
static void P(string n, Func<bool> f) { try { Console.WriteLine(n.PadRight(14) + "= " + f()); } catch (Exception e) { Console.WriteLine(n.PadRight(14) + "= ? " + e.GetType().Name); } }
P("AVX", () => Avx.IsSupported);
P("AVX2", () => Avx2.IsSupported);
P("AVX512F", () => Avx512F.IsSupported);
P("AVX512BW", () => Avx512BW.IsSupported);
P("AVX512VNNI", () => AvxVnni.IsSupported);
P("FMA", () => Fma.IsSupported);
P("SSE4.2", () => Sse42.IsSupported);
Console.WriteLine("LogicalCores  = " + Environment.ProcessorCount);
