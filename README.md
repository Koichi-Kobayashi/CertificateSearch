# Certificate Search for Command Palette

Search certificates across the Windows **Current User** and **Local Computer** certificate stores without remembering their store paths. Requires Microsoft PowerToys Command Palette.

Type part of a subject, friendly name, issuer, thumbprint, serial number, store name, or location. Thumbprints match with or without spaces and regardless of case. Results show the certificate's location and validity; the details pane shows its issuer, thumbprint, serial number, dates, and private-key presence. The index loads once when the page opens. Use **Refresh certificates** after installing or removing a certificate outside this extension.

Press **Enter** to open `certmgr.msc` for Current User or `certlm.msc` for Local Computer. Windows elevates the Local Computer console, so this action requests UAC approval for a short-lived helper that navigates MMC at the same integrity level. Searching and listing certificates do not require elevation. The extension uses Windows' localized store display name to find the matching store and then selects its Certificates list with UI Automation. It opens a certificate row when MMC exposes a unique full-thumbprint match. If the thumbprint is not shown, it can also use a unique subject, issuer, and expiration-date match, provided no other indexed certificate in that store shares those fields. Ambiguous matches leave the store list open. Some custom store labels or MMC accessibility differences may prevent automation; in those cases the appropriate certificate manager still opens.

The context menu provides **Open certificate store**, **Copy thumbprint**, **Copy subject**, and **Copy issuer**. The extension opens stores read-only and never installs, deletes, exports, or changes certificate trust or private keys. A failure to read one store is logged and does not stop other stores from loading.

Navigation writes stage-only diagnostics to `%LOCALAPPDATA%\CertificateSearch\navigation.log`. The log does not include certificate subjects or thumbprints.

## Build and test

```text
dotnet build CertificateSearch.slnx -p:Platform=x64
dotnet run --project tests/CertificateSearch.Tests/CertificateSearch.Tests.csproj
dotnet run --project tests/CertificateSearch.Tests/CertificateSearch.Tests.csproj -- --smoke
```

The optional smoke command enumerates the current machine's stores read-only. Interactive verification requires installing the extension in Command Palette and using known test certificates in the relevant stores. Automated tests never modify Windows certificate stores.
