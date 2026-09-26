// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CertificateSearch.Certificates;
using CertificateSearch.Navigation;
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
        Title = "Certificate Search";
        Name = "Search certificates";
        PlaceholderText = "Name, issuer, thumbprint, serial number, or store";
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
            return [new ListItem(new NoOpCommand()) { Title = _loading ? "Loading certificates…" : "No certificates loaded" }];
        }

        var matches = _index.Search(_query);
        var results = matches.Take(200).Select(entry => CreateItem(entry, _index.CanMatchByVisibleMetadata(entry))).ToList();
        if (results.Count == 0)
        {
            results.Add(new ListItem(new NoOpCommand()) { Title = "No matching certificates" });
        }

        if (matches.Count > 200)
        {
            results.Add(new ListItem(new NoOpCommand()) { Title = "More matches available", Subtitle = "Type a more specific name or identifier" });
        }

        results.Add(new ListItem(new RefreshCertificatesCommand(this)) { Title = "Refresh certificates", Subtitle = "Reload Windows certificate stores" });
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
        return new ListItem(new OpenCertificateCommand(entry, true, allowMetadataMatch))
        {
            Title = entry.DisplayName,
            Subtitle = $"{entry.Location} · {entry.Subject}",
            Tags = [new Tag(entry.Validity(now))],
            Details = new Details
            {
                Title = entry.DisplayName,
                Body = $"**Store:** {entry.Location}\n\n**Subject:** {entry.Subject}\n\n**Issuer:** {entry.Issuer}\n\n**Friendly name:** {entry.FriendlyName}\n\n**Thumbprint:** {entry.Thumbprint}\n\n**Serial number:** {entry.SerialNumber}\n\n**Valid from:** {entry.NotBefore:g}\n\n**Valid until:** {entry.NotAfter:g}\n\n**Private key present:** {(entry.HasPrivateKey ? "Yes" : "No")}",
            },
            MoreCommands =
            [
                new CommandContextItem(new OpenCertificateCommand(entry, false)),
                new CommandContextItem(new CopyTextCommand(entry.Thumbprint) { Name = "Copy thumbprint" }),
                new CommandContextItem(new CopyTextCommand(entry.Subject) { Name = "Copy subject" }),
                new CommandContextItem(new CopyTextCommand(entry.Issuer) { Name = "Copy issuer" }),
            ],
        };
    }
}

internal sealed partial class RefreshCertificatesCommand(CertificateSearchPage page) : InvokableCommand
{
    public override string Name => "Refresh certificates";

    public override ICommandResult Invoke()
    {
        page.Load();
        return CommandResult.KeepOpen();
    }
}
