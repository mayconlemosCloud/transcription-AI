using System;

namespace TraducaoRealtime.Services;

internal static class RealTimeTranslationProviderFactory
{
    public static IRealTimeTranslationProvider Create(TranslationEnvironment environment)
    {
        return environment.Provider switch
        {
            TranslationProvider.AzureSpeech => new AzureSpeechTranslationProvider(environment),
            TranslationProvider.OpenAi => new OpenAiTranslationProvider(environment),
            _ => throw new InvalidOperationException("Provedor de tradução não suportado.")
        };
    }
}
