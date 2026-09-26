# ローカライズ

拡張機能は英語と日本語に対応しています。`Strings.Get` は Windows の表示言語に対応するリソースを読み込み、翻訳がない場合は既定の英語リソースを使用します。

## 拡張機能の画面

`CertificateSearch/Resources` の `.resx` ファイルで、Command Palette に表示する拡張機能名、コマンド名と説明、画面のラベル、状態メッセージ、証明書の詳細、コンテキスト メニューを管理します。

- `Strings.resx`: 既定の英語
- `Strings.ja.resx`: 日本語

両方のファイルでリソース キーを揃えてください。キーを追加するときは、英語と日本語の値を両方追加します。値が見つからない場合はリソース キー自体が表示されるため、開発中に翻訳漏れを確認できます。

## Microsoft Store の掲載情報

`CertificateSearch/Strings` の `.resw` ファイルで、Store の掲載情報を管理します。

- `Strings/en-US/Resources.resw`: 英語の掲載情報
- `Strings/ja-JP/Resources.resw`: 日本語の掲載情報

Store の掲載情報と拡張機能内の表示文言は別々に管理します。掲載文を変更した場合は、両方の言語を更新してください。パッケージ ID と提出手順は [Microsoft Store リリース手順](StoreRelease.md)を参照してください。

## 検索語

証明書の検索では、サブジェクト、発行者、フレンドリ名、拇印、シリアル番号、ストア名と場所を照合します。拡張機能を開くコマンド名と説明は、Windows の表示言語に応じて切り替わります。日本語表示でも `cert` でコマンドを検索できるよう、日本語の説明文には英語の `certificates` を含めてください。
