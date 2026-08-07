# HappyGemini

HappyGemini is a lightweight Gemini protocol server written in C# and .NET 10.

It is built on top of [`JoyfulReaperLib.TcpServer`](https://github.com/JoyfulReaper/JoyfulReaperLib) and currently implements the basic Gemini request/response flow over TLS.

> Early development. Expect missing features, rough edges, and questionable decisions.

## Current Status

HappyGemini can currently:

* Listen for Gemini connections on port `1965`
* Accept IPv4 and IPv6 connections
* Perform TLS 1.2 / TLS 1.3 handshakes
* Support TLS SNI
* Read and validate Gemini request URLs
* Enforce the Gemini 1024-byte request limit
* Return Gemini status responses
* Serve a simple `text/gemini` response
* Gracefully close TLS connections
* Limit concurrent connections
* Run as a Windows Service

The current implementation is intentionally minimal while the core protocol handling is developed.

## Requirements

* .NET 10
* A TLS certificate in PKCS#12/PFX format

## Configuration

Configuration is stored under the `Gemini` section in `appsettings.json`:

```json
{
  "Gemini": {
    "ListenAddress": "::",
    "DualMode": true,
    "Port": 1965,
    "MaxConcurrentConnections": 16,
    "ConnectionLimitBehavior": "Wait",
    "CertificatePath": "happygemini.pfx",
    "CertificatePassword": null,
    "HandshakeTimeout": "00:00:05",
    "RequestTimeout": "00:00:10"
  }
}
```

Certificate passwords should not be committed to the repository.

For local development, .NET user secrets can be used:

```powershell
dotnet user-secrets set "Gemini:CertificatePassword" "your-password"
```

## Development Certificate

A temporary self-signed certificate can be created on Windows with PowerShell:

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

Then configure the password:

```powershell
dotnet user-secrets set "Gemini:CertificatePassword" "happygemini-dev"
```

## Running

```powershell
dotnet run --project HappyGemini
```

The default listener is:

```text
[::]:1965
```

with IPv4/IPv6 dual-stack enabled.

## Testing

Until a Gemini client is configured, the server can be tested directly from PowerShell:

```powershell
$tcp = [System.Net.Sockets.TcpClient]::new()
$tcp.Connect("localhost", 1965)

$ssl = [System.Net.Security.SslStream]::new(
    $tcp.GetStream(),
    $false,
    { param($sender, $cert, $chain, $errors) return $true }
)

$ssl.AuthenticateAsClient("localhost")
$ssl.ReadTimeout = 5000

$request = [Text.Encoding]::UTF8.GetBytes("gemini://localhost/`r`n")
$ssl.Write($request, 0, $request.Length)
$ssl.Flush()

$buffer = New-Object byte[] 4096
$result = ""

try {
    while (($count = $ssl.Read($buffer, 0, $buffer.Length)) -gt 0) {
        $result += [Text.Encoding]::UTF8.GetString($buffer, 0, $count)
    }
}
catch [System.IO.IOException] {
}

$result

$ssl.Dispose()
$tcp.Dispose()
```

A successful response currently looks like:

```text
20 text/gemini; charset=utf-8

# HappyGemini

It lives.
```

## Planned

Some likely next steps:

* Separate Gemini request parsing and response writing
* Static `.gmi` file serving
* Request routing
* Virtual hosts
* Multiple SNI certificates
* Proper hostname validation
* Gemini status types and response helpers
* Client certificate support
* Automated protocol tests
* Linux service/container deployment

## Why?

Why not?

## License

Copyright 2026 Kyle Givler Licensed under the MIT license
