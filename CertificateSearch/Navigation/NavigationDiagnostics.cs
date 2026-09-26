// Copyright (c) 2026 Koichi Kobayashi
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;

namespace CertificateSearch.Navigation;

internal static class NavigationDiagnostics
{
#if DEBUG
    private const long MaximumLogBytes = 1024 * 1024;
#endif

    public static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CertificateSearch", "navigation.log");

    [Conditional("DEBUG")]
    public static void Write(string message)
    {
#if DEBUG
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            using var stream = new FileStream(LogPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            if (stream.Length >= MaximumLogBytes)
            {
                stream.SetLength(0);
            }

            stream.Seek(0, SeekOrigin.End);
            using var writer = new StreamWriter(stream);
            writer.WriteLine($"{DateTimeOffset.Now:O} [{Environment.ProcessId}] {message}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Certificate navigation diagnostics unavailable: {exception.Message}");
        }
#endif
    }
}
