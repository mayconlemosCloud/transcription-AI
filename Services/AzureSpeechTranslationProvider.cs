using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Translation;

namespace TraducaoRealtime.Services;

internal sealed class AzureSpeechTranslationProvider : IRealTimeTranslationProvider
{
    private readonly TranslationEnvironment environment;
    private SpeechTranslationConfig? cachedConfig;

    public AzureSpeechTranslationProvider(TranslationEnvironment environment)
    {
        this.environment = environment;
    }

    public async Task StartAsync(
        TranslationMode mode,
        Action<string> onStatus,
        Action<TranslationResult> onTranslation,
        CancellationToken cancellationToken)
    {
        var recognitionLanguage = environment.GetRecognitionLanguage(mode);
        var targetLanguage = environment.GetTargetLanguage(mode);

        var config = GetOrCreateConfig();
        config.SpeechRecognitionLanguage = recognitionLanguage;
        config.AddTargetLanguage(targetLanguage);

        onStatus("Aguardando fala...");

        try
        {
            using var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
            using var recognizer = new TranslationRecognizer(config, audioConfig);

            recognizer.Recognizing += (_, eventArgs) =>
            {
                if (!string.IsNullOrWhiteSpace(eventArgs.Result.Text))
                {
                    onStatus($"Ouvindo: {eventArgs.Result.Text}");
                }
            };

            recognizer.Recognized += (_, eventArgs) =>
            {
                if (eventArgs.Result.Reason == ResultReason.TranslatedSpeech
                    && eventArgs.Result.Translations.TryGetValue(targetLanguage, out var translatedText))
                {
                    onTranslation(new TranslationResult(
                        recognitionLanguage,
                        eventArgs.Result.Text,
                        targetLanguage,
                        translatedText));

                    onStatus(translatedText);
                    return;
                }

                if (eventArgs.Result.Reason == ResultReason.NoMatch)
                {
                    onStatus("Aguardando fala...");
                }
            };

            recognizer.Canceled += (_, eventArgs) =>
            {
                var details = CancellationDetails.FromResult(eventArgs.Result);
                var message = string.IsNullOrWhiteSpace(details.ErrorDetails)
                    ? "Erro no reconhecimento."
                    : $"Erro no reconhecimento: {details.ErrorDetails}";

                onStatus(message);
            };

            await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);

            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }

            await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
            onStatus("Tradução finalizada.");
        }
        catch (OperationCanceledException)
        {
            onStatus("Tradução finalizada.");
        }
        catch
        {
            onStatus("Erro na tradução.");
        }
    }

    private SpeechTranslationConfig GetOrCreateConfig()
    {
        if (string.IsNullOrWhiteSpace(environment.Region))
        {
            throw new InvalidOperationException("Região Azure não configurada.");
        }

        cachedConfig ??= SpeechTranslationConfig.FromSubscription(environment.ApiKey, environment.Region);

        return cachedConfig;
    }
}
