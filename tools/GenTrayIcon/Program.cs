using WinBox.Host.Ui;

var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var outPath = Path.Combine(repoRoot, "src", "WinBox.Host", "Assets", "winbox.ico");
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
TrayIconFactory.WriteToFile(outPath);
Console.WriteLine($"Wrote {outPath} ({new FileInfo(outPath).Length} bytes)");
