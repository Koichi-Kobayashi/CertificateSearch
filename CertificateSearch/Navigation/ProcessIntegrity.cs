// Copyright (c) 2026 Koichi Kobayashi
// Licensed under the MIT License.

using System.Security.Principal;

namespace CertificateSearch.Navigation;

internal static class ProcessIntegrity
{
    public static bool IsElevated
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
