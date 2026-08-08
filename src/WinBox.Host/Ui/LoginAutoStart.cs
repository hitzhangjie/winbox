using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace WinBox.Host.Ui;

/// <summary>
/// Per-user login auto-start via HKCU Run. No elevation required.
/// </summary>
public sealed class LoginAutoStart
{
    public const string ValueName = "WinBox";

    private readonly ILoginAutoStartStore _store;
    private readonly Func<string> _commandFactory;

    public LoginAutoStart()
        : this(new RegistryLoginAutoStartStore(), BuildLaunchCommand)
    {
    }

    internal LoginAutoStart(ILoginAutoStartStore store, Func<string> commandFactory)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _commandFactory = commandFactory ?? throw new ArgumentNullException(nameof(commandFactory));
    }

    public bool IsEnabled()
    {
        var value = _store.Get(ValueName);
        return !string.IsNullOrWhiteSpace(value);
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            _store.Set(ValueName, _commandFactory());
            return;
        }

        _store.Delete(ValueName);
    }

    /// <summary>
    /// Syncs the Run key with the persisted preference (refreshes the command path when enabled).
    /// </summary>
    public void ApplyPreference(bool startWithWindows) => SetEnabled(startWithWindows);

    public static string BuildLaunchCommand()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            throw new InvalidOperationException("Cannot resolve the current process path for auto-start.");
        }

        var entry = Assembly.GetEntryAssembly()?.Location;
        return FormatLaunchCommand(processPath, entry);
    }

    /// <summary>
    /// Builds a Run-key command. When hosted by <c>dotnet</c> (UseAppHost=false), registers
    /// <c>dotnet "…\WinBox.Host.dll"</c>; otherwise registers the native process path.
    /// </summary>
    internal static string FormatLaunchCommand(string processPath, string? entryAssemblyLocation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processPath);

        var processFile = Path.GetFileName(processPath);
        var isDotnetHost = processFile.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase)
            || processFile.Equals("dotnet", StringComparison.OrdinalIgnoreCase);

        if (isDotnetHost)
        {
            if (string.IsNullOrWhiteSpace(entryAssemblyLocation))
            {
                throw new InvalidOperationException(
                    "Cannot register auto-start under the dotnet host without an entry assembly path.");
            }

            return Quote(processPath) + " " + Quote(entryAssemblyLocation);
        }

        return Quote(processPath);
    }

    private static string Quote(string path)
    {
        if (path.Contains('"', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Launch path must not contain double quotes.");
        }

        return "\"" + path + "\"";
    }
}

internal interface ILoginAutoStartStore
{
    string? Get(string name);

    void Set(string name, string value);

    void Delete(string name);
}

internal sealed class RegistryLoginAutoStartStore : ILoginAutoStartStore
{
    private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public string? Get(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunSubKey, writable: false);
        return key?.GetValue(name) as string;
    }

    public void Set(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        using var key = Registry.CurrentUser.CreateSubKey(RunSubKey, writable: true)
            ?? throw new InvalidOperationException("Cannot open HKCU\\…\\Run for auto-start.");
        key.SetValue(name, value);
    }

    public void Delete(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        using var key = Registry.CurrentUser.OpenSubKey(RunSubKey, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }
}
