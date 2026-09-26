// Copyright (c) 2026 Koichi Kobayashi
// Licensed under the MIT License.

using System;
using System.Linq;

namespace CertificateSearch.Navigation;

internal static class CertificateHelperProtocol
{
    public const string PipePrefix = "CertificateSearch.";

    public static bool IsValidPipeName(string pipeName) =>
        pipeName.StartsWith(PipePrefix, StringComparison.Ordinal) &&
        Guid.TryParseExact(pipeName[PipePrefix.Length..], "N", out _);

    public static bool IsValidRequest(string? storeName, string? thumbprint, string? selectFlag) =>
        !string.IsNullOrWhiteSpace(storeName) && storeName.Length <= 256 &&
        !storeName.Any(char.IsControl) &&
        !string.IsNullOrEmpty(thumbprint) && thumbprint.Length is 40 or 64 &&
        thumbprint.All(Uri.IsHexDigit) &&
        selectFlag is "0" or "1";
}
