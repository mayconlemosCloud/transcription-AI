using System;
using System.Threading;
using System.Threading.Tasks;

namespace TraducaoRealtime.Services;

internal interface IRealTimeTranslationProvider
{
    Task StartAsync(
        TranslationMode mode,
        Action<string> onStatus,
        Action<TranslationResult> onTranslation,
        CancellationToken cancellationToken);
}
