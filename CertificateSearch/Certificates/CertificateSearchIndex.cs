// Copyright (c) 2026 Koichi Kobayashi
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace CertificateSearch.Certificates;

internal sealed class CertificateSearchIndex(IReadOnlyList<CertificateEntry> entries)
{
    public bool CanMatchByVisibleMetadata(CertificateEntry entry) => entries.Count(other =>
        other.StoreLocation == entry.StoreLocation &&
        string.Equals(other.StoreName, entry.StoreName, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(other.Subject, entry.Subject, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(other.Issuer, entry.Issuer, StringComparison.OrdinalIgnoreCase) &&
        other.NotAfter.Date == entry.NotAfter.Date) == 1;

    public IReadOnlyList<CertificateEntry> Search(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return entries.OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.Location, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        // A spaced thumbprint is also searched as one identifier.
        var normalized = IsIdentifierQuery(query) ? NormalizeIdentifier(query) : string.Empty;
        return entries.Select(e => (Entry: e, Score: Score(e, terms, normalized)))
            .Where(result => result.Score > 0)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.Entry.Location, StringComparer.OrdinalIgnoreCase)
            .Select(result => result.Entry).ToArray();
    }

    private static int Score(CertificateEntry entry, string[] terms, string identifier)
    {
        var total = 0;
        foreach (var term in terms)
        {
            var score = Math.Max(
                Math.Max(TextScore(entry.FriendlyName, term, 120, 65), TextScore(entry.CommonName, term, 110, 60)),
                Math.Max(TextScore(entry.Subject, term, 100, 55), TextScore(entry.Issuer, term, 25, 15)));
            score = Math.Max(score, TextScore(entry.StoreName, term, 12, 8));
            score = Math.Max(score, TextScore(entry.StoreLocation.ToString(), term, 12, 8));
            var identifierTerm = IsIdentifierQuery(term) ? NormalizeIdentifier(term) : string.Empty;
            score = Math.Max(score, IdentifierScore(entry.Thumbprint, identifierTerm));
            score = Math.Max(score, IdentifierScore(entry.SerialNumber, identifierTerm));
            if (score == 0)
            {
                return 0;
            }

            total += score;
        }

        return Math.Max(total, IdentifierScore(entry.Thumbprint, identifier));
    }

    private static int TextScore(string value, string term, int exact, int partial) =>
        string.Equals(value, term, StringComparison.OrdinalIgnoreCase) ? exact :
        value.Contains(term, StringComparison.OrdinalIgnoreCase) ? partial : 0;

    private static int IdentifierScore(string value, string term)
    {
        if (term.Length == 0)
        {
            return 0;
        }

        var normalized = NormalizeIdentifier(value);
        return string.Equals(normalized, term, StringComparison.OrdinalIgnoreCase) ? 105 :
            normalized.StartsWith(term, StringComparison.OrdinalIgnoreCase) ? 90 :
            normalized.Contains(term, StringComparison.OrdinalIgnoreCase) ? 45 : 0;
    }

    internal static string NormalizeIdentifier(string value) =>
        new(value.Where(Uri.IsHexDigit).ToArray());

    private static bool IsIdentifierQuery(string value) =>
        value.Any(Uri.IsHexDigit) && value.All(character =>
            Uri.IsHexDigit(character) || char.IsWhiteSpace(character) || character is ':' or '-');
}
