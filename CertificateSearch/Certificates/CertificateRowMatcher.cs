// Copyright (c) 2026 Koichi Kobayashi
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace CertificateSearch.Certificates;

internal static class CertificateRowMatcher
{
    public static bool ContainsThumbprint(string rowText, string thumbprint)
    {
        var normalizedThumbprint = CertificateSearchIndex.NormalizeIdentifier(thumbprint);
        return normalizedThumbprint.Length > 0 &&
            rowText.Split('|').Any(field => CertificateSearchIndex.NormalizeIdentifier(field)
                .EndsWith(normalizedThumbprint, StringComparison.OrdinalIgnoreCase));
    }

    public static bool MatchesVisibleMetadata(IReadOnlyList<string> fields, CertificateEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.CommonName) || string.IsNullOrWhiteSpace(entry.IssuerName))
        {
            return false;
        }

        var subjectFields = Enumerable.Range(0, fields.Count)
            .Where(index => Equal(fields[index], entry.CommonName)).ToArray();
        var issuerFields = Enumerable.Range(0, fields.Count)
            .Where(index => Equal(fields[index], entry.IssuerName)).ToArray();
        var expirationFields = Enumerable.Range(0, fields.Count)
            .Where(index => DateTime.TryParse(fields[index], CultureInfo.CurrentCulture,
                DateTimeStyles.AllowWhiteSpaces, out var date) && date.Date == entry.NotAfter.Date).ToArray();

        if (subjectFields.Any(subject => issuerFields.Any(issuer => issuer != subject &&
            expirationFields.Any(expiration => expiration != subject && expiration != issuer))))
        {
            return true;
        }

        // MMC sometimes exposes all visible columns as one ListItem name instead
        // of separate child names. Compare the complete row in that case.
        var rowText = string.Join(" | ", fields);
        if (!Contains(rowText, entry.CommonName) || !Contains(rowText, entry.IssuerName) ||
            !ContainsExpirationDate(rowText, entry.NotAfter))
        {
            return false;
        }

        if (Equal(entry.CommonName, entry.IssuerName))
        {
            var first = rowText.IndexOf(entry.CommonName, StringComparison.OrdinalIgnoreCase);
            var second = rowText.IndexOf(entry.CommonName, first + entry.CommonName.Length, StringComparison.OrdinalIgnoreCase);
            return second >= 0;
        }

        return true;
    }

    public static int[] FindMatchingRows(IReadOnlyList<string[]> rows, CertificateEntry entry, bool allowMetadataMatch) =>
        Enumerable.Range(0, rows.Count).Where(index =>
            ContainsThumbprint(string.Join(" | ", rows[index]), entry.Thumbprint) ||
            (allowMetadataMatch && MatchesVisibleMetadata(rows[index], entry))).ToArray();

    private static bool Equal(string actual, string expected) =>
        string.Equals(actual.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool Contains(string text, string value) =>
        text.Contains(value.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool ContainsExpirationDate(string text, DateTime expiration)
    {
        foreach (Match match in Regex.Matches(text, @"(?<!\d)\d{1,4}[/.-]\d{1,2}[/.-]\d{1,4}(?!\d)"))
        {
            if ((DateTime.TryParse(match.Value, CultureInfo.CurrentCulture, DateTimeStyles.None, out var date) ||
                 DateTime.TryParse(match.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) &&
                date.Date == expiration.Date)
            {
                return true;
            }
        }

        return false;
    }
}
