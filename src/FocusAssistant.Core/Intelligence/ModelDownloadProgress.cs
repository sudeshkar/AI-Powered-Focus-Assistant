using System;

namespace FocusAssistant.Core.Intelligence
{
    /// <summary>
    /// Progress of a model download, detailed enough to render an honest progress bar.
    /// </summary>
    /// <remarks>
    /// Carries both the per-file and overall position because the download is seven files
    /// of wildly different sizes - one of them is almost the entire 2.5GB - so a bar driven
    /// by file count alone would sit at 14% for several minutes and then jump to done.
    /// </remarks>
    public readonly record struct ModelDownloadProgress(
        string CurrentFile,
        int FileIndex,
        int FileCount,
        long BytesReceived,
        long BytesTotal,
        TimeSpan Elapsed)
    {
        public double Fraction => BytesTotal <= 0 ? 0 : Math.Clamp((double)BytesReceived / BytesTotal, 0, 1);
    }
}
