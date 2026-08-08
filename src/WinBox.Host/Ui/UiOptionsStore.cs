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
            return new UiOptions();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var options = JsonSerializer.Deserialize<UiOptions>(json, JsonOptions);
            return Normalize(options ?? new UiOptions());
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new UiOptions();
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
        var width = options.OverlayWidth;
        if (double.IsNaN(width) || width < 400 || width > 900)
        {
            width = WinBoxTheme.OverlayWidth;
        }

        return new UiOptions
        {
            OverlayLeft = SanitizeCoord(options.OverlayLeft),
            OverlayTop = SanitizeCoord(options.OverlayTop),
            OverlayWidth = width,
        };
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
