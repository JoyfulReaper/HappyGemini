namespace HappyGemini;

/// <summary>
/// Represents a Gemini virtual host served by the server.
/// </summary>
public sealed record GeminiVirtualHost(
    string Hostname);