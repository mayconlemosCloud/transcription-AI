using System;
using System.Threading;
using System.Threading.Tasks;

namespace TraducaoRealtime.Services;

internal interface IRealTimeTranslator
{
    Task StartAsync(
        TranslationMode mode,
        string? audioInputDeviceId,
        Action<string> onStatus,
        Action<TranslationResult> onTranslation,
        CancellationToken cancellationToken);
}
