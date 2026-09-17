import SwiftUI
import AppKit
import UniformTypeIdentifiers

struct ContentView: View {
    @State private var certificateURL: URL?
    @State private var privateKeyURL: URL?
    @State private var outputURL: URL?
    @State private var password = ""
    @State private var confirmPassword = ""
    @State private var isConverting = false
    @State private var status: ConversionStatus = .idle
    @State private var opensslOptions: [OpenSSLExecutable] = []
    @State private var selectedOpenSSLPath = ""

    private var canConvert: Bool {
        certificateURL != nil &&
        privateKeyURL != nil &&
        outputURL != nil &&
        selectedOpenSSL != nil &&
        !password.isEmpty &&
        password == confirmPassword &&
        !isConverting
    }

    private var selectedOpenSSL: OpenSSLExecutable? {
        opensslOptions.first { $0.path == selectedOpenSSLPath }
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 22) {
            header

            VStack(spacing: 14) {
                FilePickerRow(
                    title: "Certificate + chain (.crt)",
                    url: certificateURL,
                    placeholder: "Choose certificate.crt",
                    buttonTitle: "Choose",
                    action: pickCertificate
                )

                FilePickerRow(
                    title: "Private key (.key)",
                    url: privateKeyURL,
                    placeholder: "Choose private.key",
                    buttonTitle: "Choose",
                    action: pickPrivateKey
                )

                FilePickerRow(
                    title: "Output Windows certificate (.pfx)",
                    url: outputURL,
                    placeholder: "Choose where to save certificate.pfx",
                    buttonTitle: "Save as",
                    action: pickOutput
                )
            }

            opensslPicker
            passwordFields
            actionButtons
            StatusView(status: status)

            Spacer(minLength: 0)
        }
        .padding(28)
        .onAppear(perform: refreshOpenSSLOptions)
    }

    private var header: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("PFX Converter")
                .font(.largeTitle.weight(.semibold))

            Text("Convert a .crt file containing the certificate chain and a .key file into a Windows-ready .pfx file.")
                .foregroundStyle(.secondary)
        }
    }

    private var opensslPicker: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                Text("OpenSSL")
                    .font(.headline)

                Spacer()

                Button(action: refreshOpenSSLOptions) {
                    Image(systemName: "arrow.clockwise")
                    Text("Refresh")
                }
                .disabled(isConverting)
            }

            if opensslOptions.isEmpty {
                Text("No OpenSSL executable was found.")
                    .foregroundStyle(.red)
            } else {
                Picker("OpenSSL executable", selection: $selectedOpenSSLPath) {
                    ForEach(opensslOptions) { option in
                        Text(option.displayName)
                            .tag(option.path)
                    }
                }
                .pickerStyle(.menu)
                .disabled(isConverting)

                if let selectedOpenSSL {
                    Text(selectedOpenSSL.detailText)
                        .foregroundStyle(.secondary)
                        .font(.caption)
                        .textSelection(.enabled)
                }
            }
        }
    }

    private var passwordFields: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text("PFX password")
                .font(.headline)

            HStack(spacing: 12) {
                SecureField("Password", text: $password)
                    .textFieldStyle(.roundedBorder)

                SecureField("Confirm password", text: $confirmPassword)
                    .textFieldStyle(.roundedBorder)
            }

            if !confirmPassword.isEmpty && password != confirmPassword {
                Text("Passwords do not match.")
                    .foregroundStyle(.red)
                    .font(.caption)
            }
        }
    }

    private var actionButtons: some View {
        HStack {
            Button(action: convert) {
                if isConverting {
                    ProgressView()
                        .controlSize(.small)
                } else {
                    Image(systemName: "lock.doc")
                }

                Text(isConverting ? "Generating..." : "Generate .pfx")
            }
            .buttonStyle(.borderedProminent)
            .disabled(!canConvert)

            Button(action: reset) {
                Image(systemName: "arrow.counterclockwise")
                Text("Clear")
            }
            .disabled(isConverting)

            Spacer()
        }
    }

    private func pickCertificate() {
        let panel = NSOpenPanel()
        panel.title = "Choose Certificate"
        panel.allowedContentTypes = contentTypes(for: ["crt", "cer", "pem"])
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = false

        if panel.runModal() == .OK {
            certificateURL = panel.url
            status = .idle
        }
    }

    private func pickPrivateKey() {
        let panel = NSOpenPanel()
        panel.title = "Choose Private Key"
        panel.allowedContentTypes = contentTypes(for: ["key", "pem", "txt"])
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = false

        if panel.runModal() == .OK {
            privateKeyURL = panel.url
            status = .idle
        }
    }

    private func pickOutput() {
        let panel = NSSavePanel()
        panel.title = "Save Windows Certificate"
        panel.nameFieldStringValue = "certificate.pfx"
        panel.allowedContentTypes = contentTypes(for: ["pfx"])
        panel.canCreateDirectories = true

        if panel.runModal() == .OK, let selectedURL = panel.url {
            outputURL = selectedURL.pathExtension.lowercased() == "pfx"
                ? selectedURL
                : selectedURL.appendingPathExtension("pfx")
            status = .idle
        }
    }

    private func reset() {
        certificateURL = nil
        privateKeyURL = nil
        outputURL = nil
        password = ""
        confirmPassword = ""
        status = .idle
    }

    private func refreshOpenSSLOptions() {
        opensslOptions = PFXExporter.discoverOpenSSLExecutables()

        if opensslOptions.contains(where: { $0.path == selectedOpenSSLPath }) {
            return
        }

        selectedOpenSSLPath = opensslOptions.first?.path ?? ""
    }

    private func convert() {
        guard let certificateURL, let privateKeyURL, let outputURL else { return }
        guard let selectedOpenSSL else {
            status = .failure("Choose an OpenSSL executable before generating the .pfx file.")
            return
        }
        guard password == confirmPassword, !password.isEmpty else {
            status = .failure("Enter and confirm a password for the .pfx file.")
            return
        }

        isConverting = true
        status = .working

        let pfxPassword = password

        Task.detached {
            let result = PFXExporter.export(
                certificateURL: certificateURL,
                privateKeyURL: privateKeyURL,
                outputURL: outputURL,
                openssl: selectedOpenSSL,
                password: pfxPassword
            )

            await MainActor.run {
                isConverting = false

                switch result {
                case .success:
                    status = .success("Done: \(outputURL.path)")
                case .failure(let error):
                    status = .failure(error.localizedDescription)
                }
            }
        }
    }

    private func contentTypes(for extensions: [String]) -> [UTType] {
        extensions.compactMap { UTType(filenameExtension: $0) }
    }
}

