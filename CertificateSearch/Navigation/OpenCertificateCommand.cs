// Copyright (c) 2026 Koichi Kobayashi
// Licensed under the MIT License.

using System;
using System.ComponentModel;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using CertificateSearch.Certificates;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CertificateSearch.Navigation;

internal sealed partial class OpenCertificateCommand(CertificateEntry entry, bool selectCertificate, bool allowMetadataMatch = false) : InvokableCommand
{
    public override string Name => selectCertificate ? "Open certificate" : "Open certificate store";

    public override ICommandResult Invoke()
    {
        try
        {
            if (entry.StoreLocation == StoreLocation.LocalMachine && !ProcessIntegrity.IsElevated)
            {
                ElevatedCertificateLauncher.Start(entry, selectCertificate);
            }
            else
            {
                CertificateManagerNavigator.Open(entry, selectCertificate, allowMetadataMatch);
            }
            return CommandResult.Hide();
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or SecurityException)
        {
            return CommandResult.ShowToast("Could not open Windows Certificate Manager.");
        }
    }
}
