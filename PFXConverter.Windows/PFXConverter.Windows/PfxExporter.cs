using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace PFXConverter.Windows;

public static class PfxExporter
{
    public static List<ConversionEngine> DiscoverEngines()
    {
        var engines = new List<ConversionEngine>
        {
            new(
                Id: "windows-built-in",
                DisplayName: "Windows built-in",
                Description: "Uses .NET and Windows certificate APIs. No OpenSSL installation is required.",
                Kind: ConversionEngineKind.WindowsBuiltIn,
                ExecutablePath: null,
                SupportsLegacyFlag: false)
        };

        foreach (var opensslPath in DiscoverOpenSslPaths())
        {
            var version = RunAndCapture(opensslPath, ["version"]);
            var supportsLegacy = SupportsLegacyFlag(opensslPath);
            engines.Add(new ConversionEngine(
                Id: opensslPath,
                DisplayName: $"OpenSSL - {opensslPath}",
                Description: $"{version.Trim()}. {(supportsLegacy ? "Supports -legacy; the app will use it." : "Does not support -legacy; the app will use SHA1/3DES parameters without that flag.")}",
                Kind: ConversionEngineKind.OpenSsl,
                ExecutablePath: opensslPath,
                SupportsLegacyFlag: supportsLegacy));
        }

        return engines;
    }

    public static void Export(
        string certificatePath,
        string keyPath,
        string outputPath,
        string password,
        ConversionEngine engine)
    {
        ValidateInputs(certificatePath, keyPath, outputPath, password);

        if (engine.Kind == ConversionEngineKind.OpenSsl)
        {
            ExportWithOpenSsl(certificatePath, keyPath, outputPath, password, engine);
            return;
        }

        ExportWithWindowsBuiltIn(certificatePath, keyPath, outputPath, password);
    }

    private static void ExportWithWindowsBuiltIn(
        string certificatePath,
        string keyPath,
        string outputPath,
        string password)
    {
        using var leafWithKey = X509Certificate2.CreateFromPemFile(certificatePath, keyPath);
        var certificatePem = File.ReadAllText(certificatePath);
        var chainCertificates = new X509Certificate2Collection();
        chainCertificates.ImportFromPem(certificatePem);

        var exportCollection = new X509Certificate2Collection();
        exportCollection.Add(leafWithKey);

        var seenThumbprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            leafWithKey.Thumbprint
        };

        foreach (var certificate in chainCertificates)
        {
            if (seenThumbprints.Add(certificate.Thumbprint))
            {
                exportCollection.Add(certificate);
            }
        }

        var pfxBytes = exportCollection.Export(X509ContentType.Pkcs12, password)
            ?? throw new InvalidOperationException("Windows could not export the certificate collection as PKCS#12.");
        File.WriteAllBytes(outputPath, pfxBytes);
    }

    private static void ExportWithOpenSsl(
        string certificatePath,
        string keyPath,
        string outputPath,
        string password,
        ConversionEngine engine)
    {
        if (string.IsNullOrWhiteSpace(engine.ExecutablePath))
        {
            throw new InvalidOperationException("The selected OpenSSL engine does not have an executable path.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = engine.ExecutablePath,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("pkcs12");
        startInfo.ArgumentList.Add("-export");
        if (engine.SupportsLegacyFlag)
        {
            startInfo.ArgumentList.Add("-legacy");
        }
        startInfo.ArgumentList.Add("-keypbe");
        startInfo.ArgumentList.Add("PBE-SHA1-3DES");
        startInfo.ArgumentList.Add("-certpbe");
        startInfo.ArgumentList.Add("PBE-SHA1-3DES");
        startInfo.ArgumentList.Add("-macalg");
        startInfo.ArgumentList.Add("SHA1");
        startInfo.ArgumentList.Add("-out");
        startInfo.ArgumentList.Add(outputPath);
        startInfo.ArgumentList.Add("-inkey");
        startInfo.ArgumentList.Add(keyPath);
        startInfo.ArgumentList.Add("-in");
        startInfo.ArgumentList.Add(certificatePath);
        startInfo.ArgumentList.Add("-passout");
        startInfo.ArgumentList.Add("env:PFX_PASSWORD");
        startInfo.Environment["PFX_PASSWORD"] = password;

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start OpenSSL.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var message = string.Join(Environment.NewLine, new[] { error, output }.Where(text => !string.IsNullOrWhiteSpace(text)));
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(message)
                ? $"OpenSSL returned exit code {process.ExitCode}."
                : $"OpenSSL error:{Environment.NewLine}{message}");
        }
    }

    private static void ValidateInputs(string certificatePath, string keyPath, string outputPath, string password)
    {
        if (!File.Exists(certificatePath))
        {
            throw new FileNotFoundException("Certificate file was not found.", certificatePath);
        }

        if (!File.Exists(keyPath))
        {
            throw new FileNotFoundException("Private key file was not found.", keyPath);
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Choose where to save the .pfx file.", nameof(outputPath));
        }

        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("A .pfx password is required.", nameof(password));
        }
    }

    private static IEnumerable<string> DiscoverOpenSslPaths()
    {
        var candidates = new List<string>
        {
            @"C:\Program Files\OpenSSL-Win64\bin\openssl.exe",
            @"C:\Program Files\OpenSSL-Win32\bin\openssl.exe",
            @"C:\Program Files\Git\usr\bin\openssl.exe",
            @"C:\ProgramData\chocolatey\bin\openssl.exe"
        };

        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? "";
        candidates.AddRange(pathValue
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(directory => Path.Combine(directory, "openssl.exe")));

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(candidate);
            if (seen.Add(fullPath))
            {
                yield return fullPath;
            }
        }
    }

    private static bool SupportsLegacyFlag(string opensslPath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = opensslPath,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("pkcs12");
            startInfo.ArgumentList.Add("-legacy");
            startInfo.ArgumentList.Add("-help");

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string RunAndCapture(string executablePath, IReadOnlyList<string> arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return "Unknown OpenSSL version";
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            var result = string.IsNullOrWhiteSpace(output) ? error : output;
            return string.IsNullOrWhiteSpace(result) ? "Unknown OpenSSL version" : result;
        }
        catch
        {
            return "Unknown OpenSSL version";
        }
    }
}
