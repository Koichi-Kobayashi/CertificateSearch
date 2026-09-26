// Copyright (c) 2026 Koichi Kobayashi
// Licensed under the MIT License.

using System.Globalization;
using System.Resources;

namespace CertificateSearch.Resources;

internal static class Strings
{
    private static readonly ResourceManager ResourceManager = new(
        "CertificateSearch.Resources.Strings",
        typeof(Strings).Assembly);

    public static string Get(string resourceKey) =>
        ResourceManager.GetString(resourceKey, CultureInfo.CurrentUICulture) ?? resourceKey;
}
