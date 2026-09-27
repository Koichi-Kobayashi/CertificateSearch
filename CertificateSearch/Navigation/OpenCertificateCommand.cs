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
                try
                {
                    CertificateManagerNavigator.Open(entry, selectCertificate, allowMetadataMatch);
                }
                catch (Win32Exception exception) when (
                    entry.StoreLocation == StoreLocation.CurrentUser &&
                    !ProcessIntegrity.IsElevated &&
                    exception.NativeErrorCode == 740)
                {
                    NavigationDiagnostics.Write("Command: Current User MMC requires elevation; starting helper");
                    ElevatedCertificateLauncher.Start(entry, selectCertificate);
                }
            }
            return CommandResult.Hide();
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or SecurityException)
        {
            NavigationDiagnostics.Write($"Command: certificate manager launch failed ({exception.GetType().Name}, 0x{exception.HResult:X8})");
            return CommandResult.ShowToast(Strings.Get("Error.CertificateManager"));
        }
    }
}
