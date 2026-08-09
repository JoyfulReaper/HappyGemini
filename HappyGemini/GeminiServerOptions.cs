using JoyfulReaperLib.TcpServer;

namespace HappyGemini;

public sealed class GeminiServerOptions : ITcpServerOptions
{
    public const string SectionName = "Gemini";

    public string ListenAddress { get; set; } = "::";
    public bool DualMode { get; set; } = true;
    public int Port { get; set; } = 1965;
    public int MaxConcurrentConnections { get; set; } = 16;
    public ConnectionLimitBehavior ConnectionLimitBehavior { get; set; } =
        ConnectionLimitBehavior.Wait;

    public string CertificatePath { get; set; } = "happygemini.pfx";
    public string? CertificatePassword { get; set; }

    public TimeSpan HandshakeTimeout { get; set; } =
        TimeSpan.FromSeconds(5);

    public TimeSpan RequestTimeout { get; set; } =
        TimeSpan.FromSeconds(10);
}