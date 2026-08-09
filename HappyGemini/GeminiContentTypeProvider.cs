namespace HappyGemini;

/// <summary>
/// Determines the MIME type for static Gemini content.
/// </summary>
public static class GeminiContentTypeProvider
{
    public static string GetContentType(
        string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            filePath);

        return Path.GetExtension(filePath)
            .ToLowerInvariant() switch
        {
            ".gmi" or ".gemini" =>
                "text/gemini; charset=utf-8",

            ".txt" =>
                "text/plain; charset=utf-8",

            ".md" =>
                "text/markdown; charset=utf-8",

            ".html" or ".htm" =>
                "text/html; charset=utf-8",

            ".css" =>
                "text/css; charset=utf-8",

            ".json" =>
                "application/json; charset=utf-8",

            ".xml" =>
                "application/xml; charset=utf-8",

            ".png" =>
                "image/png",

            ".jpg" or ".jpeg" =>
                "image/jpeg",

            ".gif" =>
                "image/gif",

            ".webp" =>
                "image/webp",

            ".svg" =>
                "image/svg+xml",

            ".pdf" =>
                "application/pdf",

            ".zip" =>
                "application/zip",

            ".gz" =>
                "application/gzip",

            ".mp3" =>
                "audio/mpeg",

            ".ogg" =>
                "audio/ogg",

            ".mp4" =>
                "video/mp4",

            _ =>
                "application/octet-stream"
        };
    }
}