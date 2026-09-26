// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using CertificateSearch.Resources;

namespace CertificateSearch;

public partial class CertificateSearchCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;

    public CertificateSearchCommandsProvider()
    {
        DisplayName = Strings.Get("Extension.DisplayName");
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        var searchPage = new CertificateSearchPage();
        _commands = [
            new ListItem(searchPage)
            {
                Title = Strings.Get("Command.SearchCertificates.Title"),
                Subtitle = Strings.Get("Command.SearchCertificates.Subtitle"),
            },
        ];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }

}
