using WinBox.Host.Ui;

namespace WinBox.Host.Tests;

public sealed class LoginAutoStartTests
{
    [Fact]
    public void SetEnabled_WritesAndClearsRunValue()
    {
        var store = new MemoryLoginAutoStartStore();
        var autoStart = new LoginAutoStart(store, () => "\"C:\\Tools\\WinBox.Host.exe\"");

        Assert.False(autoStart.IsEnabled());

        autoStart.SetEnabled(true);

        Assert.True(autoStart.IsEnabled());
        Assert.Equal("\"C:\\Tools\\WinBox.Host.exe\"", store.Get(LoginAutoStart.ValueName));

        autoStart.SetEnabled(false);

        Assert.False(autoStart.IsEnabled());
        Assert.Null(store.Get(LoginAutoStart.ValueName));
    }

    [Fact]
    public void ApplyPreference_RefreshesCommandWhenEnabled()
    {
        var store = new MemoryLoginAutoStartStore();
        store.Set(LoginAutoStart.ValueName, "\"C:\\Old\\WinBox.Host.exe\"");
        var calls = 0;
        var autoStart = new LoginAutoStart(store, () =>
        {
            calls++;
            return "\"C:\\New\\WinBox.Host.exe\"";
        });

        autoStart.ApplyPreference(true);

        Assert.Equal(1, calls);
        Assert.Equal("\"C:\\New\\WinBox.Host.exe\"", store.Get(LoginAutoStart.ValueName));
    }

    [Theory]
    [InlineData(@"C:\Program Files\dotnet\dotnet.exe", @"D:\Github\winbox\src\WinBox.Host\bin\Debug\net8.0-windows\WinBox.Host.dll",
        "\"C:\\Program Files\\dotnet\\dotnet.exe\" \"D:\\Github\\winbox\\src\\WinBox.Host\\bin\\Debug\\net8.0-windows\\WinBox.Host.dll\"")]
    [InlineData(@"D:\Apps\WinBox.Host.exe", @"D:\Apps\WinBox.Host.dll", "\"D:\\Apps\\WinBox.Host.exe\"")]
    [InlineData(@"D:\Apps\WinBox.Host.exe", null, "\"D:\\Apps\\WinBox.Host.exe\"")]
    public void FormatLaunchCommand_UsesDotnetOnlyWhenHosted(
        string processPath,
        string? entryAssembly,
        string expected)
    {
        var command = LoginAutoStart.FormatLaunchCommand(processPath, entryAssembly);
        Assert.Equal(expected, command);
    }

    [Fact]
    public void FormatLaunchCommand_DotnetWithoutEntry_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            LoginAutoStart.FormatLaunchCommand(@"C:\dotnet\dotnet.exe", null));
    }

    [Fact]
    public void Save_Then_Load_RoundTripsStartWithWindows()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "winbox-ui-autostart-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new UiOptionsStore(path);
            store.Save(new UiOptions { StartWithWindows = true, Theme = "dark" });

            var loaded = store.LoadOrDefault();

            Assert.True(loaded.StartWithWindows);
        }
        finally
        {
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
    }

    private sealed class MemoryLoginAutoStartStore : ILoginAutoStartStore
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

        public string? Get(string name) =>
            _values.TryGetValue(name, out var value) ? value : null;

        public void Set(string name, string value) => _values[name] = value;

        public void Delete(string name) => _values.Remove(name);
    }
}
