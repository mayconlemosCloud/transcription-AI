using System;
using System.Threading;
using System.Threading.Tasks;

namespace TraducaoRealtime.Services;

internal sealed class RealTimeTranslator : IRealTimeTranslator
{
    private readonly IRealTimeTranslator implementation;

    public RealTimeTranslator(TranslationEnvironment environment)
    {
        implementation = environment.Provider switch
        {
            TranslationProvider.AzureSpeech => new AzureSpeechRealTimeTranslator(environment),
            TranslationProvider.OpenAi => new OpenAiRealTimeTranslator(environment),
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