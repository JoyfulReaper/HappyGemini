# HappyGemini

HappyGemini is a Gemini protocol server written in C# and .NET 10. It runs as a .NET worker service on top of `JoyfulReaperLib.TcpServer` and supports TLS, virtual hosts, static files, in-process dynamic pages, and pages loaded from external plugins.

The project is still small and intentionally implements a focused subset of a Gemini server. The sections below describe the current code rather than planned features.

## Protocol behavior

HappyGemini listens on TCP port `1965` by default and handles one Gemini request per TLS connection:

1. Complete a TLS 1.2 or TLS 1.3 handshake.
2. Read one UTF-8 request URI terminated by CRLF.
3. Resolve the URI hostname to a configured virtual host.
4. Try an exact-path dynamic page, then a static file.
5. Write a response and close the TLS connection cleanly.

Request validation currently requires:

- An absolute `gemini://` URI. Scheme comparison is case-insensitive.
- A non-empty host.
- No URI user information or fragment.
- A CRLF request terminator. LF alone and EOF before CRLF are rejected.
- A URI no longer than 1024 UTF-8 bytes, excluding CRLF.

An invalid request receives `59 Bad request`. A hostname not listed in `Gemini:Hostnames`, or a DNS request host that does not match TLS SNI, receives `53 Proxy request refused`. A path with no dynamic page or static file receives `51 Not found`.

Dynamic pages can use the response writer and the status codes defined by `GeminiStatusCode`, including input, success, redirect, failure, and client-certificate status codes. The writer requires metadata for 1x, 2x, and 3x responses, rejects CR or LF in metadata, permits a response body only after a 2x header, and writes text as UTF-8.

## TLS and SNI

A PKCS#12/PFX server certificate is required. `Gemini:CertificatePath` and `Gemini:CertificatePassword` configure the default certificate. Certificate passwords should be supplied through environment-specific configuration or .NET user secrets rather than committed to `appsettings.json`.

During the TLS handshake, HappyGemini uses the SNI hostname to select an exact, case-insensitive entry from `Gemini:Certificates`. Hostnames are normalized by trimming whitespace and a trailing dot. If no per-host entry matches, the default certificate is used.

For DNS hostnames, the URI hostname must match the SNI hostname after the same normalization. A DNS request without SNI is refused after request parsing. Certificate selection does not currently support wildcard mappings.

To create a local development certificate on Windows:

```powershell
$cert = New-SelfSignedCertificate `
    -DnsName "localhost" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -KeyAlgorithm RSA `
    -KeyLength 2048 `
    -NotAfter (Get-Date).AddYears(1)

$password = ConvertTo-SecureString "happygemini-dev" -AsPlainText -Force

Export-PfxCertificate `
    -Cert $cert `
    -FilePath ".\happygemini.pfx" `
    -Password $password
```

The server project has a user-secrets ID, so the password can be configured with:

```powershell
dotnet user-secrets set --project HappyGemini "Gemini:CertificatePassword" "happygemini-dev"
```

## Configuration

Configuration uses the normal .NET configuration providers. Repository defaults are in `HappyGemini/appsettings.json`, with environment-specific overrides such as `appsettings.Development.json`, user secrets, and environment variables available as usual.

### Server settings

The `Gemini` section binds to `GeminiServerOptions`:

| Setting | Default | Purpose |
| --- | --- | --- |
| `ListenAddress` | `::` | Listener address. |
| `DualMode` | `true` | Enables IPv4/IPv6 dual-stack operation for the default IPv6 listener. |
| `Port` | `1965` | TCP listener port. |
| `MaxConcurrentConnections` | `16` | Maximum connections handled concurrently. |
| `ConnectionLimitBehavior` | `Wait` | Behavior applied by the TCP server when the connection limit is reached. |
| `Hostnames` | `["localhost"]` | Hostnames this server will accept and route. At least one unique hostname is required. |
| `CertificatePath` | `happygemini.pfx` | Default PKCS#12/PFX certificate. |
| `CertificatePassword` | `null` | Password for the default certificate. |
| `Certificates` | `{}` | Optional hostname-to-certificate mappings for SNI. Each key must also appear in `Hostnames`. |
| `HandshakeTimeout` | `00:00:05` | TLS handshake timeout. |
| `RequestTimeout` | `00:00:10` | Time allowed for request processing and response writing. |

