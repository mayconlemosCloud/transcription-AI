using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TraducaoRealtime.Configuration;
using TraducaoRealtime.Services;

namespace TraducaoRealtime;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        EnvironmentLoader.LoadDotEnvIfAvailable();
        ApplicationConfiguration.Initialize();

        if (!TranslationEnvironment.TryCreateFromEnvironment(out var translationEnvironment, out var errorMessage))
        {
            MessageBox.Show(
                errorMessage,
                "Configuração inválida",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        using var form = new OverlayForm();
        IRealTimeTranslator translator = new RealTimeTranslator(translationEnvironment!);
        AiAnalysisForm? aiAnalysisForm = null;
        CancellationTokenSource? cancellationTokenSource = null;
        var aiMinimizedByMain = false;

        form.AiAssistantRequested += () =>
        {
            aiAnalysisForm ??= new AiAnalysisForm(form.GetHistoryForAnalysis);

            if (!aiAnalysisForm.Visible)
            {
                aiAnalysisForm.Show();
            }

            if (aiAnalysisForm.WindowState == FormWindowState.Minimized)
            {
                aiAnalysisForm.WindowState = FormWindowState.Normal;
            }

            aiAnalysisForm.PositionNear(form);
            aiAnalysisForm.Activate();
        };
        form.ConfigurationRequested += () =>
        {
            cancellationTokenSource?.Cancel();
        };
        form.FormClosed += (_, _) =>
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();

            if (aiAnalysisForm is not null && !aiAnalysisForm.IsDisposed)
            {
                aiAnalysisForm.Close();
            }
        };
        form.Move += (_, _) =>
        {
            if (aiAnalysisForm is not null && !aiAnalysisForm.IsDisposed && aiAnalysisForm.Visible)
            {
                aiAnalysisForm.PositionNear(form);
            }
        };
        form.Resize += (_, _) =>
        {
            if (aiAnalysisForm is null || aiAnalysisForm.IsDisposed)
            {
                return;
            }

            if (form.WindowState == FormWindowState.Minimized)
            {
                if (aiAnalysisForm.WindowState != FormWindowState.Minimized)
                {
                    aiAnalysisForm.WindowState = FormWindowState.Minimized;
                    aiMinimizedByMain = true;
                }

                return;
            }

            if (aiMinimizedByMain)
            {
                aiAnalysisForm.WindowState = FormWindowState.Normal;
                aiMinimizedByMain = false;
            }

            if (aiAnalysisForm.Visible)
            {
                aiAnalysisForm.PositionNear(form);
            }
        };

        form.ModeSelected += (mode, audioInputDeviceId) =>
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = new CancellationTokenSource();
            var currentToken = cancellationTokenSource.Token;

            _ = Task.Run(() => translator.StartAsync(
                mode,
                audioInputDeviceId,
                form.SetContentText,
                result => form.AddTranslation(
                    result.RecognitionLanguage,
                    result.RecognizedText,
                    result.TargetLanguage,
                    result.TranslatedText),
                currentToken));
        };

        Application.Run(form);
    }
}
