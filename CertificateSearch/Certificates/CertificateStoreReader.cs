// Copyright (c) 2026 Koichi Kobayashi
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Win32;

namespace CertificateSearch.Certificates;

internal sealed class CertificateStoreReader
{
    private static readonly string[] CommonStores =
        ["My", "Root", "CA", "AuthRoot", "TrustedPeople", "TrustedPublisher", "Disallowed", "AddressBook"];

    public static IReadOnlyList<CertificateEntry> ReadAll() => ReadAll(GetStoreNames, ReadStore);

    internal static IReadOnlyList<CertificateEntry> ReadAll(
        Func<StoreLocation, IEnumerable<string>> storeNames,
        Func<StoreLocation, string, IReadOnlyList<CertificateEntry>> readStore)
    {
        var entries = new List<CertificateEntry>();
        foreach (var location in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
        {
            foreach (var name in storeNames(location))
            {
                try
                {
                    entries.AddRange(readStore(location, name));
                }
                catch (Exception exception) when (exception is CryptographicException or SecurityException or UnauthorizedAccessException)
                {
                    Trace.TraceWarning($"Certificate store {location}/{name}: {exception}");
                }
            }
        }

        return entries;
    }

    internal static IReadOnlyList<CertificateEntry> ReadStore(StoreLocation location, string name)
    {
        var entries = new List<CertificateEntry>();
        using var store = new X509Store(name, location);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
        foreach (X509Certificate2 certificate in store.Certificates)
        {
            try
            {
                entries.Add(new CertificateEntry(
                    certificate.Subject,
                    certificate.GetNameInfo(X509NameType.SimpleName, false),
                    certificate.FriendlyName,
                    certificate.Issuer,
                    certificate.Thumbprint,
                    certificate.SerialNumber,
                    certificate.NotBefore,
                    certificate.NotAfter,
                    certificate.HasPrivateKey,
                    location,
                    name)
                {
                    IssuerName = certificate.GetNameInfo(X509NameType.SimpleName, true),
                });
            }
            catch (CryptographicException exception)
            {
                Trace.TraceWarning($"Certificate metadata in {location}/{name}: {exception}");
            }
            finally
            {
                certificate.Dispose();
            }
        }

        return entries;
    }

    private static HashSet<string> GetStoreNames(StoreLocation location)
    {
        var names = new HashSet<string>(CommonStores, StringComparer.OrdinalIgnoreCase);
        try
        {
            using var root = RegistryKey.OpenBaseKey(
                location == StoreLocation.CurrentUser ? RegistryHive.CurrentUser : RegistryHive.LocalMachine,
                RegistryView.Default);
            using var stores = root.OpenSubKey(@"SOFTWARE\Microsoft\SystemCertificates", false);
            if (stores is not null)
            {
                names.UnionWith(stores.GetSubKeyNames());
            }
        }
        catch (Exception exception) when (exception is SecurityException or UnauthorizedAccessException or System.IO.IOException)
        {
            Trace.TraceWarning($"Certificate store discovery for {location}: {exception}");
        }

        return names;
    }
}
