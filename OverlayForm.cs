using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NAudio.CoreAudioApi;

namespace TraducaoRealtime;

public class OverlayForm : Form
{
    private readonly Label statusLabel;
    private readonly RichTextBox liveCaptionBox;
    private readonly RichTextBox portugueseBox;
    private readonly Panel setupPanel;
    private readonly Panel liveActionsPanel;
    private readonly ComboBox audioDeviceCombo;
    private readonly Panel opacityPanel;
    private readonly Label opacityLabel;
    private readonly TrackBar opacityTrackBar;
    private readonly Button englishToPortugueseButton;
    private readonly Button portugueseToEnglishButton;
    private readonly Button aiAssistantButton;
    private readonly Button changeModeButton;
    private readonly Button closeSessionButton;
    private readonly List<AudioInputOption> audioInputOptions = new List<AudioInputOption>();
    private readonly List<TranslationEntry> history = new List<TranslationEntry>();
    private readonly List<DateTimeOffset> historyTimestamps = new List<DateTimeOffset>();
    private const int HistorySize = 6;
    private const int LiveCaptionLineLength = 58;
    private const int SubtitleLineLength = 42;
    private const int SubtitleMaxLines = 2;
    private const int MergeQuickPauseMsDefault = 1000;
    private const int NewBlockPauseMsDefault = 1500;
    private static readonly TimeSpan MergeQuickPause = TimeSpan.FromMilliseconds(ReadIntFromEnv("MERGE_QUICK_PAUSE_MS", MergeQuickPauseMsDefault));
    private static readonly TimeSpan NewBlockPause = TimeSpan.FromMilliseconds(ReadIntFromEnv("NEW_BLOCK_PAUSE_MS", NewBlockPauseMsDefault));
    private static readonly string[] MergeConnectors =
    {
        "e", "mas", "ou", "que", "porque", "entao", "então", "de", "do", "da", "dos", "das", "com", "para"
    };
    private readonly Font historyFont;
    private readonly Font historyFontBold;
    private readonly Color historyTextColor = Color.FromArgb(210, 224, 240);
    private readonly Color historyHighlightColor = Color.FromArgb(255, 225, 120);

    public event Action<TranslationMode, string?>? ModeSelected;
    public event Action? AiAssistantRequested;
    public event Action? ConfigurationRequested;

    public OverlayForm()
    {
        Text = string.Empty;
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        ShowInTaskbar = true;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(24, 27, 32);
        ForeColor = Color.White;
        Opacity = 0.94;
        Size = new Size(780, 360);
        MinimumSize = new Size(520, 220);
        Padding = new Padding(14, 12, 14, 12);

        statusLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 20,
            Text = "Configure modo e áudio para iniciar.",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(170, 190, 210)
        };

        historyFont = new Font("Segoe UI", 10.5F, FontStyle.Regular, GraphicsUnit.Point);
        historyFontBold = new Font("Segoe UI", 10.5F, FontStyle.Bold, GraphicsUnit.Point);

        liveCaptionBox = CreateHistoryBox();
        liveCaptionBox.Dock = DockStyle.Fill;
        liveCaptionBox.Font = new Font("Segoe UI", 15F, FontStyle.Bold, GraphicsUnit.Point);
        liveCaptionBox.ForeColor = Color.FromArgb(245, 249, 255);
        liveCaptionBox.BackColor = Color.FromArgb(26, 30, 36);
        liveCaptionBox.ScrollBars = RichTextBoxScrollBars.None;
        liveCaptionBox.Margin = new Padding(0, 6, 0, 0);
        liveCaptionBox.Text = "Aguardando início...";

