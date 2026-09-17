# PFX Converter

A small macOS SwiftUI app for Xcode that generates a Windows-ready `.pfx` certificate from:

- a `.crt` file containing the certificate and full issuer chain
- a `.key` file containing the private key
- a required password for the `.pfx` file

The generated command is equivalent to:

```sh
openssl pkcs12 -export -legacy \
  -keypbe PBE-SHA1-3DES \
  -certpbe PBE-SHA1-3DES \
  -macalg SHA1 \
  -out certificate.pfx \
  -inkey private.key \
  -in certificate.crt
```

Apple's system `/usr/bin/openssl` is LibreSSL and does not support `-legacy`, so the app checks the selected executable:

- if the selected OpenSSL supports `-legacy`, the app uses it
- if it does not, the app still uses the SHA1/3DES PKCS#12 parameters without that flag

The password is passed through `-passout env:PFX_PASSWORD`, so it is not exposed as a command-line argument.

## OpenSSL Selection

On launch, the app checks for OpenSSL in:

- `/usr/bin/openssl`
- `/opt/homebrew/opt/openssl@3/bin/openssl`
- `/opt/homebrew/opt/openssl/bin/openssl`
- `/opt/homebrew/bin/openssl`
- `/usr/local/opt/openssl@3/bin/openssl`
- `/usr/local/opt/openssl/bin/openssl`
- `/usr/local/bin/openssl`

If both system and Homebrew OpenSSL are installed, the app shows them in a picker and lets you choose which one to use.

## Requirements

- macOS 12 or newer
- Xcode

## Intel Macs

The Xcode project is configured to build with `ARCHS_STANDARD` and `ONLY_ACTIVE_ARCH = NO`, so Release builds are universal by default:

- `arm64` for Apple Silicon Macs
- `x86_64` for Intel Macs

To create a distributable universal app, use Product > Archive in Xcode, then export the archive.

## Running

1. Open `PFXConverter.xcodeproj` in Xcode.
2. Choose the `PFXConverter` scheme.
3. Press Run.
