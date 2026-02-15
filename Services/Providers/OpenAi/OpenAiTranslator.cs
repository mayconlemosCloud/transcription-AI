using System;
using System.Threading;
using System.Threading.Tasks;
using TraducaoRealtime.Services.Configuration;
using TraducaoRealtime.Services.Contracts;
using TraducaoRealtime.Services.Models;

namespace TraducaoRealtime.Services.Providers.OpenAi;

internal sealed class OpenAiTranslator : IRealTimeTranslator
{
    private readonly TranslationEnvironment environment;

    public OpenAiTranslator(TranslationEnvironment environment)
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
