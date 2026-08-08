using System.Diagnostics;

namespace WinBox.Search;

/// <summary>
/// Default shell activation via <see cref="Process.Start"/> / Explorer <c>/select</c>.
/// </summary>
public sealed class ProcessPathActivation : IPathActivation
{
    public void Open(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
        });
    }

    public void RevealInFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (File.Exists(path) || Directory.Exists(path))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "/select,\"" + path + "\"",
                UseShellExecute = true,
            });
            return;
        }

        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = directory,
            UseShellExecute = true,
        });
    }
}
