// Copyright (c) 2026 Koichi Kobayashi
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace CertificateSearch.Navigation;

internal static class NavigationDiagnostics
{
    private const string DailyLogPrefix = "navigation-";
    private const string LogExtension = ".log";
    private const int RetainedCalendarDays = 7;

    private static string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CertificateSearch");

    public static string LogPath => Path.Combine(LogDirectory, GetLogFileName(DateTimeOffset.Now));

    public static void Write(string message)
    {
        try
        {
            var now = DateTimeOffset.Now;
            Directory.CreateDirectory(LogDirectory);
            var path = Path.Combine(LogDirectory, GetLogFileName(now));
            using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(stream);
            writer.WriteLine($"{now:O} [{Environment.ProcessId}] {message}");

            RemoveExpiredDailyLogs(DateOnly.FromDateTime(now.Date));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Certificate navigation diagnostics unavailable: {exception.Message}");
        }
    }

    private static string GetLogFileName(DateTimeOffset now) =>
        $"{DailyLogPrefix}{now:yyyy-MM-dd}{LogExtension}";

    private static void RemoveExpiredDailyLogs(DateOnly today)
    {
        var oldestRetainedDay = today.AddDays(1 - RetainedCalendarDays);
        foreach (var path in Directory.EnumerateFiles(LogDirectory, $"{DailyLogPrefix}*{LogExtension}"))
        {
            var name = Path.GetFileName(path);
            if (name.Length != DailyLogPrefix.Length + 10 + LogExtension.Length ||
                !name.StartsWith(DailyLogPrefix, StringComparison.Ordinal) ||
                !name.EndsWith(LogExtension, StringComparison.Ordinal))
            {
                continue;
            }

            var dateText = name.Substring(DailyLogPrefix.Length, 10);
            if (DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var fileDate) && fileDate < oldestRetainedDay)
            {
                File.Delete(path);
            }
        }

        // The previous version used one undated file in this directory.
        var legacyPath = Path.Combine(LogDirectory, "navigation.log");
        if (File.Exists(legacyPath) &&
            DateOnly.FromDateTime(File.GetLastWriteTime(legacyPath)) < oldestRetainedDay)
        {
            File.Delete(legacyPath);
        }
    }
}
