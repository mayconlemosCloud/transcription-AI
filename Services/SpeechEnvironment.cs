using System;

namespace TraducaoRealtime.Services;

internal sealed class SpeechEnvironment
{
    private SpeechEnvironment(string speechKey, string region)
    {
        SpeechKey = speechKey;
        Region = region;
    }

    public string SpeechKey { get; }

    public string Region { get; }

    public static bool TryCreateFromEnvironment(out SpeechEnvironment? speechEnvironment, out string errorMessage)
    {
        var speechKey = Environment.GetEnvironmentVariable("SPEECH_KEY");
        if (string.IsNullOrWhiteSpace(speechKey))
        {
            speechKey = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_KEY");
        }

        var region = Environment.GetEnvironmentVariable("SPEECH_REGION");

        if (string.IsNullOrWhiteSpace(speechKey) || string.IsNullOrWhiteSpace(region))
        {
            speechEnvironment = null;
            errorMessage = "As variáveis SPEECH_KEY (ou AZURE_SUBSCRIPTION_KEY) e SPEECH_REGION devem estar configuradas no .env.";
            return false;
        }

        speechEnvironment = new SpeechEnvironment(speechKey, region);
        errorMessage = string.Empty;
        return true;
    }

    public string GetRecognitionLanguage(TranslationMode mode)
    {
        return mode == TranslationMode.EnglishToPortuguese ? "en-US" : "pt-BR";
    }

    public string GetTargetLanguage(TranslationMode mode)
    {
        return mode == TranslationMode.EnglishToPortuguese ? "pt-BR" : "en";
    }
}