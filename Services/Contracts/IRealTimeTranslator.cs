using System;
using System.Threading;
using System.Threading.Tasks;
using TraducaoRealtime.Services.Models;

namespace TraducaoRealtime.Services.Contracts;

internal interface IRealTimeTranslator
{
    Task StartAsync(
        TranslationMode mode,
        string? audioInputDeviceId,
        Action<string> onStatus,
        Action<TranslationResult> onTranslation,
        CancellationToken cancellationToken);
}
