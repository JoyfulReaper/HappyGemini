using HappyGemini;
using HappyGemini.Pages;
using HappyGemini.Server;
using JoyfulReaperLib.TcpServer;

var builder = Host.CreateApplicationBuilder(args);

// Windows Service Support
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Happy Gemini Service";
});

builder.Services.Configure<GeminiServerOptions>(
    builder.Configuration.GetSection("Gemini"));

builder.Services.AddSingleton<GeminiCertificateProvider>();

builder.Services.AddScoped<GeminiPageResolver>();

builder.Services.AddGeminiPagesFromAssemblyContaining<HomePage>();

builder.Services.AddHostedService<GeminiPageStartupValidator>();

builder.Services.AddTcpServer<
    GeminiConnectionHandler,
    GeminiServerOptions>();

var host = builder.Build();
host.Run();