struct FilePickerRow: View {
    let title: String
    let url: URL?
    let placeholder: String
    let buttonTitle: String
    let action: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(title)
                .font(.headline)

            HStack(spacing: 10) {
                Text(url?.path ?? placeholder)
                    .lineLimit(1)
                    .truncationMode(.middle)
                    .foregroundStyle(url == nil ? .secondary : .primary)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(.horizontal, 10)
                    .frame(height: 34)
                    .background(.quaternary.opacity(0.45), in: RoundedRectangle(cornerRadius: 7))

                Button(action: action) {
                    Image(systemName: "folder")
                    Text(buttonTitle)
                }
                .frame(width: 128)
            }
        }
    }
}

struct StatusView: View {
    let status: ConversionStatus

    var body: some View {
        Group {
            switch status {
            case .idle:
                Text("Ready to choose files.")
                    .foregroundStyle(.secondary)
            case .working:
                Text("OpenSSL is generating the .pfx file...")
                    .foregroundStyle(.secondary)
            case .success(let message):
                Label(message, systemImage: "checkmark.circle.fill")
                    .foregroundStyle(.green)
            case .failure(let message):
                Label(message, systemImage: "exclamationmark.triangle.fill")
                    .foregroundStyle(.red)
                    .textSelection(.enabled)
            }
        }
        .font(.callout)
    }
}

enum ConversionStatus {
    case idle
    case working
    case success(String)
    case failure(String)
}

enum PFXExporter {
    static func discoverOpenSSLExecutables() -> [OpenSSLExecutable] {
        var seenResolvedPaths = Set<String>()

        return [
            "/usr/bin/openssl",
            "/opt/homebrew/opt/openssl@3/bin/openssl",
            "/opt/homebrew/opt/openssl/bin/openssl",
            "/opt/homebrew/bin/openssl",
            "/usr/local/opt/openssl@3/bin/openssl",
            "/usr/local/opt/openssl/bin/openssl",
            "/usr/local/bin/openssl"
        ]
        .filter { FileManager.default.isExecutableFile(atPath: $0) }
        .filter { path in
            let resolvedPath = URL(fileURLWithPath: path).resolvingSymlinksInPath().path
            return seenResolvedPaths.insert(resolvedPath).inserted
        }
        .map {
            OpenSSLExecutable(
                path: $0,
                version: version(for: $0),
                supportsLegacyFlag: supportsLegacyFlag($0)
            )
        }
    }