        portugueseBox = CreateHistoryBox();
        portugueseBox.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular, GraphicsUnit.Point);

        setupPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 112,
            Padding = new Padding(0, 8, 0, 0)
        };

        var sourceLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Left,
            Width = 190,
            Text = "Fonte de áudio:",
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(210, 224, 240)
        };

        audioDeviceCombo = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
            BackColor = Color.FromArgb(40, 48, 58),
            ForeColor = Color.FromArgb(235, 245, 255)
        };
        LoadAudioInputOptions();

        var sourcePanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 26
        };
        sourcePanel.Controls.Add(audioDeviceCombo);
        sourcePanel.Controls.Add(sourceLabel);

        opacityLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Left,
            Width = 110,
            Text = "Transparencia",
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(210, 224, 240)
        };

        opacityTrackBar = new TrackBar
        {
            Dock = DockStyle.Fill,
            Minimum = 40,
            Maximum = 100,
            TickFrequency = 10,
            Value = (int)(Opacity * 100),
            AutoSize = false,
            Height = 24
        };
        opacityTrackBar.ValueChanged += (_, __) =>
        {
            Opacity = opacityTrackBar.Value / 100.0;
        };

        opacityPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 32
        };
        opacityPanel.Controls.Add(opacityTrackBar);
        opacityPanel.Controls.Add(opacityLabel);

        englishToPortugueseButton = CreateModeButton("EN -> PT");
        englishToPortugueseButton.Click += (_, __) => SelectMode(TranslationMode.EnglishToPortuguese);

        portugueseToEnglishButton = CreateModeButton("PT -> EN");
        portugueseToEnglishButton.Click += (_, __) => SelectMode(TranslationMode.PortugueseToEnglish);

        var buttonLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 38,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        buttonLayout.Controls.Add(englishToPortugueseButton);
        buttonLayout.Controls.Add(portugueseToEnglishButton);
        setupPanel.Controls.Add(buttonLayout);
        setupPanel.Controls.Add(opacityPanel);
        setupPanel.Controls.Add(sourcePanel);

        aiAssistantButton = CreateModeButton("IA");
        aiAssistantButton.Click += (_, __) => AiAssistantRequested?.Invoke();

        changeModeButton = CreateModeButton("Configurar");
        changeModeButton.Click += (_, __) => ReturnToConfiguration();

        closeSessionButton = CreateModeButton("Fechar");
        closeSessionButton.Click += (_, __) => Close();

        var liveButtonsLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        liveButtonsLayout.Controls.Add(aiAssistantButton);
        liveButtonsLayout.Controls.Add(changeModeButton);
        liveButtonsLayout.Controls.Add(closeSessionButton);

        liveActionsPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            Visible = false,
            Padding = new Padding(0, 8, 0, 0)
        };
        liveActionsPanel.Controls.Add(liveButtonsLayout);

        Controls.Add(liveCaptionBox);
        Controls.Add(statusLabel);
        Controls.Add(liveActionsPanel);
        Controls.Add(setupPanel);

        Resize += (_, __) => ApplyRoundedCorners();
        Load += (_, __) =>
        {
            LoadAudioInputOptions();
            EnableAcrylic(Handle, Color.FromArgb(24, 27, 32), 180);
            ApplyRoundedCorners();
            ApplyRoundedCornerPreference();
        };
    }

    public void SetContentText(string text)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(SetContentText), text);
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            statusLabel.Text = "Aguardando fala...";
            return;
        }

        if (text.StartsWith("Ouvindo:", StringComparison.OrdinalIgnoreCase))
        {
            var payload = text.Substring("Ouvindo:".Length).Trim();
            statusLabel.Text = "Ouvindo...";
            if (!string.IsNullOrWhiteSpace(payload))
            {
                SetLiveCaption(payload, highlightNewestWord: false);
            }
            return;
        }

        if (text.StartsWith("Ouvindo (PT):", StringComparison.OrdinalIgnoreCase))
        {
            var payload = text.Substring("Ouvindo (PT):".Length).Trim();
            statusLabel.Text = "Ouvindo em PT...";
            if (!string.IsNullOrWhiteSpace(payload))
            {
                SetLiveCaption(payload, highlightNewestWord: false);
            }
            return;
        }

        if (text.Equals("Aguardando fala...", StringComparison.OrdinalIgnoreCase))
        {
            statusLabel.Text = "Aguardando fala...";
            return;
        }

        if (text.Equals("Ouvindo...", StringComparison.OrdinalIgnoreCase))
        {
            statusLabel.Text = "Ouvindo...";
            return;
        }

        if (text.Equals("Tradução finalizada.", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Traducao finalizada.", StringComparison.OrdinalIgnoreCase))
        {
            statusLabel.Text = "Tradução finalizada.";
            return;
        }

        if (text.Equals("Transcrição finalizada.", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Transcricao finalizada.", StringComparison.OrdinalIgnoreCase))
        {
            statusLabel.Text = "Transcrição finalizada.";
            return;
        }

        statusLabel.Text = "Legenda atualizada.";
        SetLiveCaption(text, highlightNewestWord: false);
    }

    public void AddTranslation(string recognitionLanguage, string recognizedText, string targetLanguage, string translatedText)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action<string, string, string, string>(AddTranslation), recognitionLanguage, recognizedText, targetLanguage, translatedText);
            return;
        }

        var entry = BuildEntry(recognitionLanguage, recognizedText, targetLanguage, translatedText);
        AddOrMergeHistory(entry);

        if (!string.IsNullOrWhiteSpace(entry.Portuguese))
        {
            SetLiveCaption(entry.Portuguese, highlightNewestWord: false);
            statusLabel.Text = "Tradução recebida.";
        }

        UpdateHistoryText();
    }

    public IReadOnlyList<string> GetHistoryForAnalysis()
    {
        if (IsDisposed)
        {
            return Array.Empty<string>();
        }

        if (InvokeRequired)
        {
            return (IReadOnlyList<string>)Invoke(new Func<IReadOnlyList<string>>(GetHistoryForAnalysis));
        }

        return history
            .Select(entry => !string.IsNullOrWhiteSpace(entry.Portuguese)
                ? entry.Portuguese.Trim()
                : entry.English.Trim())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();
    }

    private void SelectMode(TranslationMode mode)
    {
        setupPanel.Visible = false;
        liveActionsPanel.Visible = true;
        SetContentText("Aguardando fala...");
        ModeSelected?.Invoke(mode, GetSelectedAudioDeviceId());
    }

    private void ReturnToConfiguration()
    {
        ConfigurationRequested?.Invoke();
        setupPanel.Visible = true;
        liveActionsPanel.Visible = false;
        statusLabel.Text = "Escolha modo e áudio.";
    }

    private string? GetSelectedAudioDeviceId()
    {
        var selectedIndex = audioDeviceCombo.SelectedIndex;
        if (selectedIndex < 0 || selectedIndex >= audioInputOptions.Count)
        {
            return null;
        }

        return audioInputOptions[selectedIndex].DeviceId;
    }

    private void LoadAudioInputOptions()
    {
        audioInputOptions.Clear();
        audioDeviceCombo.Items.Clear();

        audioInputOptions.Add(new AudioInputOption("Entrada padrão (microfone)", null));

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var captureDevices = enumerator
                .EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                .OrderBy(device => device.FriendlyName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var device in captureDevices)
            {
                audioInputOptions.Add(new AudioInputOption($"Entrada: {device.FriendlyName}", $"capture:{device.ID}"));
            }

            var renderDevices = enumerator
                .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                .OrderBy(device => device.FriendlyName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var device in renderDevices)
            {
                audioInputOptions.Add(new AudioInputOption($"Saída (captura): {device.FriendlyName}", $"loopback:{device.ID}"));
            }
        }
        catch
        {
            // Keep default option if enumeration fails.
        }

        foreach (var option in audioInputOptions)
        {
            audioDeviceCombo.Items.Add(option.DisplayName);
        }

        audioDeviceCombo.SelectedIndex = 0;
    }

    private void UpdateHistoryText()
    {
        var visibleCount = Math.Min(history.Count, HistorySize);
        if (visibleCount == 0)
        {
            portugueseBox.Clear();
            return;
        }

        var portugueseText = new List<string>(visibleCount);
        var startIndex = history.Count - visibleCount;

        for (int i = startIndex; i < history.Count; i++)
        {
            portugueseText.Add(history[i].Portuguese);
        }

        SetHistoryBoxText(portugueseBox, portugueseText);
    }

    private void AddOrMergeHistory(TranslationEntry incoming)
    {
        var now = DateTimeOffset.UtcNow;

        if (history.Count == 0)
        {
            history.Add(incoming);
            historyTimestamps.Add(now);
            return;
        }

        var previousTimestamp = historyTimestamps[historyTimestamps.Count - 1];
        var previous = history[history.Count - 1];
        if (!ShouldMerge(previous.Portuguese, incoming.Portuguese, now - previousTimestamp))
        {
            history.Add(incoming);
            historyTimestamps.Add(now);
            return;
        }

        var mergedPortuguese = MergePhrase(previous.Portuguese, incoming.Portuguese);
        var mergedEnglish = MergePhrase(previous.English, incoming.English);
        history[history.Count - 1] = new TranslationEntry(mergedEnglish, mergedPortuguese);
        historyTimestamps[historyTimestamps.Count - 1] = now;
    }

    private static bool ShouldMerge(string previousText, string currentText, TimeSpan elapsedSincePrevious)
    {
        if (string.IsNullOrWhiteSpace(previousText) || string.IsNullOrWhiteSpace(currentText))
        {
            return false;
        }

        var previousTrim = previousText.Trim();
        var currentTrim = currentText.Trim();

        if (previousTrim.EndsWith(".", StringComparison.Ordinal)
            || previousTrim.EndsWith("?", StringComparison.Ordinal)
            || previousTrim.EndsWith("!", StringComparison.Ordinal)
            || previousTrim.EndsWith(":", StringComparison.Ordinal)
            || previousTrim.EndsWith(";", StringComparison.Ordinal))
        {
            return false;
        }

        if (elapsedSincePrevious >= NewBlockPause)
        {
            return false;
        }

        if (elapsedSincePrevious <= MergeQuickPause)
        {
            return true;
        }

        if (currentTrim.Length <= 45)
        {
            return true;
        }

        var firstWord = currentTrim.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        if (MergeConnectors.Contains(firstWord, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return char.IsLower(currentTrim[0]);
    }

    private static int ReadIntFromEnv(string variableName, int defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        if (!int.TryParse(value, out var parsed) || parsed <= 0)
        {
            return defaultValue;
        }

        return parsed;
    }

    private static string MergePhrase(string previousText, string currentText)
    {
        if (string.IsNullOrWhiteSpace(previousText))
        {
            return currentText?.Trim() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(currentText))
        {
            return previousText.Trim();
        }

        var previousTrim = previousText.Trim();
        var currentTrim = currentText.Trim();

        if (previousTrim.EndsWith("-", StringComparison.Ordinal))
        {
            return previousTrim + currentTrim;
        }

        return previousTrim + " " + currentTrim;
    }

    private static TranslationEntry BuildEntry(string recognitionLanguage, string recognizedText, string targetLanguage, string translatedText)
    {
        var recIsEnglish = IsEnglish(recognitionLanguage);
        var recIsPortuguese = IsPortuguese(recognitionLanguage);
        var targetIsEnglish = IsEnglish(targetLanguage);
        var targetIsPortuguese = IsPortuguese(targetLanguage);

        var english = string.Empty;
        var portuguese = string.Empty;

        if (recIsEnglish)
        {
            english = recognizedText;
        }

        if (recIsPortuguese)
        {
            portuguese = recognizedText;
        }

        if (targetIsEnglish)
        {
            english = translatedText;
        }

        if (targetIsPortuguese)
        {
            portuguese = translatedText;
        }

        if (string.IsNullOrWhiteSpace(english))
        {
            english = translatedText;
        }

        if (string.IsNullOrWhiteSpace(portuguese))
        {
            portuguese = recognizedText;
        }

        return new TranslationEntry(english, portuguese);
    }

    private RichTextBox CreateHistoryBox()
    {
        return new RichTextBox
        {
            Dock = DockStyle.Fill,
            Font = historyFont,
            ForeColor = historyTextColor,
            BackColor = BackColor,
            BorderStyle = BorderStyle.None,
            ReadOnly = true,
            Multiline = true,
            WordWrap = true,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            DetectUrls = false
        };
    }

    private void SetHistoryBoxText(RichTextBox box, List<string> entries)
    {
        var wrappedEntries = entries.Select(WrapCaption).ToList();
        var text = string.Join(Environment.NewLine + Environment.NewLine, wrappedEntries);
        box.Text = text;

        if (wrappedEntries.Count == 0)
        {
            return;
        }

        var lastEntry = wrappedEntries[wrappedEntries.Count - 1];
        var highlightStart = text.LastIndexOf(lastEntry, StringComparison.Ordinal);
        if (highlightStart < 0)
        {
            return;
        }

        box.SelectionStart = highlightStart;
        box.SelectionLength = lastEntry.Length;
        box.SelectionColor = historyHighlightColor;
        box.SelectionFont = historyFontBold;
        box.SelectionLength = 0;
        box.SelectionStart = box.TextLength;
        box.ScrollToCaret();
    }

    private static string WrapCaption(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var words = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var currentLineLength = 0;

        foreach (var word in words)
        {
            var projectedLength = currentLineLength == 0 ? word.Length : currentLineLength + 1 + word.Length;
            if (projectedLength > LiveCaptionLineLength)
            {
                builder.AppendLine();
                builder.Append(word);
                currentLineLength = word.Length;
                continue;
            }

            if (currentLineLength > 0)
            {
                builder.Append(' ');
                currentLineLength++;
            }

            builder.Append(word);
            currentLineLength += word.Length;
        }

        return builder.ToString();
    }

    private void SetLiveCaption(string text, bool highlightNewestWord)
    {
        var wrapped = WrapSubtitleCaption(text);
        liveCaptionBox.Text = wrapped;

        if (string.IsNullOrWhiteSpace(wrapped))
        {
            return;
        }

        liveCaptionBox.SelectionStart = 0;
        liveCaptionBox.SelectionLength = liveCaptionBox.TextLength;
        liveCaptionBox.SelectionColor = liveCaptionBox.ForeColor;
        liveCaptionBox.SelectionFont = liveCaptionBox.Font;
        liveCaptionBox.SelectionAlignment = HorizontalAlignment.Center;

        liveCaptionBox.SelectionLength = 0;
        liveCaptionBox.SelectionStart = 0;
    }

    private static string WrapSubtitleCaption(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var words = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return string.Empty;
        }

        var lines = new List<string>();
        var currentLine = new StringBuilder();

        foreach (var word in words)
        {
            var projectedLength = currentLine.Length == 0
                ? word.Length
                : currentLine.Length + 1 + word.Length;

            if (projectedLength > SubtitleLineLength && currentLine.Length > 0)
            {
                lines.Add(currentLine.ToString());
                currentLine.Clear();
            }

            if (currentLine.Length > 0)
            {
                currentLine.Append(' ');
            }

            currentLine.Append(word);
        }

        if (currentLine.Length > 0)
        {
            lines.Add(currentLine.ToString());
        }

        if (lines.Count <= SubtitleMaxLines)
        {
            return string.Join(Environment.NewLine, lines);
        }

        return string.Join(Environment.NewLine, lines.Skip(lines.Count - SubtitleMaxLines));
    }

    private static string GetNewestWord(string text)
    {
        var words = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return string.Empty;
        }

        return words[words.Length - 1];
    }

    private static bool IsEnglish(string language)
    {
        return !string.IsNullOrWhiteSpace(language) && language.StartsWith("en", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPortuguese(string language)
    {
        return !string.IsNullOrWhiteSpace(language) && language.StartsWith("pt", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TranslationEntry
    {
        public TranslationEntry(string english, string portuguese)
        {
            English = english;
            Portuguese = portuguese;
        }

        public string English { get; }

        public string Portuguese { get; }
    }

    private sealed class AudioInputOption
    {
        public AudioInputOption(string displayName, string? deviceId)
        {
            DisplayName = displayName;
            DeviceId = deviceId;
        }

        public string DisplayName { get; }

        public string? DeviceId { get; }
    }

    private static Button CreateModeButton(string text)
    {
        return new Button
        {
            Text = text,
            AutoSize = true,
            Padding = new Padding(8, 4, 8, 4),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(40, 48, 58),
            ForeColor = Color.FromArgb(235, 245, 255)
        };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var pen = new Pen(Color.FromArgb(140, 255, 255, 255), 1f);
        var rect = ClientRectangle;
        rect.Inflate(-1, -1);
        using var path = CreateRoundedRectPath(rect, 16);
        e.Graphics.DrawPath(pen, path);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int CS_DROPSHADOW = 0x00020000;
            var cp = base.CreateParams;
            cp.ClassStyle |= CS_DROPSHADOW;
            return cp;
        }
    }

    private void ApplyRoundedCorners()
    {
        var radius = 16;
        using var path = CreateRoundedRectPath(new Rectangle(0, 0, Width, Height), radius);
        Region = new Region(path);
    }

    private static GraphicsPath CreateRoundedRectPath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();

        return path;
    }

    private static void EnableAcrylic(IntPtr handle, Color tintColor, byte opacity)
    {
        var accent = new AccentPolicy
        {
            AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
            GradientColor = (opacity << 24)
                | (tintColor.B << 16)
                | (tintColor.G << 8)
                | tintColor.R
        };

        var accentSize = Marshal.SizeOf(accent);
        var accentPtr = Marshal.AllocHGlobal(accentSize);
        Marshal.StructureToPtr(accent, accentPtr, false);

        var data = new WindowCompositionAttributeData
        {
            Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
            SizeOfData = accentSize,
            Data = accentPtr
        };

        SetWindowCompositionAttribute(handle, ref data);
        Marshal.FreeHGlobal(accentPtr);
    }

    private void ApplyRoundedCornerPreference()
    {
        try
        {
            var preference = DwmWindowCornerPreference.DWMWCP_ROUND;
            DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(uint));
        }
        catch
        {
            // Ignore if running on older Windows builds.
        }
    }

    private enum AccentState
    {
        ACCENT_DISABLED = 0,
        ACCENT_ENABLE_GRADIENT = 1,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
        ACCENT_ENABLE_BLURBEHIND = 3,
        ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
        ACCENT_ENABLE_HOSTBACKDROP = 5,
        ACCENT_INVALID_STATE = 6
    }

    private enum WindowCompositionAttribute
    {
        WCA_ACCENT_POLICY = 19
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public WindowCompositionAttribute Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;

    private enum DwmWindowCornerPreference
    {
        DWMWCP_DEFAULT = 0,
        DWMWCP_DONOTROUND = 1,
        DWMWCP_ROUND = 2,
        DWMWCP_ROUNDSMALL = 3
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref DwmWindowCornerPreference attrValue, int attrSize);
}
