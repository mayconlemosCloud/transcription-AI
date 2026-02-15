using System;
using System.Threading;
using System.Threading.Tasks;
using TraducaoRealtime.Services.Configuration;
using TraducaoRealtime.Services.Contracts;
using TraducaoRealtime.Services.Models;
using TraducaoRealtime.Services.Providers.AzureSpeech;
using TraducaoRealtime.Services.Providers.OpenAi;

namespace TraducaoRealtime.Services.Orchestration;

internal sealed class RealTimeTranslationOrchestrator : IRealTimeTranslator
{
    private readonly IRealTimeTranslator implementation;

    public RealTimeTranslationOrchestrator(TranslationEnvironment environment)
    {
        implementation = environment.Provider switch
        {
            TranslationProvider.AzureSpeech => new AzureSpeechTranslator(environment),
            TranslationProvider.OpenAi => new OpenAiTranslator(environment),
            _ => throw new NotSupportedException("Provider de tradução não suportado.")
        };
    }

    public Task StartAsync(
        TranslationMode mode,
        string? audioInputDeviceId,
        Action<string> onStatus,
        Action<TranslationResult> onTranslation,
        CancellationToken cancellationToken)
    {
        return implementation.StartAsync(mode, audioInputDeviceId, onStatus, onTranslation, cancellationToken);
    }
}