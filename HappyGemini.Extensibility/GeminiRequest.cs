using System.Net;

namespace HappyGemini.Extensibility;

/// <summary>
/// Represents one Gemini request and its connection metadata.
/// </summary>
public sealed record GeminiRequest(Uri Url)
{
    /// <summary>
    /// Gets the remote client endpoint, when available.
    /// </summary>
    public IPEndPoint? RemoteEndPoint { get; init; }

    /// <summary>
    /// Gets the local server endpoint that accepted the connection,
    /// including the destination address used by the client.
    /// </summary>
    public IPEndPoint? LocalEndPoint { get; init; }
}
