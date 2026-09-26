// Copyright (c) 2026 Koichi Kobayashi
// Licensed under the MIT License.

using System.Security.Cryptography.X509Certificates;
using CertificateSearch.Certificates;
using CertificateSearch.Navigation;

var now = new DateTime(2026, 9, 26);
CertificateEntry Entry(string subject, string commonName, string friendly, string issuer,
    string thumbprint, string serial, StoreLocation location, string store,
    DateTime? from = null, DateTime? until = null, bool privateKey = false) =>
    new CertificateEntry(subject, commonName, friendly, issuer, thumbprint, serial,
        from ?? now.AddYears(-1), until ?? now.AddYears(1), privateKey, location, store)
    { IssuerName = issuer.Replace("CN=", "", StringComparison.OrdinalIgnoreCase) };

var named = Entry("CN=Localhost", "Localhost", "Development server", "CN=IssuerCorp",
    "AB CD EF 12", "1234ABC", StoreLocation.CurrentUser, "My");
var issuerOnly = Entry("CN=Other", "Other", "", "CN=Development server",
    "99887766", "FFF999", StoreLocation.LocalMachine, "Root");
var duplicate = Entry("CN=Localhost", "Localhost", "Development server", "CN=Elsewhere",
    "AABBCCDD", "445566", StoreLocation.LocalMachine, "TrustedPeople");
var index = new CertificateSearchIndex([named, issuerOnly, duplicate]);

void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException(name);
    Console.WriteLine($"PASS {name}");
}

Check(index.Search("development server")[0] == named, "friendly name outranks issuer");
Check(index.Search("localhost").Count == 2, "same subject in multiple stores");
Check(index.Search("issuerCorp").SequenceEqual([named]), "issuer search case insensitive");
Check(index.Search("AB CD EF 12").SequenceEqual([named]), "spaced thumbprint");
Check(index.Search("abcdef12").SequenceEqual([named]), "compact thumbprint");
Check(index.Search("cd ef").SequenceEqual([named]), "thumbprint fragment");
Check(index.Search("1234a").SequenceEqual([named]), "serial prefix");
Check(index.Search("TrustedPeople").SequenceEqual([duplicate]), "store name");
Check(index.Search("LocalMachine").Count == 2, "store location");
Check(index.Search("develop")[0] == named, "partial friendly name ranking");
Check(index.Search("no-match").Count == 0, "no matches");
Check(new CertificateSearchIndex([]).Search("anything").Count == 0, "empty index");
Check(named.Validity(now) == "Valid", "valid certificate");
Check(named with { NotAfter = now.AddDays(-1) } is { } expired && expired.Validity(now) == "Expired", "expired certificate");
Check(named with { NotBefore = now.AddDays(1) } is { } future && future.Validity(now) == "Not yet valid", "future certificate");
Check(named with { NotAfter = now.AddDays(10) } is { } soon && soon.Validity(now) == "Expires soon", "expires soon");
Check(named with { FriendlyName = "" } is { } unnamed && unnamed.DisplayName == "Localhost", "friendly name fallback");
Check((named with { HasPrivateKey = true }).HasPrivateKey, "private key presence metadata");
var read = CertificateStoreReader.ReadAll(
    _ => ["Empty", "Broken", "My"],
    (location, store) => store switch
    {
        "Empty" => [],
        "Broken" => throw new System.Security.Cryptography.CryptographicException("Simulated access failure"),
        _ => [location == StoreLocation.CurrentUser ? named : duplicate],
    });
Check(read.Count == 2 && read.Any(e => e.StoreLocation == StoreLocation.CurrentUser) &&
    read.Any(e => e.StoreLocation == StoreLocation.LocalMachine), "both locations and per-store failure isolation");
Check(CertificateRowMatcher.ContainsThumbprint("Issued to | AB CD EF 12", named.Thumbprint), "unique row thumbprint match");
Check(!CertificateRowMatcher.ContainsThumbprint("Issued to | AB CD EF 13", named.Thumbprint), "different row thumbprint rejected");
Check(CertificateRowMatcher.MatchesVisibleMetadata(
    ["Localhost", "IssuerCorp", named.NotAfter.ToShortDateString()], named), "subject issuer and expiry match");
Check(!CertificateRowMatcher.MatchesVisibleMetadata(
    ["Localhost", "Other issuer", named.NotAfter.ToShortDateString()], named), "wrong issuer rejected");
Check(!CertificateRowMatcher.MatchesVisibleMetadata(
    ["Localhost", "IssuerCorp", named.NotAfter.AddDays(1).ToShortDateString()], named), "wrong expiry rejected");
Check(CertificateRowMatcher.MatchesVisibleMetadata(
    [$"Localhost\tIssuerCorp\t{named.NotAfter:yyyy/MM/dd}\tCode signing"], named),
    "combined MMC row matches certificate");
Check(!CertificateRowMatcher.MatchesVisibleMetadata(
    [$"Localhost\tOther issuer\t{named.NotAfter:yyyy/MM/dd}"], named),
    "combined MMC row rejects wrong issuer");
Check(index.CanMatchByVisibleMetadata(named), "distinct issuer permits metadata match");
Check(!new CertificateSearchIndex([named, named with { Thumbprint = "11223344" }])
    .CanMatchByVisibleMetadata(named), "same visible identity is ambiguous");
string[][] rows = [["Other", "IssuerCorp", named.NotAfter.ToShortDateString()],
    ["Localhost", "IssuerCorp", named.NotAfter.ToShortDateString()]];
Check(CertificateRowMatcher.FindMatchingRows(rows, named, true).SequenceEqual([1]), "unique visible row selected");
Check(CertificateRowMatcher.FindMatchingRows(rows, named, false).Length == 0, "metadata match disabled for duplicate identity");
Check(CertificateRowMatcher.FindMatchingRows([rows[1], rows[1]], named, true).Length == 2,
    "ambiguous visible rows rejected");
var localizedMy = StoreDisplayName.GetLocalizedName("My");
Check(!string.IsNullOrWhiteSpace(localizedMy), "Windows provides localized store name");
Console.WriteLine($"Localized My store: {localizedMy}");
Check(CertificateHelperProtocol.IsValidPipeName($"CertificateSearch.{Guid.NewGuid():N}"), "valid helper pipe name");
Check(!CertificateHelperProtocol.IsValidPipeName("CertificateSearch.bad"), "invalid helper pipe rejected");
Check(CertificateHelperProtocol.IsValidRequest("Dell Trust", new string('A', 40), "1"), "valid helper request");
Check(!CertificateHelperProtocol.IsValidRequest("Dell Trust\nRoot", new string('A', 40), "1"), "injected store name rejected");
Check(!CertificateHelperProtocol.IsValidRequest("Dell Trust", "not-a-thumbprint", "1"), "invalid thumbprint rejected");

if (args.Contains("--smoke", StringComparer.OrdinalIgnoreCase))
{
    var live = CertificateStoreReader.ReadAll();
    Console.WriteLine($"READ-ONLY SMOKE CurrentUser={live.Count(e => e.StoreLocation == StoreLocation.CurrentUser)} LocalMachine={live.Count(e => e.StoreLocation == StoreLocation.LocalMachine)}");
}
