using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TraducaoRealtime;

public class AiAnalysisForm : Form
{
    private enum SessionType
    {
        TechnicalInterview,
        FreeTopic
    }

    private readonly Func<IReadOnlyList<string>> getHistory;
    private readonly ComboBox sessionTypeCombo;
    private readonly RadioButton conversationSourceRadio;
    private readonly RadioButton manualSourceRadio;
    private readonly Label manualInputLabel;
    private readonly RichTextBox manualInputBox;
    private readonly Button analyzeButton;
    private readonly RichTextBox responseBox;

    private static readonly HttpClient httpClient = new HttpClient();
    private const string ClaudeModel = "claude-sonnet-4-5-20250929";
    private const int ClaudeMaxTokens = 256;
    private const string ClaudeEndpoint = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";
    private const string AnthropicApiKeyEnv = "ANTHROPIC_API_KEY";
    private static readonly string ProfilePath = Path.Combine(AppContext.BaseDirectory, "rag", "perfil.md");
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "logs", "ia.log");

    public AiAnalysisForm(Func<IReadOnlyList<string>> getHistory)
    {
        this.getHistory = getHistory;

        Text = string.Empty;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = true;
        BackColor = Color.FromArgb(24, 27, 32);
        ForeColor = Color.White;
        Size = new Size(680, 520);
        MinimumSize = new Size(480, 340);
        Padding = new Padding(12);

        var sessionLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 20,
            Text = "Sessão",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(170, 190, 210),
            TextAlign = ContentAlignment.MiddleLeft
        };

        sessionTypeCombo = new ComboBox
        {
            Dock = DockStyle.Top,
            Height = 30,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
            BackColor = Color.FromArgb(40, 48, 58),
            ForeColor = Color.FromArgb(235, 245, 255)
        };
        sessionTypeCombo.Items.Add("Entrevista técnica");
        sessionTypeCombo.Items.Add("Tema livre");
        sessionTypeCombo.SelectedIndex = 0;

        var sourceLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 20,
            Text = "Fonte para análise",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(170, 190, 210),
            TextAlign = ContentAlignment.MiddleLeft
        };

        conversationSourceRadio = new RadioButton
        {
            AutoSize = true,
            Text = "Analisar conversa",
            Checked = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(220, 235, 250),
            BackColor = BackColor,
            Margin = new Padding(0, 0, 18, 0)
        };

        manualSourceRadio = new RadioButton
        {
            AutoSize = true,
            Text = "Analisar texto escrito",
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(220, 235, 250),
            BackColor = BackColor
        };

        var sourceOptionsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 34,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0)
        };
        sourceOptionsPanel.Controls.Add(conversationSourceRadio);
        sourceOptionsPanel.Controls.Add(manualSourceRadio);

        manualInputLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 18,
            Text = "Texto para análise",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(170, 190, 210),
            TextAlign = ContentAlignment.MiddleLeft,
            Visible = false
        };

        manualInputBox = CreateEditableBox();
        manualInputBox.Dock = DockStyle.Top;
        manualInputBox.Height = 140;
        manualInputBox.Visible = false;

        analyzeButton = new Button
        {
            Text = "Gerar resposta",
            Dock = DockStyle.Top,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(40, 48, 58),
            ForeColor = Color.FromArgb(235, 245, 255)
        };
        analyzeButton.Click += async (_, __) => await AnalyzeContextAsync();

        var responseLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 18,
            Text = "Resposta",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(170, 190, 210),
            TextAlign = ContentAlignment.MiddleLeft
        };

        responseBox = CreateReadOnlyBox();
        responseBox.Dock = DockStyle.Fill;
        responseBox.Text = "A resposta sugerida aparece aqui.";

        conversationSourceRadio.CheckedChanged += (_, __) => UpdateSourceMode();
        manualSourceRadio.CheckedChanged += (_, __) => UpdateSourceMode();

        Controls.Add(responseBox);
        Controls.Add(responseLabel);
        Controls.Add(analyzeButton);
        Controls.Add(manualInputBox);
        Controls.Add(manualInputLabel);
        Controls.Add(sourceOptionsPanel);
        Controls.Add(sourceLabel);
        Controls.Add(sessionTypeCombo);
        Controls.Add(sessionLabel);

        UpdateSourceMode();
    }

    public void PositionNear(Form referenceForm)
    {
        if (referenceForm is null)
        {
            return;
        }

        var x = referenceForm.Left + referenceForm.Width + 12;
        var y = referenceForm.Top;
        Left = x;
        Top = y;
    }

    private void UpdateSourceMode()
    {
        var useManualText = manualSourceRadio.Checked;
        manualInputLabel.Visible = useManualText;
        manualInputBox.Visible = useManualText;
    }

    private static RichTextBox CreateReadOnlyBox()
    {
        return new RichTextBox
        {
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(210, 224, 240),
            BackColor = Color.FromArgb(26, 30, 36),
            BorderStyle = BorderStyle.None,
            ReadOnly = true,
            Multiline = true,
            WordWrap = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            DetectUrls = false
        };
    }

    private static RichTextBox CreateEditableBox()
    {
        return new RichTextBox
        {
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(235, 245, 255),
            BackColor = Color.FromArgb(32, 36, 43),
            BorderStyle = BorderStyle.None,
            ReadOnly = false,
            Multiline = true,
            WordWrap = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            DetectUrls = false,
            AcceptsTab = true
        };
    }

    private async Task AnalyzeContextAsync()
    {
        AppendLog("[IA] Analise iniciada.");

        var useManualText = manualSourceRadio.Checked;
        var context = useManualText
            ? (manualInputBox.Text?.Trim() ?? string.Empty)
            : string.Join(" ", getHistory());

        if (string.IsNullOrWhiteSpace(context))
        {
            responseBox.Text = useManualText
                ? "Digite um texto para analisar."
                : "Sem histórico de conversa para analisar.";
            return;
        }

        var sourceType = useManualText ? "TEXTO DO USUARIO" : "HISTORICO DA CONVERSA";
        var sessionType = GetSelectedSessionType();
        var profile = LoadProfileText();
        var requestBody = BuildClaudeRequest(profile, context, sourceType, sessionType);
        var apiKey = Environment.GetEnvironmentVariable(AnthropicApiKeyEnv);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            responseBox.Text = "ANTHROPIC_API_KEY nao configurada.";
            return;
        }

        analyzeButton.Enabled = false;
        responseBox.Text = "Analisando...";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ClaudeEndpoint);
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("anthropic-version", AnthropicVersion);
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            using var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var aiOutput = ExtractClaudeText(json);
            ApplyAiOutput(aiOutput);

            AppendLog("[IA] Analise concluida.");
        }
        catch (Exception ex)
        {
            AppendLog("[IA] Erro: " + ex);
            responseBox.Text = "Falha ao analisar contexto.";
        }
        finally
        {
            analyzeButton.Enabled = true;
        }
    }

    private SessionType GetSelectedSessionType()
    {
        return sessionTypeCombo.SelectedIndex == 1
            ? SessionType.FreeTopic
            : SessionType.TechnicalInterview;
    }

    private static string BuildClaudeRequest(string profile, string context, string sourceType, SessionType sessionType)
    {
        var systemPrompt = new StringBuilder();
        if (sessionType == SessionType.TechnicalInterview)
        {
            systemPrompt.AppendLine("Voce e um assistente de entrevista tecnica em ingles.");
            systemPrompt.AppendLine("Priorize clareza tecnica, objetividade e exemplos práticos quando necessário.");
            systemPrompt.AppendLine("Se o contexto estiver fora de entrevista tecnica, ainda responda de forma util e profissional sem se perder.");
        }
        else
        {
            systemPrompt.AppendLine("Voce e um assistente geral para conversa em ingles.");
            systemPrompt.AppendLine("Responda de forma natural, clara e coerente com o tema atual.");
            systemPrompt.AppendLine("Nao force linguagem de entrevista tecnica quando o tema for livre.");
        }

        systemPrompt.AppendLine("Analise o contexto enviado e gere uma resposta sugerida objetiva.");
        systemPrompt.AppendLine("Responda apenas em JSON valido, sem texto extra.");
        systemPrompt.AppendLine("Formato:");
        systemPrompt.AppendLine("{");
        systemPrompt.AppendLine("  \"summary\": \"<1-2 linhas>\",");
        systemPrompt.AppendLine("  \"response\": \"<2-4 frases em ingles>\"");
        systemPrompt.AppendLine("}");

        var userPrompt = new StringBuilder();
        if (sessionType == SessionType.TechnicalInterview)
        {
            userPrompt.AppendLine("PERFIL:");
            userPrompt.AppendLine(profile);
            userPrompt.AppendLine();
        }

        userPrompt.AppendLine("SESSAO:");
        userPrompt.AppendLine(sessionType == SessionType.TechnicalInterview ? "ENTREVISTA TECNICA" : "TEMA LIVRE");
        userPrompt.AppendLine();
        userPrompt.AppendLine("FONTE:");
        userPrompt.AppendLine(sourceType);
        userPrompt.AppendLine();
        userPrompt.AppendLine("CONTEXTO:");
        userPrompt.AppendLine(context);

        var payload = new
        {
            model = ClaudeModel,
            max_tokens = ClaudeMaxTokens,
            system = systemPrompt.ToString(),
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = userPrompt.ToString()
                }
            }
        };

        return JsonSerializer.Serialize(payload);
    }

    private static string ExtractClaudeText(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in content.EnumerateArray())
                {
                    if (item.TryGetProperty("type", out var type) && type.GetString() == "text"
                        && item.TryGetProperty("text", out var text))
                    {
                        return text.GetString() ?? string.Empty;
                    }
                }
            }
        }
        catch
        {
            return string.Empty;
        }

        return string.Empty;
    }

    private void ApplyAiOutput(string content)
    {
        if (TryParseJsonOutput(content, out var response))
        {
            responseBox.Text = string.IsNullOrWhiteSpace(response) ? "Sem resposta." : response;
            return;
        }

        responseBox.Text = string.IsNullOrWhiteSpace(content) ? "Sem resposta." : content.Trim();
    }

    private static bool TryParseJsonOutput(string content, out string response)
    {
        response = string.Empty;

        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (root.TryGetProperty("response", out var responseElement))
            {
                response = responseElement.GetString() ?? string.Empty;
            }

            return !string.IsNullOrWhiteSpace(response);
        }
        catch
        {
            return false;
        }
    }

    private static string LoadProfileText()
    {
        try
        {
            if (File.Exists(ProfilePath))
            {
                return File.ReadAllText(ProfilePath);
            }
        }
        catch
        {
            return "Perfil nao encontrado.";
        }

        return "Perfil nao encontrado.";
    }

    private static void AppendLog(string message)
    {
        try
        {
            var directory = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}";
            File.AppendAllText(LogPath, line);
        }
        catch
        {
        }
    }
}
