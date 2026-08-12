using HappyGemini;
using HappyGemini.Pages;
using HappyGemini.Plugins;
using HappyGemini.Server;
using HappyGemini.Telemetry;
using JoyfulReaperLib.MissionControl;
using JoyfulReaperLib.TcpServer;

var builder = Host.CreateApplicationBuilder(args);

// TODO Clean this file up

// Windows Service Support
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Happy Gemini Service";
});

// Mission Control Integration
builder.Services.AddMissionControlClient(
    builder.Configuration.GetSection(MissionControlClientOptions.SectionName)
);
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddHttpClient();

// Gemini Configuration
builder
    .Services.AddOptions<GeminiServerOptions>()
    .Bind(builder.Configuration.GetSection(GeminiServerOptions.SectionName))
    .Validate(
        options => options.Port is > 0 and <= 65535,
        "Gemini:Port must be between 1 and 65535."
    )
    .Validate(
        options => options.MaxConcurrentConnections > 0,
        "Gemini:MaxConcurrentConnections must be positive."
    )
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.ListenAddress),
        "Gemini:ListenAddress must not be empty."
    )
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.CertificatePath),
        "Gemini:CertificatePath must not be empty."
    )
    .Validate(
        options => options.HandshakeTimeout > TimeSpan.Zero,
        "Gemini:HandshakeTimeout must be positive."
    )
    .Validate(
        options => options.RequestTimeout > TimeSpan.Zero,
        "Gemini:RequestTimeout must be positive."
    )
    .Validate(
        options =>
            options.Hostnames is { Length: > 0 }
            && TryNormalizeHostnames(options.Hostnames, out _),
        "Gemini:Hostnames must contain at least one valid hostname."
    )
    .Validate(
        options =>
            TryNormalizeHostnames(options.Hostnames, out string[] normalizedHostnames)
            && normalizedHostnames
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() == normalizedHostnames.Length,
        "Gemini:Hostnames must not contain duplicate hostnames."
    )
    .Validate(
        options =>
        {
            if (!TryNormalizeHostnames(options.Hostnames, out string[] normalizedHostnames))
            {
                return false;
            }

            HashSet<string> servedHosts = normalizedHostnames.ToHashSet(
                StringComparer.OrdinalIgnoreCase
            );

            return options.Certificates.All(certificate =>
                certificate.Value is not null
                && !string.IsNullOrWhiteSpace(certificate.Value.Path)
                && GeminiHostname.TryNormalize(
                    certificate.Key,
                    out string normalizedCertificateHostname
                )
                && servedHosts.Contains(normalizedCertificateHostname)
            );
        },
        "Gemini:Certificates entries must have a hostname served by Gemini:Hostnames and a non-empty certificate path."
    )
    .ValidateOnStart();

// Static Content Configuration
builder
    .Services.AddOptions<GeminiContentOptions>()
    .Bind(builder.Configuration.GetSection(GeminiContentOptions.SectionName))
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.ContentDirectory),
        "GeminiContent:ContentDirectory must not be empty."
    )
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.IndexFile),
        "GeminiContent:IndexFile must not be empty."
    )
    .Validate(
        options =>
            options.Hosts.All(host =>
                host.Value is not null && GeminiHostname.TryNormalize(host.Key, out _)
            ),
        "GeminiContent:Hosts entries must have a valid hostname."
    )
    .Validate(
        options =>
            TryNormalizeHostnames(options.Hosts.Keys, out string[] normalizedHostnames)
            && normalizedHostnames
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() == normalizedHostnames.Length,
        "GeminiContent:Hosts must not contain duplicate hostnames."
    )
    .ValidateOnStart();

builder.Services.AddSingleton<GeminiCertificateProvider>();
builder.Services.AddSingleton<GeminiContentStore>();
builder.Services.AddSingleton<GeminiHostValidator>();
builder.Services.AddSingleton<TelemetryService>();

builder.Services.AddScoped<GeminiPageResolver>();

builder.Services.AddGeminiPagesFromAssemblyContaining<ServerTimePage>();

builder.Services.AddGeminiPlugins(builder.Configuration);

builder.Services.AddHostedService<GeminiPageStartupValidator>();
builder.Services.AddHostedService<GeminiLifecycleService>();
builder.Services.AddSingleton<GeminiVirtualHostResolver>();
builder.Services.AddTcpServer<GeminiConnectionHandler, GeminiServerOptions>();

var host = builder.Build();
host.Run();

static bool TryNormalizeHostnames(
    IEnumerable<string>? hostnames,
    out string[] normalizedHostnames
)
{
    normalizedHostnames = [];

    if (hostnames is null)
    {
        return false;
    }

    List<string> normalized = [];

    foreach (string hostname in hostnames)
    {
        if (!GeminiHostname.TryNormalize(hostname, out string value))
        {
            return false;
        }

        normalized.Add(value);
    }

    normalizedHostnames = normalized.ToArray();
    return true;
}
