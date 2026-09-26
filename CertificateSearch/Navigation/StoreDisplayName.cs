// Copyright (c) 2026 Koichi Kobayashi
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace CertificateSearch.Navigation;

internal static class StoreDisplayName
{
    public static string? GetLocalizedName(string storeName)
    {
        var value = CryptFindLocalizedName(storeName);
        return value == IntPtr.Zero ? null : Marshal.PtrToStringUni(value);
    }

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CryptFindLocalizedName(string pwszCryptName);
}
