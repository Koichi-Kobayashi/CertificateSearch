# Privacy Policy

Last updated: September 26, 2026

Certificate Search for PowerToys Command Palette (the “extension”) is a PowerToys Command Palette extension that searches Windows certificate stores and opens a selected certificate in Windows Certificate Manager.

## Information the extension accesses

The extension reads information from certificate stores through the Windows certificate APIs and uses it to show and search certificate results. This can include the subject, issuer, friendly name, thumbprint, serial number, validity period, private key presence, store name, and store location. This information is used within the extension to provide search results and certificate details.

## Collection, transmission, and sharing

The extension does not send certificate information, search terms, or usage data to the developer or third-party servers. It does not include advertising, analytics, or telemetry. It does not share certificates with external services.

## Local diagnostic logs

The extension writes diagnostic logs for the process of opening and navigating Windows Certificate Manager to:

```text
%LOCALAPPDATA%\CertificateSearch\navigation-YYYY-MM-DD.log
```

Logs record navigation stages, timestamps, process IDs, and details such as exception types and error codes. They do not record certificate-identifying values such as subjects, thumbprints, or serial numbers. Logs are written in both Debug and Release builds. The latest seven calendar days are retained, and older logs are removed the next time a log entry is written. Logs remain on the device and are not transmitted automatically. Users may delete them manually.

## Certificate store access

Certificate stores are accessed for reading. The extension does not modify certificates, trust settings, or private keys. Windows may request UAC approval when opening Certificate Manager for the Local Computer store.

## Changes to this policy

This policy may be updated as the extension or applicable requirements change. The latest version is available in this repository.
