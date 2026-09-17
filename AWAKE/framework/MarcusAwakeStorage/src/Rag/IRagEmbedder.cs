using System;
using System.Collections.Generic;
using System.Threading;

namespace MarcusAwakeStorage
{
    /// <summary>
    /// Turns text into normalized embedding vectors for <see cref="SqliteStorageAndRagBackend"/>'s
    /// semantic retrieval mode.
    ///
    /// Why it is an interface and not a concrete class wired into the backend: ONNX and a 90 MB
    /// model must not become a hard dependency of the storage component. A backend built without an
    /// embedder keeps answering <c>rag.retrieval_mode_unsupported</c> exactly as before, and an
    /// offline harness can host the real backend with the real embedder in-process — same code,
    /// different binding, no stand-ins.
    /// </summary>
    public interface IRagEmbedder : IDisposable
    {
        /// <summary>
        /// Identity of the embedding function: model + dimension + how the passages are composed.
        /// Stored next to every vector, so swapping the model (or changing the passage composition)
        /// invalidates the cached vectors instead of silently mixing two vector spaces.
        /// </summary>
        string ModelId { get; }

        /// <summary>Vector length. Must not change for a given <see cref="ModelId"/>.</summary>
        int Dimension { get; }

        /// <summary>
        /// Encodes a batch of texts. Returns one L2-normalized vector per input, in input order.
        /// Cosine similarity is therefore a plain dot product.
        /// </summary>
        float[][] Encode(IReadOnlyList<string> texts, CancellationToken cancellationToken);
    }
}
