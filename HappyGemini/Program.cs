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
    .ValidateOnStart();

builder.Services.AddSingleton<GeminiCertificateProvider>();

builder.Services.AddScoped<GeminiPageResolver>();

builder.Services.AddGeminiPagesFromAssemblyContaining<HomePage>();

builder.Services.AddGeminiPlugins(builder.Configuration);

builder.Services.AddHostedService<GeminiPageStartupValidator>();

builder.Services.AddTcpServer<
    GeminiConnectionHandler,
    GeminiServerOptions>();

var host = builder.Build();
host.Run();