# Copyright (c) 2026 Koichi Kobayashi
# Licensed under the MIT License.

[CmdletBinding()]
param(
    [string]$PropsPath = (Join-Path $PSScriptRoot '..\Directory.Build.props'),
    [string]$ManifestPath = (Join-Path $PSScriptRoot '..\CertificateSearch\Package.appxmanifest')
)

$ErrorActionPreference = 'Stop'
[xml]$props = Get-Content -LiteralPath $PropsPath -Raw
$versionNode = $props.SelectSingleNode('/Project/PropertyGroup/AppxPackageVersion')
if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.InnerText)) {
    throw "AppxPackageVersion was not found in $PropsPath."
}

$version = $versionNode.InnerText.Trim()
if ($version -notmatch '^\d{1,5}\.\d{1,5}\.\d{1,5}\.0$') {
    throw "AppxPackageVersion must be Store-compatible major.minor.build.0: $version"
}
foreach ($part in $version.Split('.')) {
    if ([int]$part -gt 65535) {
        throw "AppxPackageVersion component exceeds 65535: $version"
    }
}

[xml]$manifest = Get-Content -LiteralPath $ManifestPath -Raw
$namespace = [System.Xml.XmlNamespaceManager]::new($manifest.NameTable)
$namespace.AddNamespace('appx', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
$identity = $manifest.SelectSingleNode('/appx:Package/appx:Identity', $namespace)
if ($null -eq $identity) {
    throw "Identity was not found in $ManifestPath."
}

if ($identity.GetAttribute('Version') -eq $version) {
    Write-Host "Package version is already $version."
    exit 0
}

$identity.SetAttribute('Version', $version)
$settings = [System.Xml.XmlWriterSettings]::new()
$settings.Encoding = [System.Text.UTF8Encoding]::new($false)
$settings.Indent = $true
$settings.NewLineChars = "`r`n"
$settings.NewLineHandling = [System.Xml.NewLineHandling]::Replace
$writer = [System.Xml.XmlWriter]::Create($ManifestPath, $settings)
try {
    $manifest.Save($writer)
}
finally {
    $writer.Dispose()
}

Write-Host "Updated Package.appxmanifest to version $version."
