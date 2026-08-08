using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinBox.Host.Ui;

/// <summary>
/// Loads/saves <see cref="UiOptions"/> as JSON. Path is injectable for tests.
/// </summary>
public sealed class UiOptionsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _filePath;

    public UiOptionsStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Config path is required.", nameof(filePath));
        }

        _filePath = filePath;
    }

    public string FilePath => _filePath;

    public static string DefaultFilePath =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinBox",
            "ui-options.json");

    public UiOptions LoadOrDefault()
    {
        if (!File.Exists(_filePath))
        {
            return Normalize(new UiOptions());
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var options = JsonSerializer.Deserialize<UiOptions>(json, JsonOptions);
            return Normalize(options ?? new UiOptions());
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return Normalize(new UiOptions());
        }
    }

    public void Save(UiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var directory = System.IO.Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(Normalize(options), JsonOptions);
        File.WriteAllText(_filePath, json);
    }

    public static UiOptions Normalize(UiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new UiOptions
        {
            OverlayLeft = SanitizeCoord(options.OverlayLeft),
            OverlayTop = SanitizeCoord(options.OverlayTop),
            OverlayWidth = Clamp(options.OverlayWidth, 400, 900, WinBoxTheme.OverlayWidth),
            ResultsMaxHeight = Clamp(options.ResultsMaxHeight, 160, 560, WinBoxTheme.ResultsMaxHeight),
            FontInput = Clamp(options.FontInput, 14, 28, WinBoxTheme.FontInput),
            FontTitle = Clamp(options.FontTitle, 11, 20, WinBoxTheme.FontTitle),
            FontSubtitle = Clamp(options.FontSubtitle, 10, 16, WinBoxTheme.FontSubtitle),
            ScrollBarWidth = Clamp(options.ScrollBarWidth, 4, 16, 8),
            ScrollBarMode = UiLayout.ToStorage(UiLayout.ParseScrollBarMode(options.ScrollBarMode)),
            Theme = WinBoxTheme.ToStorage(WinBoxTheme.ParseTheme(options.Theme)),
            StartWithWindows = options.StartWithWindows,
            FileDialogAssistEnabled = options.FileDialogAssistEnabled,
            LauncherHotkey = LauncherHotkeyBinding.Normalize(options.LauncherHotkey),
        };
    }

    private static double Clamp(double value, double min, double max, double fallback)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < min || value > max)
        {
            return fallback;
        }

        return value;
    }

    private static double? SanitizeCoord(double? value)
    {
        if (value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
        {
            return null;
        }

        return value;
    }
}
