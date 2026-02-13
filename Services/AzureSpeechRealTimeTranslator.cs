using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Translation;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace TraducaoRealtime.Services;

internal sealed class AzureSpeechRealTimeTranslator : IRealTimeTranslator
{
    private readonly TranslationEnvironment environment;
    private SpeechTranslationConfig? cachedConfig;

    public AzureSpeechRealTimeTranslator(TranslationEnvironment environment)
    {
        this.environment = environment;
    }

    public async Task StartAsync(
        TranslationMode mode,
        string? audioInputDeviceId,
        Action<string> onStatus,
        Action<TranslationResult> onTranslation,
        CancellationToken cancellationToken)
    {
        var recognitionLanguage = environment.GetRecognitionLanguage(mode);
        var targetLanguage = environment.GetTargetLanguage(mode);

        var config = GetOrCreateConfig();
        config.SpeechRecognitionLanguage = recognitionLanguage;
        config.AddTargetLanguage(targetLanguage);

        onStatus(string.IsNullOrWhiteSpace(audioInputDeviceId)
            ? "Aguardando fala... (fonte: entrada padrão)"
            : audioInputDeviceId.StartsWith("loopback:", StringComparison.OrdinalIgnoreCase)
                ? "Aguardando fala... (fonte: saída do PC selecionada)"
                : "Aguardando fala... (fonte: entrada selecionada)");

        try
        {
            using var selectedDeviceBridge = CreateSelectedDeviceBridge(audioInputDeviceId);
            using var audioConfig = selectedDeviceBridge is not null
                ? AudioConfig.FromStreamInput(selectedDeviceBridge.Stream)
                : CreateAudioConfig(audioInputDeviceId);
            using var recognizer = new TranslationRecognizer(config, audioConfig);

            var lastPartialText = string.Empty;
            var lastPartialUpdate = DateTimeOffset.MinValue;
            const int PartialMinIntervalMs = 450;

            selectedDeviceBridge?.Start();

            recognizer.Recognizing += (_, eventArgs) =>
            {
                if (!string.IsNullOrWhiteSpace(eventArgs.Result.Text))
                {
                    var partialText = eventArgs.Result.Translations.TryGetValue(targetLanguage, out var partialTranslated)
                        && !string.IsNullOrWhiteSpace(partialTranslated)
                        ? partialTranslated
                        : eventArgs.Result.Text;

                    if (string.IsNullOrWhiteSpace(partialText))
                    {
                        onStatus("Ouvindo...");
                        return;
                    }

                    var now = DateTimeOffset.UtcNow;
                    if ((now - lastPartialUpdate).TotalMilliseconds < PartialMinIntervalMs)
                    {
                        return;
                    }

                    if (string.Equals(partialText, lastPartialText, StringComparison.Ordinal))
                    {
                        return;
                    }

                    if (!string.IsNullOrEmpty(lastPartialText)
                        && !partialText.StartsWith(lastPartialText, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    if (!string.IsNullOrEmpty(lastPartialText)
                        && partialText.Length <= lastPartialText.Length)
                    {
                        return;
                    }

                    lastPartialText = partialText;
                    lastPartialUpdate = now;
                    onStatus($"Ouvindo (PT): {partialText}");
                }
            };

            recognizer.Recognized += (_, eventArgs) =>
            {
                if (eventArgs.Result.Reason == ResultReason.TranslatedSpeech
                    && eventArgs.Result.Translations.TryGetValue(targetLanguage, out var translatedText)
                    && !string.IsNullOrWhiteSpace(translatedText))
                {
                    onTranslation(new TranslationResult(
                        recognitionLanguage,
                        eventArgs.Result.Text,
                        targetLanguage,
                        translatedText));

                    lastPartialText = string.Empty;
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
        catch (Exception ex)
        {
            onStatus($"Erro na tradução: {ex.Message}");
        }
    }

    private SpeechTranslationConfig GetOrCreateConfig()
    {
        if (string.IsNullOrWhiteSpace(environment.Region))
        {
            throw new InvalidOperationException("Região Azure não configurada.");
        }

        cachedConfig ??= SpeechTranslationConfig.FromSubscription(environment.ApiKey, environment.Region);
        cachedConfig.SetProperty(PropertyId.SpeechServiceResponse_PostProcessingOption, "TrueText");

        return cachedConfig;
    }

    private static AudioConfig CreateAudioConfig(string? audioInputDeviceId)
    {
        return AudioConfig.FromDefaultMicrophoneInput();
    }

    private static WasapiDeviceAudioBridge? CreateSelectedDeviceBridge(string? audioInputDeviceId)
    {
        if (string.IsNullOrWhiteSpace(audioInputDeviceId))
        {
            return null;
        }

        var isLoopback = audioInputDeviceId.StartsWith("loopback:", StringComparison.OrdinalIgnoreCase);
        var isCapture = audioInputDeviceId.StartsWith("capture:", StringComparison.OrdinalIgnoreCase);
        if (!isLoopback && !isCapture)
        {
            return null;
        }

        var prefixLength = isLoopback ? "loopback:".Length : "capture:".Length;
        var deviceId = audioInputDeviceId[prefixLength..];
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        return new WasapiDeviceAudioBridge(deviceId, isLoopback);
    }

    private sealed class WasapiDeviceAudioBridge : IDisposable
    {
        private readonly IWaveIn capture;
        private readonly WaveFormat sourceWaveFormat;

        public WasapiDeviceAudioBridge(string deviceId, bool isLoopback)
        {
            using var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDevice(deviceId);
            capture = isLoopback ? new WasapiLoopbackCapture(device) : new WasapiCapture(device);
            sourceWaveFormat = capture.WaveFormat;

            var outputFormat = AudioStreamFormat.GetWaveFormatPCM(
                (uint)sourceWaveFormat.SampleRate,
                16,
                1);
            Stream = AudioInputStream.CreatePushStream(outputFormat);

            capture.DataAvailable += OnDataAvailable;
            capture.RecordingStopped += (_, __) => Stream.Close();
        }

        public PushAudioInputStream Stream { get; }

        public void Start()
        {
            capture.StartRecording();
        }

        public void Dispose()
        {
            try
            {
                capture.DataAvailable -= OnDataAvailable;
                if (capture is WasapiCapture wasapiCapture && wasapiCapture.CaptureState != CaptureState.Stopped)
                {
                    capture.StopRecording();
                }
                else if (capture is not WasapiCapture)
                {
                    capture.StopRecording();
                }
            }
            catch
            {
                // Ignore stop errors during disposal.
            }

            capture.Dispose();
            Stream.Close();
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded <= 0)
            {
                return;
            }

            var bytesPerSample = Math.Max(1, sourceWaveFormat.BitsPerSample / 8);
            var channels = Math.Max(1, sourceWaveFormat.Channels);
            var blockAlign = Math.Max(1, sourceWaveFormat.BlockAlign);
            var frameCount = e.BytesRecorded / blockAlign;
            if (frameCount <= 0)
            {
                return;
            }

            var pcmBytes = new byte[frameCount * 2];
            var targetIndex = 0;

            for (var frame = 0; frame < frameCount; frame++)
            {
                float sum = 0;
                for (var ch = 0; ch < channels; ch++)
                {
                    var sampleOffset = (frame * blockAlign) + (ch * bytesPerSample);
                    var sample = ReadSampleAsFloat(e.Buffer, sampleOffset, sourceWaveFormat);
                    sum += sample;
                }

                var mono = sum / channels;
                mono = Math.Clamp(mono, -1f, 1f);
                var pcm = (short)(mono * short.MaxValue);
                pcmBytes[targetIndex++] = (byte)(pcm & 0xFF);
                pcmBytes[targetIndex++] = (byte)((pcm >> 8) & 0xFF);
            }

            Stream.Write(pcmBytes);
        }

        private static float ReadSampleAsFloat(byte[] buffer, int offset, WaveFormat format)
        {
            var encoding = format.Encoding;
            var bits = format.BitsPerSample;

            if (encoding == WaveFormatEncoding.IeeeFloat && bits == 32)
            {
                return BitConverter.ToSingle(buffer, offset);
            }

            if ((encoding == WaveFormatEncoding.Pcm || encoding == WaveFormatEncoding.Extensible) && bits == 16)
            {
                var sample = BitConverter.ToInt16(buffer, offset);
                return sample / 32768f;
            }

            if ((encoding == WaveFormatEncoding.Pcm || encoding == WaveFormatEncoding.Extensible) && bits == 24)
            {
                var sample = (buffer[offset + 2] << 24) | (buffer[offset + 1] << 16) | (buffer[offset] << 8);
                sample >>= 8;
                return sample / 8388608f;
            }

            if ((encoding == WaveFormatEncoding.Pcm || encoding == WaveFormatEncoding.Extensible) && bits == 32)
            {
                var sample = BitConverter.ToInt32(buffer, offset);
                return sample / 2147483648f;
            }

            return 0f;
        }
    }

}
