# PFX Converter for Windows

A small Windows WinForms app that creates a `.pfx` file from:

- a `.crt`, `.cer`, or `.pem` file containing the certificate and issuer chain
- a `.key` or `.pem` private key
- a required password for the exported `.pfx`

## Conversion Engines

The app always offers **Windows built-in** conversion. This uses .NET and Windows certificate APIs, so OpenSSL is not required.

If `openssl.exe` is installed, the app also shows an **OpenSSL** option. OpenSSL is detected from common install paths and from `PATH`.

When OpenSSL is selected, the command is equivalent to:

```powershell
openssl pkcs12 -export -legacy `
  -keypbe PBE-SHA1-3DES `
  -certpbe PBE-SHA1-3DES `
  -macalg SHA1 `
  -out certificate.pfx `
  -inkey private.key `
  -in certificate.crt
```

The app checks whether the selected OpenSSL supports `-legacy`. If it does, the flag is used; otherwise the SHA1/3DES parameters are still used without that flag.

## Build Requirements

- Windows
- Visual Studio 2022 or newer, or the .NET 8 SDK

## Build

From the `PFXConverter.Windows` folder:

```powershell
dotnet build .\PFXConverter.Windows\PFXConverter.Windows.csproj -c Release
```

To publish a regular Windows x64 build:

```powershell
dotnet publish .\PFXConverter.Windows\PFXConverter.Windows.csproj -c Release -r win-x64
```

To make a self-contained build that does not require a separate .NET runtime:

```powershell
dotnet publish .\PFXConverter.Windows\PFXConverter.Windows.csproj -c Release -r win-x64 --self-contained true
```
