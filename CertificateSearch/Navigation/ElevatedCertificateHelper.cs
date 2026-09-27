// Copyright (c) 2026 Koichi Kobayashi
// Licensed under the MIT License.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using CertificateSearch.Certificates;

namespace CertificateSearch.Navigation;

/// <summary>Validates the selected certificate again before navigating elevated MMC.</summary>
internal static class ElevatedCertificateHelper
{
    private const int ConnectionTimeoutMilliseconds = 60000;

    public static void Run(string pipeName)
    {
        NavigationDiagnostics.Write("Helper: process started");
        try
        {
            if (!ProcessIntegrity.IsElevated || !CertificateHelperProtocol.IsValidPipeName(pipeName))
            {
                NavigationDiagnostics.Write("Helper: integrity or pipe-name validation failed");
                return;
            }
        }
        catch (Exception exception) when (exception is SecurityException or InvalidOperationException)
        {
            Trace.TraceWarning($"Certificate helper integrity check failed: {exception}");
            NavigationDiagnostics.Write($"Helper: integrity check failed ({exception.GetType().Name}, 0x{exception.HResult:X8})");
            return;
        }

        try
        {
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.In);
            client.Connect(ConnectionTimeoutMilliseconds);
            NavigationDiagnostics.Write("Helper: connected to request pipe");
            using var reader = new StreamReader(client);
            var locationText = reader.ReadLine();
            var requestingUserSid = reader.ReadLine();
            var storeName = reader.ReadLine();
            var thumbprint = reader.ReadLine();
            var selectFlag = reader.ReadLine();
            if (!CertificateHelperProtocol.IsValidRequest(locationText, requestingUserSid, storeName, thumbprint, selectFlag))
            {
                NavigationDiagnostics.Write("Helper: request validation failed");
                return;
            }

            var location = Enum.Parse<StoreLocation>(locationText!);
            if (location == StoreLocation.CurrentUser &&
                !string.Equals(requestingUserSid, WindowsIdentity.GetCurrent().User?.Value, StringComparison.OrdinalIgnoreCase))
            {
                NavigationDiagnostics.Write("Helper: Current User identity differs after elevation");
                return;
            }

            var entries = CertificateStoreReader.ReadStore(location, storeName!);
            NavigationDiagnostics.Write($"Helper: read {entries.Count} certificates from requested store");
            var matches = entries.Where(entry =>
                string.Equals(entry.Thumbprint, thumbprint, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1)
            {
                NavigationDiagnostics.Write($"Helper: certificate thumbprint matched {matches.Length} entries");
                return;
            }

            var entry = matches[0];
            var allowMetadataMatch = new CertificateSearchIndex(entries).CanMatchByVisibleMetadata(entry);
            NavigationDiagnostics.Write("Helper: starting MMC navigation");
            CertificateManagerNavigator.NavigateAndWait(entry, selectFlag == "1", allowMetadataMatch);
            NavigationDiagnostics.Write("Helper: MMC navigation finished");
        }
        catch (Exception exception) when (exception is IOException or TimeoutException or CryptographicException or SecurityException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
        {
            Trace.TraceWarning($"Elevated certificate helper failed: {exception}");
            NavigationDiagnostics.Write($"Helper: failed ({exception.GetType().Name}, 0x{exception.HResult:X8})");
        }
    }

}
