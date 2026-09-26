// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using CertificateSearch.Certificates;
using CertificateSearch.Navigation;
using CertificateSearch.Resources;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CertificateSearch;

internal sealed partial class CertificateSearchPage : DynamicListPage
{
    private volatile CertificateSearchIndex? _index;
    private string _query = string.Empty;
    private volatile bool _loading;

    public CertificateSearchPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = Strings.Get("Extension.DisplayName");
        Name = Strings.Get("Page.Search.Name");
        PlaceholderText = Strings.Get("Page.Search.Placeholder");
        ShowDetails = true;
        Load();
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _query = newSearch;
        RaiseItemsChanged();
    }

    public override IListItem[] GetItems()
    {
        if (_index is null)
        {
            return [new ListItem(new NoOpCommand()) { Title = Strings.Get(_loading ? "Status.Loading" : "Status.NoCertificates") }];
        }

        var matches = _index.Search(_query);
        var results = matches.Take(200).Select(entry => CreateItem(entry, _index.CanMatchByVisibleMetadata(entry))).ToList();
        if (results.Count == 0)
        {
            results.Add(new ListItem(new NoOpCommand()) { Title = Strings.Get("Status.NoMatches") });
        }

        if (matches.Count > 200)
        {
            results.Add(new ListItem(new NoOpCommand()) { Title = Strings.Get("Status.MoreMatches"), Subtitle = Strings.Get("Status.MoreMatchesHint") });
        }

        results.Add(new ListItem(new RefreshCertificatesCommand(this)) { Title = Strings.Get("Command.Refresh.Title"), Subtitle = Strings.Get("Command.Refresh.Subtitle") });
        return results.ToArray();
    }

    internal void Load()
    {
        if (_loading)
        {
            return;
        }

        _loading = true;
        _ = Task.Run(() =>
        {
            try
            {
                _index = new CertificateSearchIndex(CertificateStoreReader.ReadAll());
            }
            catch (Exception exception)
            {
                Trace.TraceError($"Certificate load failed: {exception}");
                _index = new CertificateSearchIndex([]);
            }
            finally
            {
                _loading = false;
                RaiseItemsChanged();
            }
        });
    }

    private static ListItem CreateItem(CertificateEntry entry, bool allowMetadataMatch)
    {
        var now = DateTime.Now;
        var location = $"{Strings.Get(entry.StoreLocation == StoreLocation.CurrentUser ? "Store.CurrentUser" : "Store.LocalMachine")} / {entry.StoreName}";
        var validity = entry.Validity(now) switch
        {
            "Not yet valid" => Strings.Get("Validity.NotYetValid"),
            "Expired" => Strings.Get("Validity.Expired"),
            "Expires soon" => Strings.Get("Validity.ExpiresSoon"),
            _ => Strings.Get("Validity.Valid"),
        };
        return new ListItem(new OpenCertificateCommand(entry, true, allowMetadataMatch))
        {
            Title = entry.DisplayName,
            Subtitle = $"{location} · {entry.Subject}",
            Tags = [new Tag(validity)],
            Details = new Details
            {
                Title = entry.DisplayName,
                Body = $"**{Strings.Get("Details.Store")}:** {location}\n\n**{Strings.Get("Details.Subject")}:** {entry.Subject}\n\n**{Strings.Get("Details.Issuer")}:** {entry.Issuer}\n\n**{Strings.Get("Details.FriendlyName")}:** {entry.FriendlyName}\n\n**{Strings.Get("Details.Thumbprint")}:** {entry.Thumbprint}\n\n**{Strings.Get("Details.SerialNumber")}:** {entry.SerialNumber}\n\n**{Strings.Get("Details.ValidFrom")}:** {entry.NotBefore:g}\n\n**{Strings.Get("Details.ValidUntil")}:** {entry.NotAfter:g}\n\n**{Strings.Get("Details.PrivateKeyPresent")}:** {Strings.Get(entry.HasPrivateKey ? "Value.Yes" : "Value.No")}",
            },
            MoreCommands =
            [
                new CommandContextItem(new OpenCertificateCommand(entry, false)),
                new CommandContextItem(new CopyTextCommand(entry.Thumbprint) { Name = Strings.Get("Command.CopyThumbprint") }),
                new CommandContextItem(new CopyTextCommand(entry.Subject) { Name = Strings.Get("Command.CopySubject") }),
                new CommandContextItem(new CopyTextCommand(entry.Issuer) { Name = Strings.Get("Command.CopyIssuer") }),
            ],
        };
    }
}

internal sealed partial class RefreshCertificatesCommand(CertificateSearchPage page) : InvokableCommand
{
    public override string Name => Strings.Get("Command.Refresh.Title");

    public override ICommandResult Invoke()
    {
        page.Load();
        return CommandResult.KeepOpen();
    }
}
