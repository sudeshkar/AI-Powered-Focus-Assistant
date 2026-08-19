using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FocusAssistant.Intelligence.Embeddings
{
    /// <summary>
    /// Sentence embeddings from all-MiniLM-L6-v2, running locally on the CPU.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The tokenizer is built from <c>vocab.txt</c>, not from HuggingFace's
    /// <c>tokenizer.json</c>: Microsoft.ML.Tokenizers' <c>BertTokenizer.Create</c> takes a
    /// WordPiece vocabulary file. Pointing it at the wrong file does not throw - it
    /// produces embeddings that are quietly meaningless, which is why the cosine-similarity
    /// assertions in the self-test exist.
    /// </para>
    /// <para>
    /// Mean-pooling is masked and the result is L2-normalised. Skipping either step is the
    /// classic silent bug with this model: padding tokens drag every vector toward a common
    /// mean, and un-normalised vectors make cosine thresholds meaningless. Both failures
    /// look like "the classifier is a bit rubbish" rather than like an error.
    /// </para>
    /// <para>
    /// ORT is pinned to a single thread for both intra- and inter-op. This runs on every
    /// window switch in the background of someone else's work; taking every core to shave
    /// two milliseconds off a classification is the wrong trade.
    /// </para>
    /// </remarks>
    public sealed class MiniLmEmbeddingGenerator : IDisposable
    {
        /// <summary>MiniLM-L6-v2's hidden size, and so the length of every vector returned.</summary>
        public const int Dimensions = 384;

        /// <summary>
        /// Titles are short. Truncating well below the model's 512 limit keeps the per-call
        /// cost flat and predictable, which matters more here than the tail of a long title.
        /// </summary>
        private const int MaxTokens = 64;

        private readonly InferenceSession _session;
        private readonly BertTokenizer _tokenizer;

        // ORT sessions are documented as thread-safe for Run, but concurrent calls on a
        // single-threaded session just queue inside the native layer anyway. Serialising
        // here keeps the tensor allocations below single-threaded and obvious.
        private readonly SemaphoreSlim _gate = new(1, 1);

        private bool _disposed;

        private MiniLmEmbeddingGenerator(InferenceSession session, BertTokenizer tokenizer)
        {
            _session = session;
            _tokenizer = tokenizer;
        }

        /// <summary>
        /// Loads the model and vocabulary from a directory containing
        /// <c>model_quantized.onnx</c> and <c>vocab.txt</c>.
        /// </summary>
        public static MiniLmEmbeddingGenerator Load(string modelDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(modelDirectory);

            var modelPath = Path.Combine(modelDirectory, "model_quantized.onnx");
            var vocabPath = Path.Combine(modelDirectory, "vocab.txt");

            if (!File.Exists(modelPath))
                throw new FileNotFoundException($"Embedding model not found at {modelPath}", modelPath);
            if (!File.Exists(vocabPath))
                throw new FileNotFoundException($"Embedding vocabulary not found at {vocabPath}", vocabPath);

            var options = new SessionOptions
            {
                IntraOpNumThreads = 1,
                InterOpNumThreads = 1,
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            };

            var session = new InferenceSession(modelPath, options);

            // The checkpoint is uncased, so the tokenizer has to lower-case to match; if it
            // does not, every capitalised word becomes [UNK] and the embeddings collapse.
            var tokenizer = BertTokenizer.Create(vocabPath, new BertOptions
            {
                LowerCaseBeforeTokenization = true,
                ApplyBasicTokenization = true,
            });

            return new MiniLmEmbeddingGenerator(session, tokenizer);
        }

        /// <summary>Embeds one string into a unit-length vector of <see cref="Dimensions"/> floats.</summary>
        public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                return Embed(text);
            }
            finally
            {
                _gate.Release();
            }
        }

        private float[] Embed(string text)
        {
            var ids = _tokenizer.EncodeToIds(text ?? string.Empty).ToArray();
            if (ids.Length > MaxTokens)
                ids = ids.Take(MaxTokens).ToArray();

            var length = ids.Length;
            if (length == 0)
                return new float[Dimensions];

            var inputIds = new DenseTensor<long>([1, length]);
            var attentionMask = new DenseTensor<long>([1, length]);
            var tokenTypeIds = new DenseTensor<long>([1, length]);

            for (var i = 0; i < length; i++)
            {
                inputIds[0, i] = ids[i];
                attentionMask[0, i] = 1;
                tokenTypeIds[0, i] = 0;
            }

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
                NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask),
                NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIds),
            };

            using var results = _session.Run(inputs);
            var hidden = results.First().AsTensor<float>();

            // Masked mean pool. Every token here is real (nothing is padded, because one
            // string is encoded at a time), but the mask is applied anyway so this stays
            // correct if batching is ever added.
            var pooled = new float[Dimensions];
            var counted = 0;
            for (var t = 0; t < length; t++)
            {
                if (attentionMask[0, t] == 0)
                    continue;

                counted++;
                for (var d = 0; d < Dimensions; d++)
                    pooled[d] += hidden[0, t, d];
            }

            if (counted == 0)
                return pooled;

            for (var d = 0; d < Dimensions; d++)
                pooled[d] /= counted;

            Normalize(pooled);
            return pooled;
        }

        /// <summary>
        /// Scales to unit length in place, so cosine similarity is a plain dot product and
        /// the thresholds elsewhere mean what they say.
        /// </summary>
        private static void Normalize(float[] vector)
        {
            double sum = 0;
            foreach (var v in vector)
                sum += v * v;

            var magnitude = Math.Sqrt(sum);
            if (magnitude < 1e-9)
                return;

            for (var i = 0; i < vector.Length; i++)
                vector[i] = (float)(vector[i] / magnitude);
        }

        /// <summary>
        /// Cosine similarity of two vectors from this generator. Both are already unit
        /// length, so this is a dot product and needs no division.
        /// </summary>
        public static double CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException("Vectors must have the same length.", nameof(b));

            double dot = 0;
            for (var i = 0; i < a.Length; i++)
                dot += a[i] * b[i];

            return dot;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _session.Dispose();
            _gate.Dispose();
        }
    }
}
