namespace HappyGemini.Pages;

/// <summary>
/// Validates Gemini page registrations during application startup.
/// </summary>
public sealed class GeminiPageStartupValidator(
    IServiceProvider serviceProvider) : IHostedService
{
    public Task StartAsync(
        CancellationToken cancellationToken)
    {
        using IServiceScope scope =
            serviceProvider.CreateScope();

        _ = scope.ServiceProvider
            .GetRequiredService<GeminiPageResolver>();

        return Task.CompletedTask;
    }

    public Task StopAsync(
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}