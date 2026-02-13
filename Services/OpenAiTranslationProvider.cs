using System;
using System.Threading;
using System.Threading.Tasks;

namespace TraducaoRealtime.Services;

internal sealed class OpenAiTranslationProvider : IRealTimeTranslationProvider
{
    private readonly TranslationEnvironment environment;

    public OpenAiTranslationProvider(TranslationEnvironment environment)
    {
        this.environment = environment;
    }

    public async Task StartAsync(
        TranslationMode mode,
        Action<string> onStatus,
        Action<TranslationResult> onTranslation,
        CancellationToken cancellationToken)
    {
        _ = mode;
        _ = onTranslation;

        onStatus("Provider OpenAI selecionado. Implementação de streaming ainda não concluída.");

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }

        var endpoint = string.IsNullOrWhiteSpace(environment.Endpoint)
            ? "https://api.openai.com/v1"
            : environment.Endpoint;

        onStatus($"Tradução finalizada. Endpoint configurado: {endpoint}");
    }
}
