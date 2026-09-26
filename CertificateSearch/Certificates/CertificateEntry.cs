// Copyright (c) 2026 Koichi Kobayashi
// Licensed under the MIT License.

using System;
using System.Security.Cryptography.X509Certificates;

namespace CertificateSearch.Certificates;

internal sealed record CertificateEntry(
    string Subject,
    string CommonName,
    string FriendlyName,
    string Issuer,
    string Thumbprint,
    string SerialNumber,
    DateTime NotBefore,
    DateTime NotAfter,
    bool HasPrivateKey,
    StoreLocation StoreLocation,
    string StoreName)
{
    public string IssuerName { get; init; } = string.Empty;

    public string Location => $"{StoreLocation} / {StoreName}";

    public string DisplayName => !string.IsNullOrWhiteSpace(FriendlyName) ? FriendlyName :
        !string.IsNullOrWhiteSpace(CommonName) ? CommonName : Subject;

    public string Validity(DateTime now) => now < NotBefore ? "Not yet valid" :
        now > NotAfter ? "Expired" :
        NotAfter <= now.AddDays(30) ? "Expires soon" : "Valid";
}
