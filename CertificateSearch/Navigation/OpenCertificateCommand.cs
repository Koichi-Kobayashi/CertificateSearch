// Copyright (c) 2026 Koichi Kobayashi
// Licensed under the MIT License.

using System;
using System.ComponentModel;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using CertificateSearch.Certificates;
using CertificateSearch.Resources;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CertificateSearch.Navigation;

internal sealed partial class OpenCertificateCommand(CertificateEntry entry, bool selectCertificate, bool allowMetadataMatch = false) : InvokableCommand
{
    public override string Name => Strings.Get(selectCertificate ? "Command.OpenCertificate" : "Command.OpenStore");

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
            return CommandResult.ShowToast(Strings.Get("Error.CertificateManager"));
        }
    }
}
