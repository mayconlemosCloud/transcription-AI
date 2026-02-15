namespace TraducaoRealtime.Services.Models;

internal sealed record TranslationResult(
    string RecognitionLanguage,
    string RecognizedText,
    string TargetLanguage,
    string TranslatedText);