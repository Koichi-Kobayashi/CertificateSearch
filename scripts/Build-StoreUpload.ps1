# Copyright (c) 2026 Koichi Kobayashi
# Licensed under the MIT License.

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$propsPath = Join-Path $repositoryRoot 'Directory.Build.props'
$projectPath = Join-Path $repositoryRoot 'CertificateSearch\CertificateSearch.csproj'

& (Join-Path $PSScriptRoot 'Sync-PackageVersion.ps1')

[xml]$props = Get-Content -LiteralPath $propsPath -Raw
$version = $props.SelectSingleNode('/Project/PropertyGroup/AppxPackageVersion').InnerText.Trim()

foreach ($targetPlatform in @('x64', 'ARM64')) {
    Write-Host "Cleaning Release output for $targetPlatform..."
    dotnet clean $projectPath --configuration Release --verbosity quiet -p:Platform=$targetPlatform
    if ($LASTEXITCODE -ne 0) {
        throw "Release clean failed for $targetPlatform."
    }
}

Write-Host "Building x64/ARM64 Store upload bundle version $version..."
dotnet build $projectPath `
    --configuration Release `
    --verbosity minimal `
    -p:Platform=x64 `
    -p:PublishTrimmed=false `
    -p:GenerateAppxPackageOnBuild=true `
    -p:AppxPackageSigningEnabled=false `
    -p:AppxPackageVersion=$version `
    -p:AppxBundle=Always `
    '-p:AppxBundlePlatforms=x64|ARM64' `
    -p:UapAppxPackageBuildMode=StoreUpload `
    -p:AppxPackageDir=AppPackages\StoreUpload\

if ($LASTEXITCODE -ne 0) {
    throw 'Store upload package build failed.'
}

$outputDirectory = Join-Path $repositoryRoot 'CertificateSearch\AppPackages\StoreUpload'
$uploads = @(Get-ChildItem -LiteralPath $outputDirectory -Filter "*_$($version)_x64_ARM64_bundle.msixupload" -Recurse)
if ($uploads.Count -ne 1) {
    throw "Expected one x64_ARM64_bundle.msixupload for version $version in $outputDirectory; found $($uploads.Count)."
}

Write-Host 'Store upload bundle ready:'
$uploads | Select-Object FullName, Length, LastWriteTime
