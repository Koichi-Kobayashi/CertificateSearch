// Copyright (c) 2026 Koichi Kobayashi
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using CertificateSearch.Certificates;

namespace CertificateSearch.Navigation;

/// <summary>Starts the packaged executable with the same integrity level as certlm.msc.</summary>
internal static class ElevatedCertificateLauncher
{
    private const int ConnectionTimeoutMilliseconds = 60000;

    public static void Start(CertificateEntry entry, bool selectCertificate)
    {
        var pipeName = $"{CertificateHelperProtocol.PipePrefix}{Guid.NewGuid():N}";
        NavigationDiagnostics.Write("Launcher: creating request pipe");
        var server = new NamedPipeServerStream(
            pipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
        var cancellation = new CancellationTokenSource(ConnectionTimeoutMilliseconds);
        _ = SendRequestAsync(server, cancellation, entry, selectCertificate);

        try
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(executablePath))
            {
                throw new InvalidOperationException("The extension executable path is unavailable.");
            }

            _ = Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = $"--elevated-certificate-helper {pipeName}",
                UseShellExecute = true,
                Verb = "runas",
            }) ?? throw new InvalidOperationException("The elevated certificate helper did not start.");
            NavigationDiagnostics.Write("Launcher: UAC helper started");
        }
        catch (Exception exception)
        {
            NavigationDiagnostics.Write($"Launcher: start failed ({exception.GetType().Name}, 0x{exception.HResult:X8})");
            cancellation.Cancel();
            server.Dispose();
            throw;
        }
    }

    private static async Task SendRequestAsync(
        NamedPipeServerStream server,
        CancellationTokenSource cancellation,
        CertificateEntry entry,
        bool selectCertificate)
    {
        try
        {
            await server.WaitForConnectionAsync(cancellation.Token).ConfigureAwait(false);
            NavigationDiagnostics.Write("Launcher: helper connected");
            await using var writer = new StreamWriter(server, leaveOpen: true) { AutoFlush = true };
            await writer.WriteLineAsync(entry.StoreName).ConfigureAwait(false);
            await writer.WriteLineAsync(entry.Thumbprint).ConfigureAwait(false);
            await writer.WriteLineAsync(selectCertificate ? "1" : "0").ConfigureAwait(false);
            NavigationDiagnostics.Write("Launcher: request sent");
        }
        catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException or IOException)
        {
            Trace.TraceWarning($"Certificate helper request failed: {exception}");
            NavigationDiagnostics.Write($"Launcher: request failed ({exception.GetType().Name}, 0x{exception.HResult:X8})");
        }
        finally
        {
            server.Dispose();
            cancellation.Dispose();
        }
    }
}
