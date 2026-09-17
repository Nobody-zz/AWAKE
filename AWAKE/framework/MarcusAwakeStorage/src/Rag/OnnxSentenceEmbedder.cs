using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace MarcusAwakeStorage
{
    /// <summary>Where the model, the weights sidecar and the vocabulary live.</summary>
    public sealed class OnnxEmbedderOptions
    {
        /// <summary>Directory holding <c>model.onnx</c> (and, for BGE exports, <c>model.onnx_data</c>).</summary>
        public string ModelDirectory { get; set; }

        /// <summary>File name of the graph inside <see cref="ModelDirectory"/>.</summary>
        public string ModelFileName { get; set; } = "model.onnx";

        /// <summary>
        /// Exported <c>vocab.txt</c> — absolute, or relative to <see cref="ModelDirectory"/>.
        /// Left empty, it is read from the model directory.
        /// </summary>
        public string VocabularyPath { get; set; }

        /// <summary>
        /// Stamped onto every stored vector so a model swap invalidates the cache.
        /// Compose it from the model name, the dimension and the passage-composition revision.
        /// </summary>
        public string ModelId { get; set; }

        /// <summary>Hard cap on sequence length. Matches <c>max_position_embeddings</c>.</summary>
        public int MaxSequenceLength { get; set; } = 512;

        /// <summary>Passages per inference call. Only affects peak memory, not results.</summary>
        public int BatchSize { get; set; } = 16;

        /// <summary>
        /// MUST be set explicitly. Left at the default, ONNX Runtime opens roughly (logical cores / 2)
        /// intra-op threads for a query that takes milliseconds — measured at ~12 threads on this box,
        /// stealing cores from the game process for no benefit.
        /// </summary>
        public int IntraOpNumThreads { get; set; } = 2;

        /// <summary>Single session, single graph — parallel operators buy nothing here.</summary>
        public int InterOpNumThreads { get; set; } = 1;

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(ModelDirectory)) throw new ArgumentException("A model directory is required.", nameof(ModelDirectory));
            if (string.IsNullOrWhiteSpace(ModelId)) throw new ArgumentException("A model identity is required.", nameof(ModelId));
            if (MaxSequenceLength < 8) throw new ArgumentOutOfRangeException(nameof(MaxSequenceLength));
            if (BatchSize < 1) throw new ArgumentOutOfRangeException(nameof(BatchSize));
            if (IntraOpNumThreads < 1) throw new ArgumentOutOfRangeException(nameof(IntraOpNumThreads));
            if (InterOpNumThreads < 1) throw new ArgumentOutOfRangeException(nameof(InterOpNumThreads));
        }
    }

    /// <summary>
    /// BERT-family sentence embedder over ONNX Runtime, using the vocabulary exported from
    /// <c>tokenizer.json</c> and the official <see cref="BertTokenizer"/>.
    ///
    /// Composition follows the model family, not guesswork:
    ///   * tokenizer options mirror <c>tokenizer_config.json</c> one field at a time (see
    ///     <c>docs/DESIGN-20260916-ONNX运行库与分词落地.md</c> §2.3) — a wrong flag does not throw,
    ///     it silently produces a different, useless vector;
    ///   * pooling is <c>[CLS]</c> (position 0). Mean pooling was measured to be worse;
    ///   * the result is L2-normalized, so retrieval is a dot product.
    ///
    /// The session is created lazily on the first encode and then kept for the process lifetime:
    /// building it reads ~90 MB, so re-creating it per request would stall every call.
    /// </summary>
    public sealed class OnnxSentenceEmbedder : IRagEmbedder
    {
        private const string ClassificationDefault = "[CLS]";
        private const string SeparatorDefault = "[SEP]";
        private const string PaddingDefault = "[PAD]";
        private const string MaskingDefault = "[MASK]";
        private const string UnknownDefault = "[UNK]";

        private readonly OnnxEmbedderOptions options;
        private readonly object gate = new object();
        private readonly BertTokenizer tokenizer;
        private InferenceSession session;
        private string embeddingOutputName;
        private string hiddenStateOutputName;
        private int dimension;
        private bool disposed;

        public OnnxSentenceEmbedder(OnnxEmbedderOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            options.Validate();
            this.options = options;

            var vocabularyPath = string.IsNullOrWhiteSpace(options.VocabularyPath)
                ? Path.Combine(options.ModelDirectory, "vocab.txt")
                : (Path.IsPathRooted(options.VocabularyPath)
                    ? options.VocabularyPath
                    : Path.Combine(options.ModelDirectory, options.VocabularyPath));
            if (!File.Exists(vocabularyPath))
            {
                throw new FileNotFoundException("The exported vocabulary is missing.", vocabularyPath);
            }

            var modelPath = Path.Combine(options.ModelDirectory, options.ModelFileName);
            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException("The ONNX graph is missing.", modelPath);
            }

            // Exported BGE graphs carry their weights in a sidecar; without it the session cannot load
            // and the failure reads as a generic ONNX error, so name it here.
            var dataPath = modelPath + "_data";
            if (!File.Exists(dataPath) && !ContainsEmbeddedWeights(modelPath))
            {
                throw new FileNotFoundException("The ONNX external weights sidecar is missing.", dataPath);
            }

            tokenizer = BertTokenizer.Create(vocabularyPath, new BertOptions
            {
                LowerCaseBeforeTokenization = false,
                ApplyBasicTokenization = true,
                IndividuallyTokenizeCjk = true,
                ClassificationToken = ClassificationDefault,
                SeparatorToken = SeparatorDefault,
                PaddingToken = PaddingDefault,
                MaskingToken = MaskingDefault,
                UnknownToken = UnknownDefault,
            });
        }

        public string ModelId => options.ModelId;

        public int Dimension
        {
            get
            {
                EnsureSession();
                return dimension;
            }
        }

        public float[][] Encode(IReadOnlyList<string> texts, CancellationToken cancellationToken)
        {
            if (texts == null || texts.Count == 0) return Array.Empty<float[]>();
            if (disposed) throw new ObjectDisposedException(nameof(OnnxSentenceEmbedder));
            EnsureSession();

            var result = new float[texts.Count][];
            for (var offset = 0; offset < texts.Count; offset += options.BatchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = Math.Min(options.BatchSize, texts.Count - offset);
                EncodeBatch(texts, offset, count, result);
            }
            return result;
        }

        private void EncodeBatch(IReadOnlyList<string> texts, int offset, int count, float[][] destination)
        {
            var sequences = new int[count][];
            var width = 1;
            for (var index = 0; index < count; index++)
            {
                var ids = Tokenize(texts[offset + index]);
                sequences[index] = ids;
                if (ids.Length > width) width = ids.Length;
            }

            var shape = new[] { count, width };
            var inputIds = new long[count * width];
            var attentionMask = new long[count * width];
            var tokenTypeIds = new long[count * width];
            var padId = tokenizer.PaddingTokenId;

            for (var index = 0; index < count; index++)
            {
                var ids = sequences[index];
                for (var position = 0; position < width; position++)
                {
                    var cell = index * width + position;
                    if (position < ids.Length)
                    {
                        inputIds[cell] = ids[position];
                        attentionMask[cell] = 1;
                    }
                    else
                    {
                        inputIds[cell] = padId;
                        attentionMask[cell] = 0;
                    }
                }
            }

            using var inputs = new DisposableList();
            inputs.Add(NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(inputIds, shape)));
            inputs.Add(NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(attentionMask, shape)));
            if (session.InputMetadata.ContainsKey("token_type_ids"))
            {
                inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", new DenseTensor<long>(tokenTypeIds, shape)));
            }

            using var outputs = session.Run(inputs.Values);
            var pooled = embeddingOutputName != null
                ? outputs.First(x => StringComparer.Ordinal.Equals(x.Name, embeddingOutputName)).AsTensor<float>()
                : null;
            var hidden = pooled == null
                ? outputs.First(x => StringComparer.Ordinal.Equals(x.Name, hiddenStateOutputName)).AsTensor<float>()
                : null;

            for (var index = 0; index < count; index++)
            {
                var vector = new float[dimension];
                if (pooled != null)
                {
                    for (var d = 0; d < dimension; d++) vector[d] = pooled[index, d];
                }
                else
                {
                    for (var d = 0; d < dimension; d++) vector[d] = hidden[index, 0, d];
                }
                Normalize(vector);
                destination[offset + index] = vector;
            }
        }

        /// <summary>
        /// [CLS] ... [SEP], right-truncated to the model's positional limit. Truncation is done by
        /// hand rather than trusting a tokenizer overload, so the trailing [SEP] is always present —
        /// the same shape the reference Python tokenizer produced.
        /// </summary>
        private int[] Tokenize(string text)
        {
            var ids = tokenizer.EncodeToIds(text ?? string.Empty, true, true, true);
            var limit = options.MaxSequenceLength;
            if (ids.Count <= limit)
            {
                var exact = new int[ids.Count];
                for (var index = 0; index < ids.Count; index++) exact[index] = ids[index];
                return exact;
            }

            var truncated = new int[limit];
            for (var index = 0; index < limit - 1; index++) truncated[index] = ids[index];
            truncated[limit - 1] = tokenizer.SeparatorTokenId;
            return truncated;
        }

        private static void Normalize(float[] vector)
        {
            double sum = 0;
            for (var index = 0; index < vector.Length; index++) sum += (double)vector[index] * vector[index];
            var norm = Math.Sqrt(sum);
            if (norm <= 1e-12) return;
            for (var index = 0; index < vector.Length; index++) vector[index] = (float)(vector[index] / norm);
        }

        private void EnsureSession()
        {
            if (session != null) return;
            lock (gate)
            {
                if (session != null) return;
                var modelPath = Path.Combine(options.ModelDirectory, options.ModelFileName);
                using var sessionOptions = new SessionOptions
                {
                    IntraOpNumThreads = options.IntraOpNumThreads,
                    InterOpNumThreads = options.InterOpNumThreads,
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                };
                sessionOptions.LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR;
                var created = new InferenceSession(modelPath, sessionOptions);

                if (created.OutputMetadata.ContainsKey("sentence_embedding"))
                {
                    embeddingOutputName = "sentence_embedding";
                }
                else if (created.OutputMetadata.ContainsKey("last_hidden_state"))
                {
                    hiddenStateOutputName = "last_hidden_state";
                }
                else if (created.OutputMetadata.ContainsKey("pooler_output"))
                {
                    hiddenStateOutputName = "pooler_output";
                }
                else
                {
                    created.Dispose();
                    throw new InvalidDataException("The ONNX model exposes no embedding output.");
                }

                var metadata = created.OutputMetadata[embeddingOutputName ?? hiddenStateOutputName];
                if (metadata.Dimensions.Length < 2)
                {
                    created.Dispose();
                    throw new InvalidDataException("The ONNX embedding output has an unexpected rank.");
                }
                dimension = metadata.Dimensions[metadata.Dimensions.Length - 1];
                if (dimension <= 0)
                {
                    // Symbolic dimension: fall back to reading it from a single probe.
                    dimension = ProbeDimension(created);
                }

                session = created;
            }
        }

        private int ProbeDimension(InferenceSession candidate)
        {
            var shape = new[] { 1, 2 };
            using var probe = new DisposableList();
            probe.Add(NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(new long[] { tokenizer.ClassificationTokenId, tokenizer.SeparatorTokenId }, shape)));
            probe.Add(NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(new long[] { 1L, 1L }, shape)));
            if (candidate.InputMetadata.ContainsKey("token_type_ids"))
            {
                probe.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", new DenseTensor<long>(new long[] { 0L, 0L }, shape)));
            }
            using var outputs = candidate.Run(probe.Values);
            var tensor = outputs.First(x => StringComparer.Ordinal.Equals(x.Name, embeddingOutputName ?? hiddenStateOutputName)).AsTensor<float>();
            return tensor.Dimensions[tensor.Dimensions.Length - 1];
        }

        private static bool ContainsEmbeddedWeights(string modelPath)
        {
            // A graph-only export is tiny; anything with real weights inside is far larger.
            return new FileInfo(modelPath).Length > 1024 * 1024;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            lock (gate)
            {
                session?.Dispose();
                session = null;
            }
        }

        /// <summary>Measures the distance-independent shape of a vector set: used by the harness to
        /// prove the encoder did not collapse into one constant vector.</summary>
        internal static void WriteVector(BinaryWriter writer, float[] vector)
        {
            for (var index = 0; index < vector.Length; index++) writer.Write(vector[index]);
        }

        private sealed class DisposableList : IDisposable
        {
            private readonly List<NamedOnnxValue> items = new List<NamedOnnxValue>();
            public IReadOnlyList<NamedOnnxValue> Values => items;
            public void Add(NamedOnnxValue value) => items.Add(value);

            // The named values wrap caller-owned tensors that Run only reads; dropping the references
            // is all that is needed, and it keeps this compiling against the 1.18 surface where
            // NamedOnnxValue's disposal contract is not part of the public shape we rely on.
            public void Dispose() => items.Clear();
        }
    }
}
