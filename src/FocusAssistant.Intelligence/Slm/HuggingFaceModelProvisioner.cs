using FocusAssistant.Core.Intelligence;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace FocusAssistant.Intelligence.Slm
{
    /// <summary>
    /// Downloads the Phi-3.5-mini ONNX build from Hugging Face.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nearly three gigabytes over one connection, so every part of this assumes it will be
    /// interrupted. Each file is fetched to a <c>.partial</c> beside its destination and
    /// only moved into place once its length matches the manifest, so an interrupted
    /// download can never be mistaken for a complete one - and a truncated weights file
    /// does not fail cleanly, it crashes inside ORT's native code with a message that says
    /// nothing about the real cause.
    /// </para>
    /// <para>
    /// Resuming uses an HTTP range request against the bytes already in the partial file.
    /// Restarting a 2.7GB file because a laptop lid closed is the kind of thing that makes
    /// people give up on a feature permanently.
    /// </para>
    /// </remarks>
    public sealed class HuggingFaceModelProvisioner : IModelProvisioner
    {
        /// <summary>
        /// Free space required before starting: the download itself plus room for the
        /// partial file that briefly doubles the largest one. Running a disk to zero is a
        /// far worse outcome than refusing to start.
        /// </summary>
        private const long RequiredFreeBytes = 6L * 1024 * 1024 * 1024;

        private readonly HttpClient _http;
        private readonly ILogger<HuggingFaceModelProvisioner> _logger;
        private readonly string _targetDirectory;
        private readonly Manifest _manifest;

        public ModelAvailability Status { get; private set; } = ModelAvailability.NotDownloaded;

        public long EstimatedBytes => _manifest.Files.Sum(f => f.Size);

        public bool IsDownloaded => _manifest.Files.All(f => IsFileComplete(Path.Combine(_targetDirectory, f.Name), f.Size));

        public HuggingFaceModelProvisioner(
            HttpClient http,
            ILogger<HuggingFaceModelProvisioner> logger,
            string manifestPath,
            string targetDirectory)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _targetDirectory = targetDirectory ?? throw new ArgumentNullException(nameof(targetDirectory));

            var json = File.ReadAllText(manifestPath);
            _manifest = JsonSerializer.Deserialize<Manifest>(json)
                ?? throw new InvalidDataException($"{manifestPath} could not be read.");

            if (IsDownloaded)
                Status = ModelAvailability.Ready;
        }

        public async Task<bool> EnsureDownloadedAsync(
            IProgress<ModelDownloadProgress>? progress, CancellationToken ct = default)
        {
            if (IsDownloaded)
            {
                Status = ModelAvailability.Ready;
                return true;
            }

            try
            {
                Directory.CreateDirectory(_targetDirectory);
                EnsureEnoughDisk();

                Status = ModelAvailability.Downloading;

                var stopwatch = Stopwatch.StartNew();
                var total = EstimatedBytes;
                var completedBefore = _manifest.Files
                    .Where(f => IsFileComplete(Path.Combine(_targetDirectory, f.Name), f.Size))
                    .Sum(f => f.Size);

                for (var i = 0; i < _manifest.Files.Count; i++)
                {
                    var file = _manifest.Files[i];
                    var destination = Path.Combine(_targetDirectory, file.Name);

                    if (IsFileComplete(destination, file.Size))
                        continue;

                    await DownloadFileAsync(file, destination, i, completedBefore, total, stopwatch, progress, ct)
                        .ConfigureAwait(false);

                    completedBefore += file.Size;
                }

                Status = ModelAvailability.Ready;
                _logger.LogInformation("Model download complete ({Bytes:N0} bytes in {Elapsed})",
                    total, stopwatch.Elapsed);
                return true;
            }
            catch (OperationCanceledException)
            {
                // A cancelled download is not a failure; the partial files stay put so the
                // next attempt resumes.
                Status = IsDownloaded ? ModelAvailability.Ready : ModelAvailability.NotDownloaded;
                _logger.LogInformation("Model download cancelled; partial files kept for resume");
                throw;
            }
            catch (Exception ex)
            {
                Status = ModelAvailability.Failed;
                _logger.LogError(ex, "Model download failed");
                return false;
            }
        }

        private async Task DownloadFileAsync(
            ManifestFile file,
            string destination,
            int index,
            long completedBefore,
            long total,
            Stopwatch stopwatch,
            IProgress<ModelDownloadProgress>? progress,
            CancellationToken ct)
        {
            var partial = destination + ".partial";
            var existing = File.Exists(partial) ? new FileInfo(partial).Length : 0;

            // A partial longer than the target means the manifest and the file disagree;
            // trusting it would produce a corrupt model, so start that file again.
            if (existing > file.Size)
            {
                File.Delete(partial);
                existing = 0;
            }

            var url = $"https://huggingface.co/{_manifest.Repository}/resolve/{_manifest.Revision}/" +
                      $"{_manifest.Directory}/{file.Name}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (existing > 0)
            {
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existing, null);
                _logger.LogInformation("Resuming {File} at {Bytes:N0} bytes", file.Name, existing);
            }

            using var response = await _http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            // A server that ignores the range header answers 200 and sends the whole file;
            // appending that to what we have would corrupt it.
            if (existing > 0 && response.StatusCode != HttpStatusCode.PartialContent)
            {
                _logger.LogInformation("Range request not honoured for {File}; restarting it", file.Name);
                File.Delete(partial);
                existing = 0;
            }

            response.EnsureSuccessStatusCode();

            await using (var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            await using (var target = new FileStream(
                partial,
                existing > 0 ? FileMode.Append : FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1024 * 128,
                useAsync: true))
            {
                var buffer = new byte[1024 * 128];
                var written = existing;
                var lastReport = 0L;

                int read;
                while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    written += read;

                    // Reporting every chunk would post thousands of updates a second at
                    // these sizes; a few megabytes apart is plenty for a progress bar.
                    if (written - lastReport >= 4 * 1024 * 1024)
                    {
                        lastReport = written;
                        progress?.Report(new ModelDownloadProgress(
                            file.Name, index + 1, _manifest.Files.Count,
                            completedBefore + written, total, stopwatch.Elapsed));
                    }
                }
            }

            var actual = new FileInfo(partial).Length;
            if (actual != file.Size)
                throw new InvalidDataException(
                    $"{file.Name} downloaded as {actual:N0} bytes, expected {file.Size:N0}.");

            File.Move(partial, destination, overwrite: true);

            progress?.Report(new ModelDownloadProgress(
                file.Name, index + 1, _manifest.Files.Count,
                completedBefore + file.Size, total, stopwatch.Elapsed));
        }

        public Task DeleteAsync(CancellationToken ct = default)
        {
            try
            {
                if (Directory.Exists(_targetDirectory))
                    Directory.Delete(_targetDirectory, recursive: true);

                Status = ModelAvailability.NotDownloaded;
                _logger.LogInformation("Local language model deleted");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not delete the local language model");
            }

            return Task.CompletedTask;
        }

        private void EnsureEnoughDisk()
        {
            var root = Path.GetPathRoot(Path.GetFullPath(_targetDirectory));
            if (string.IsNullOrEmpty(root))
                return;

            var free = new DriveInfo(root).AvailableFreeSpace;
            if (free < RequiredFreeBytes)
                throw new IOException(
                    $"Not enough disk space: {free / (1024 * 1024 * 1024)} GB free, " +
                    $"{RequiredFreeBytes / (1024 * 1024 * 1024)} GB needed.");
        }

        private static bool IsFileComplete(string path, long expectedSize) =>
            File.Exists(path) && new FileInfo(path).Length == expectedSize;

        private sealed class Manifest
        {
            [JsonPropertyName("repository")] public string Repository { get; set; } = "";
            [JsonPropertyName("revision")] public string Revision { get; set; } = "main";
            [JsonPropertyName("directory")] public string Directory { get; set; } = "";
            [JsonPropertyName("files")] public List<ManifestFile> Files { get; set; } = [];
        }

        private sealed class ManifestFile
        {
            [JsonPropertyName("name")] public string Name { get; set; } = "";
            [JsonPropertyName("size")] public long Size { get; set; }
        }
    }
}