The `GeminiContent` section configures static content:

| Setting | Default | Purpose |
| --- | --- | --- |
| `ContentDirectory` | `content` | Default content root for all virtual hosts. |
| `IndexFile` | `index.gmi` | Default file used for `/` and paths ending in `/`. |
| `Hosts` | `{}` | Per-host content and page-policy overrides. |

The `GeminiPages:PluginDirectory` setting defaults to `plugins` and controls external plugin discovery.

Relative static-content and plugin paths are resolved from the application's base directory (normally the directory containing the built server). Absolute paths are useful when content or plugins are deployed separately from the application. Certificate paths are passed directly to the certificate loader, so use an absolute path when the process working directory is not controlled.

### Minimal configuration

All listener and timeout settings have defaults. A minimal local configuration supplies a certificate and at least one served hostname:

```json
{
  "Gemini": {
    "CertificatePath": "C:\\certificates\\localhost.pfx",
    "CertificatePassword": null,
    "Hostnames": [
      "localhost"
    ]
  }
}
```

### Virtual hosting

Every entry in `Gemini:Hostnames` creates a virtual host. A host uses the global content root, index file, and global dynamic pages unless overridden under `GeminiContent:Hosts`.

This example serves two hostnames with separate content roots. `one.example` uses the default certificate and global dynamic pages. `two.example` has its own certificate and disables global pages:

```json
{
  "Gemini": {
    "ListenAddress": "::",
    "DualMode": true,
    "Port": 1965,
    "Hostnames": [
      "one.example",
      "two.example"
    ],
    "CertificatePath": "C:\\certificates\\default.pfx",
    "CertificatePassword": null,
    "Certificates": {
      "two.example": {
        "Path": "C:\\certificates\\two.example.pfx",
        "Password": null
      }
    }
  },
  "GeminiContent": {
    "ContentDirectory": "sites/default",
    "IndexFile": "index.gmi",
    "Hosts": {
      "one.example": {
        "ContentDirectory": "sites/one",
        "IndexFile": "home.gmi",
        "UseGlobalPages": true
      },
      "two.example": {
        "ContentDirectory": "sites/two",
        "UseGlobalPages": false
      }
    }
  }
}
```

Per-host `ContentDirectory` and `IndexFile` values fall back to their global values when omitted or blank. `UseGlobalPages` defaults to `true`.

## Static content

After dynamic page resolution, HappyGemini maps the URI's decoded absolute path beneath the selected virtual host's content root. `/` resolves to the configured index file, and a path ending in `/` resolves to that directory's index file. Missing files return `51 Not found`.

Static path handling rejects traversal outside the content root, including percent-encoded traversal, and rejects backslashes. It does not generate directory listings.

Static files are returned with `20 Success` and a MIME type selected by extension:

| Extensions | MIME type |
| --- | --- |
| `.gmi`, `.gemini` | `text/gemini; charset=utf-8` |
| `.txt` | `text/plain; charset=utf-8` |
| `.md` | `text/markdown; charset=utf-8` |
| `.html`, `.htm` | `text/html; charset=utf-8` |
| `.css` | `text/css; charset=utf-8` |
| `.json` | `application/json; charset=utf-8` |
| `.xml` | `application/xml; charset=utf-8` |
| `.png`, `.jpg`, `.jpeg`, `.gif`, `.webp`, `.svg` | Corresponding image type |
| `.pdf`, `.zip`, `.gz` | Corresponding application type |
| `.mp3`, `.ogg`, `.mp4` | Corresponding audio/video type |
| Any other extension | `application/octet-stream` |

## Dynamic pages

A dynamic page implements `IGeminiPage`. Its `Path` is an exact, case-sensitive route; a missing leading slash is normalized automatically. Dynamic pages take precedence over static files at the same path.