    static func export(
        certificateURL: URL,
        privateKeyURL: URL,
        outputURL: URL,
        openssl: OpenSSLExecutable,
        password: String
    ) -> Result<Void, PFXExportError> {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: openssl.path)

        var arguments = [
            "pkcs12",
            "-export",
            "-keypbe", "PBE-SHA1-3DES",
            "-certpbe", "PBE-SHA1-3DES",
            "-macalg", "SHA1",
            "-out", outputURL.path,
            "-inkey", privateKeyURL.path,
            "-in", certificateURL.path,
            "-passout", "env:PFX_PASSWORD"
        ]

        if openssl.supportsLegacyFlag {
            arguments.insert("-legacy", at: 2)
        }

        process.arguments = arguments
        process.environment = ProcessInfo.processInfo.environment.merging(
            ["PFX_PASSWORD": password],
            uniquingKeysWith: { _, newValue in newValue }
        )

        let errorPipe = Pipe()
        let outputPipe = Pipe()
        process.standardError = errorPipe
        process.standardOutput = outputPipe

        do {
            try process.run()
            process.waitUntilExit()
        } catch {
            return .failure(.processFailed(error.localizedDescription))
        }

        let standardOutput = readPipe(outputPipe)
        let standardError = readPipe(errorPipe)
        let commandOutput = [standardError, standardOutput]
            .filter { !$0.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
            .joined(separator: "\n")

        guard process.terminationStatus == 0 else {
            let message = commandOutput.isEmpty ? "OpenSSL returned exit code \(process.terminationStatus)." : commandOutput
            return .failure(.opensslFailed(message))
        }

        return .success(())
    }

    private static func supportsLegacyFlag(_ path: String) -> Bool {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: path)
        process.arguments = ["pkcs12", "-legacy", "-help"]
        process.standardOutput = Pipe()
        process.standardError = Pipe()

        do {
            try process.run()
            process.waitUntilExit()
        } catch {
            return false
        }

        return process.terminationStatus == 0
    }

    private static func version(for path: String) -> String {
        let process = Process()
        let outputPipe = Pipe()
        let errorPipe = Pipe()
        process.executableURL = URL(fileURLWithPath: path)
        process.arguments = ["version"]
        process.standardOutput = outputPipe
        process.standardError = errorPipe

        do {
            try process.run()
            process.waitUntilExit()
        } catch {
            return "Unknown version"
        }

        let output = readPipe(outputPipe).trimmingCharacters(in: .whitespacesAndNewlines)
        let error = readPipe(errorPipe).trimmingCharacters(in: .whitespacesAndNewlines)
        return output.isEmpty ? (error.isEmpty ? "Unknown version" : error) : output
    }

    private static func readPipe(_ pipe: Pipe) -> String {
        let data = pipe.fileHandleForReading.readDataToEndOfFile()
        return String(data: data, encoding: .utf8) ?? ""
    }
}

struct OpenSSLExecutable: Identifiable, Hashable {
    let path: String
    let version: String
    let supportsLegacyFlag: Bool

    var id: String { path }

    var displayName: String {
        "\(kind) - \(path)"
    }

    var detailText: String {
        let legacyText = supportsLegacyFlag
            ? "Supports -legacy; the app will use it."
            : "Does not support -legacy; the app will use SHA1/3DES parameters without that flag."
        return "\(version). \(legacyText)"
    }

    private var kind: String {
        if path == "/usr/bin/openssl" {
            return "System OpenSSL"
        }

        if path.contains("homebrew") || path.contains("/usr/local") {
            return "Homebrew OpenSSL"
        }

        return "OpenSSL"
    }
}

enum PFXExportError: LocalizedError {
    case processFailed(String)
    case opensslFailed(String)

    var errorDescription: String? {
        switch self {
        case .processFailed(let message):
            return "Could not start OpenSSL: \(message)"
        case .opensslFailed(let message):
            return "OpenSSL error:\n\(message)"
        }
    }
}
