using System;

namespace TraducaoRealtime.Services;

internal enum TranslationProvider
{
    AzureSpeech,
    OpenAi
}

internal sealed class TranslationEnvironment
{
    private TranslationEnvironment(
        TranslationProvider provider,
        string apiKey,
        string? region,
        string? endpoint)
    {
        Provider = provider;
        ApiKey = apiKey;
        Region = region;
        Endpoint = endpoint;
    }

    public TranslationProvider Provider { get; }

    public string ApiKey { get; }

    public string? Region { get; }

    public string? Endpoint { get; }

    public static bool TryCreateFromEnvironment(out TranslationEnvironment? environment, out string errorMessage)
    {
        var providerRaw = Environment.GetEnvironmentVariable("TRANSLATION_PROVIDER") ?? "azure";
        var provider = ParseProvider(providerRaw);

        if (provider == TranslationProvider.AzureSpeech)
        {
            var apiKey = FirstNonEmpty(
                Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY"),
                Environment.GetEnvironmentVariable("SPEECH_KEY"),
                Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_KEY"));

            var region = FirstNonEmpty(
                Environment.GetEnvironmentVariable("AZURE_SPEECH_REGION"),
                Environment.GetEnvironmentVariable("SPEECH_REGION"));

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(region))
            {
                environment = null;
                errorMessage = "Para TRANSLATION_PROVIDER=azure, configure AZURE_SPEECH_KEY (ou SPEECH_KEY) e AZURE_SPEECH_REGION (ou SPEECH_REGION).";
                return false;
            }

            environment = new TranslationEnvironment(TranslationProvider.AzureSpeech, apiKey, region, endpoint: null);
            errorMessage = string.Empty;
            return true;
        }

        var openAiApiKey = FirstNonEmpty(
            Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
            Environment.GetEnvironmentVariable("API_KEY"));

        if (string.IsNullOrWhiteSpace(openAiApiKey))
        {
            environment = null;
            errorMessage = "Para TRANSLATION_PROVIDER=openai, configure OPENAI_API_KEY.";
            return false;
        }

        var endpoint = FirstNonEmpty(
            Environment.GetEnvironmentVariable("OPENAI_ENDPOINT"),
            "https://api.openai.com/v1");

        environment = new TranslationEnvironment(TranslationProvider.OpenAi, openAiApiKey, region: null, endpoint);
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

    private static TranslationProvider ParseProvider(string provider)
    {
        if (provider.Equals("openai", StringComparison.OrdinalIgnoreCase)
            || provider.Equals("open-ai", StringComparison.OrdinalIgnoreCase))
        {
            return TranslationProvider.OpenAi;
        }

        return TranslationProvider.AzureSpeech;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
