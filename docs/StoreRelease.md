# Microsoft Store 向けリリース手順

この手順で、Certificate Search の x64 と ARM64 を含む Store 提出用の未署名 `.msixupload` を作成します。Partner Center へのアップロードと公開は、このスクリプトでは行いません。

## Store 識別情報

| 項目 | 値 |
| --- | --- |
| Store 製品 ID | `9NBHHRZ7DRVN` |
| パッケージ ID | `FastExplorerStudio.CertificateSearchforCommandPale` |
| Publisher | `CN=9006F72B-0B3B-47AD-BF23-A0D351017457` |
| 発行者の表示名 | `Fast Explorer Studio` |
| 製品の表示名 | `Certificate Search for Command Palette` |

パッケージ ID と Publisher は `CertificateSearch/Package.appxmanifest` と `CertificateSearch/CertificateSearch.csproj` に設定しています。製品の表示名は Partner Center の予約名と一致する必要があります。

## バージョン管理

リリース番号の正は、リポジトリ直下の `Directory.Build.props` にある `AppxPackageVersion` です。Store 向けには `major.minor.build.0` の形式を使います。

```xml
<AppxPackageVersion>1.0.0.0</AppxPackageVersion>
```

番号を変更したら、次のスクリプトでマニフェストを同期します。`Directory.Build.props` と `Package.appxmanifest` は同じ変更として管理してください。

```powershell
./scripts/Sync-PackageVersion.ps1
```

## 提出用バンドルの作成

Windows 上のリポジトリのルートで実行します。

```powershell
dotnet run --project tests/CertificateSearch.Tests/CertificateSearch.Tests.csproj
./scripts/Build-StoreUpload.ps1
```

スクリプトは Release 構成の x64 と ARM64 をビルドし、次のファイルを作成します。

```text
CertificateSearch/AppPackages/StoreUpload/CertificateSearch_1.0.0.0_x64_ARM64_bundle.msixupload
```

`.msixupload` は x64 と ARM64 の `.msix` を含むバンドルと、クラッシュ解析用のシンボルを収めたファイルです。生成物は未署名です。Windows に直接インストールするための署名済みパッケージとは用途が異なります。

Store の表示名と説明文は `CertificateSearch/Strings/en-US/Resources.resw` と `CertificateSearch/Strings/ja-JP/Resources.resw` で管理します。拡張機能の画面内の英語と日本語は `CertificateSearch/Resources` の `.resx` で管理します。

## 提出前の確認

1. バンドル名に期待するバージョンと `x64_ARM64` が含まれていることを確認します。
2. バンドル内に x64 と ARM64 の両方のアプリ パッケージがあることを確認します。
3. パッケージ ID、Publisher、製品表示名が Partner Center の予約情報と一致することを確認します。
4. 英語と日本語で Command Palette の表示を確認し、代表的な証明書を開きます。
5. Partner Center の対象製品へ `.msixupload` をアップロードします。

機密情報や署名証明書はリポジトリと生成スクリプトに含めません。
