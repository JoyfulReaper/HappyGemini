using HappyGemini;
using HappyGemini.Pages;
using HappyGemini.Plugins;
using HappyGemini.Server;
using JoyfulReaperLib.TcpServer;

var builder = Host.CreateApplicationBuilder(args);

// Windows Service Support
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Happy Gemini Service";
});

// Gemini Configuration
builder.Services
    .AddOptions<GeminiServerOptions>()
    .Bind(
        builder.Configuration.GetSection(
            GeminiServerOptions.SectionName))
    .Validate(
        options => options.Port is > 0 and <= 65535,
        "Gemini:Port must be between 1 and 65535.")
    .Validate(
        options => options.MaxConcurrentConnections > 0,
        "Gemini:MaxConcurrentConnections must be positive.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.ListenAddress),
        "Gemini:ListenAddress must not be empty.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.CertificatePath),
        "Gemini:CertificatePath must not be empty.")
    .Validate(
        options => options.HandshakeTimeout > TimeSpan.Zero,
        "Gemini:HandshakeTimeout must be positive.")
    .Validate(
        options => options.RequestTimeout > TimeSpan.Zero,
        "Gemini:RequestTimeout must be positive.")
    .Validate(
        options =>
            options.Hostnames is { Length: > 0 } &&
            options.Hostnames.All(
                static hostname =>
                    !string.IsNullOrWhiteSpace(hostname)),
        "Gemini:Hostnames must contain at least one hostname.")
    .Validate(
        options =>
            options.Hostnames
                .Select(NormalizeHostname)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() ==
            options.Hostnames.Length,
        "Gemini:Hostnames must not contain duplicate hostnames.")
    .Validate(
        options =>
        {
            HashSet<string> servedHosts =
                options.Hostnames
                    .Select(NormalizeHostname)
                    .ToHashSet(
                        StringComparer.OrdinalIgnoreCase);

            return options.Certificates.All(
                certificate =>
                    !string.IsNullOrWhiteSpace(
                        certificate.Key) &&
                    certificate.Value is not null &&
                    !string.IsNullOrWhiteSpace(
                        certificate.Value.Path) &&
                    servedHosts.Contains(
                        NormalizeHostname(
                            certificate.Key)));
        },
        "Gemini:Certificates entries must have a hostname served by Gemini:Hostnames and a non-empty certificate path.")
    .ValidateOnStart();

// Static Content Configuration
builder.Services
    .AddOptions<GeminiContentOptions>()
    .Bind(
        builder.Configuration.GetSection(
            GeminiContentOptions.SectionName))
    .Validate(
        options => !string.IsNullOrWhiteSpace(
            options.ContentDirectory),
        "GeminiContent:ContentDirectory must not be empty.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(
            options.IndexFile),
        "GeminiContent:IndexFile must not be empty.")
    .Validate(
    options =>
        options.Hosts.All(
            host =>
                !string.IsNullOrWhiteSpace(
                    host.Key) &&
                host.Value is not null &&
                !string.IsNullOrWhiteSpace(
                    host.Value.ContentDirectory)),
    "GeminiContent:Hosts entries must have a hostname and content directory.")
.Validate(
    options =>
        options.Hosts.Keys
            .Select(NormalizeHostname)
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .Count() ==
        options.Hosts.Count,
    "GeminiContent:Hosts must not contain duplicate hostnames.")
    .ValidateOnStart();

builder.Services.AddSingleton<GeminiCertificateProvider>();
builder.Services.AddSingleton<GeminiContentStore>();
builder.Services.AddSingleton<GeminiHostValidator>();

builder.Services.AddScoped<GeminiPageResolver>();

builder.Services.AddGeminiPagesFromAssemblyContaining<HomePage>();

builder.Services.AddGeminiPlugins(builder.Configuration);

builder.Services.AddHostedService<GeminiPageStartupValidator>();
builder.Services.AddSingleton<GeminiVirtualHostResolver>();
builder.Services.AddTcpServer<
    GeminiConnectionHandler,
    GeminiServerOptions>();

var host = builder.Build();
host.Run();

static string NormalizeHostname(
    string hostname)
{
    return hostname
        .Trim()
        .TrimEnd('.');
}