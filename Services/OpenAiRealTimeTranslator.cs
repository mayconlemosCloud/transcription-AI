using System;
using System.Threading;
using System.Threading.Tasks;

namespace TraducaoRealtime.Services;

internal sealed class OpenAiRealTimeTranslator : IRealTimeTranslator
{
    private readonly TranslationEnvironment environment;

    public OpenAiRealTimeTranslator(TranslationEnvironment environment)
    {
        this.environment = environment;
    }

    public Task StartAsync(
        TranslationMode mode,
        string? audioInputDeviceId,
        Action<string> onStatus,
        Action<TranslationResult> onTranslation,
        CancellationToken cancellationToken)
    {
        _ = mode;
        _ = audioInputDeviceId;
        _ = onTranslation;
        _ = cancellationToken;

        var endpoint = string.IsNullOrWhiteSpace(environment.Endpoint)
            ? "https://api.openai.com/v1"
            : environment.Endpoint;

        onStatus($"Provider OpenAI selecionado. Implementação pendente. Endpoint: {endpoint}");
        return Task.CompletedTask;
    }
}