```csharp
using HappyGemini.Extensibility;

[AutoRegisterGeminiPage]
public sealed class StatusPage : IGeminiPage
{
    public string Path => "/status";

    public async Task WriteAsync(
        GeminiRequest request,
        GeminiResponseWriter response,
        CancellationToken cancellationToken)
    {
        await response.WriteHeaderAsync(
            GeminiStatusCode.Success,
            "text/gemini; charset=utf-8",
            cancellationToken);

        await response.WriteTextAsync(
            "# Server status\r\n\r\nRunning.\r\n",
            cancellationToken);
    }
}
```

An `IGeminiPage` is global and is available on every virtual host whose `UseGlobalPages` setting is `true`. To scope a page to specific hosts, implement `IHostScopedGeminiPage` and provide its `Hostnames` collection. Host-scoped pages remain available when global pages are disabled and override a global page registered at the same path.

The application registers pages as scoped services. Automatic registration includes public, concrete, non-generic `IGeminiPage` implementations marked with `[AutoRegisterGeminiPage]` in the built-in page assembly and in each loaded plugin entry assembly. Duplicate global routes, duplicate routes for the same host, and invalid host-scoped declarations cause startup validation to fail.

The built-in `HomePage` is a global page at `/`, so it takes precedence over a static root index on hosts that enable global pages.

## External plugins

Plugins provide dynamic pages without being compiled into the server application. `GeminiPages:PluginDirectory` may be absolute or relative to the application base directory. HappyGemini inspects only immediate child directories, looking for a file named `happygemini.plugin.json`:

```text
plugins/
  Example.Plugin/
    happygemini.plugin.json
    Example.Plugin.dll
    Example.Plugin.deps.json
    ...plugin dependencies...
```

A minimal manifest is:

```json
{
  "id": "Example.Plugin",
  "entryAssembly": "Example.Plugin.dll"
}
```

Plugin IDs must be non-empty and unique, ignoring case. The entry assembly must exist inside its plugin directory. Each plugin is loaded into its own non-collectible assembly load context; managed and native dependencies are resolved from the plugin output, while all plugins share the host's `HappyGemini.Extensibility` assembly. At startup, attributed page types in the entry assembly are registered using the same rules as built-in pages.

The `HappyGemini.TestPlugin` project is a working example. Its Debug build target stages the plugin under the server's output `plugins` directory.

Plugin discovery and loading happen only at startup. Missing plugin directories are treated as having no plugins, while invalid manifests, duplicate IDs, missing entry assemblies, and load failures stop startup.

## Build, test, and run

The repository requires the .NET 10 SDK.

```powershell
dotnet build
dotnet test
dotnet run --project HappyGemini
```

Before running the server, make sure its configured certificate exists and its password is available. The default endpoint is `[::]:1965` with dual-stack IPv4/IPv6 enabled. The worker also registers Windows Service integration under the service name `Happy Gemini Service`.

## Project layout

- `HappyGemini` — worker service, TCP/TLS connection handling, request routing, virtual hosts, static content, built-in pages, and plugin loading.
- `HappyGemini.Extensibility` — contracts used by dynamic pages and plugins: requests, response writing, status codes, page interfaces, and the auto-registration attribute.
- `HappyGemini.TestPlugin` — example external plugin used to exercise the plugin pipeline.
- `HappyGemini.Tests` — xUnit tests for request parsing, response writing, page resolution, and static path resolution.

## Current limitations

- One request is processed per TLS connection; there is no connection reuse.
- Client certificates are not requested or validated. Client-certificate status codes are available to pages, but there is no built-in authentication workflow.
- There is no proxying, CGI, server-side redirect policy, or input workflow beyond what a dynamic page implements.
- Dynamic routes are exact paths only; there are no route parameters, wildcards, middleware pipeline, or directory routes.
- Static serving has no directory listing, cache validation, range requests, or content negotiation.
- SNI certificate mappings and served hostnames are exact matches; wildcard virtual hosts are not supported.
- Plugins cannot be unloaded or reloaded without restarting the server.

## License

Copyright 2026 Kyle Givler. Licensed under the MIT License. See `LICENSE`.
