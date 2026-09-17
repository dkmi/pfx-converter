# PFX Converter

Desktop applications for macOS and Windows that convert a PEM certificate and private key into a password-protected PKCS#12 (`.pfx`) file for Windows import.

Both applications have an English interface and include the issuer certificates supplied in the input certificate file.

## macOS

The SwiftUI application supports macOS 12 and newer, with Intel and Apple Silicon builds. It detects system and Homebrew OpenSSL installations and lets you choose the executable. Homebrew is optional.

Open `PFXConverter/PFXConverter.xcodeproj` in Xcode and run the `PFXConverter` scheme. Use Product > Archive for distribution.

See [macOS documentation](PFXConverter/README.md) for conversion parameters and build details.

## Windows

The Windows Forms application uses .NET 8. Its built-in conversion engine works without OpenSSL; installed OpenSSL executables are also available as conversion options.

Install the .NET 8 SDK on Windows, then build from this repository's root:

```powershell
dotnet build .\PFXConverter.Windows\PFXConverter.Windows\PFXConverter.Windows.csproj -c Release
```

Publish a self-contained Windows x64 application:

```powershell
dotnet publish .\PFXConverter.Windows\PFXConverter.Windows\PFXConverter.Windows.csproj -c Release -r win-x64 --self-contained true
```

The published files are in `PFXConverter.Windows/PFXConverter.Windows/bin/Release/net8.0-windows/win-x64/publish/`.

See [Windows documentation](PFXConverter.Windows/README.md) for conversion details.

## Input Files

Select a PEM-encoded certificate file containing the leaf certificate followed by its issuer chain, and the matching PEM private key. Set a non-empty password, select an output path, and export the `.pfx` file.

This repository contains source projects and application icons. Compiled applications and installers are not included.
