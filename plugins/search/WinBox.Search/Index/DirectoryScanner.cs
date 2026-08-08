namespace WinBox.Search.Index;

/// <summary>
/// Recursively scans configured roots into filename-index entries.
/// MVP: does not follow reparse points (symlinks / junctions).
/// </summary>
public sealed class DirectoryScanner
{
    private static readonly EnumerationOptions FileEnumOptions = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
        ReturnSpecialDirectories = false,
    };

    public IReadOnlyList<FileIndexEntry> Scan(
        IndexOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var policy = new IndexPolicy(options);
        var results = new List<FileIndexEntry>();

        foreach (var root in options.Roots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            var trimmed = root.Trim();
            if (!Directory.Exists(trimmed))
            {
                continue;
            }

            ScanDirectory(trimmed, options.Recursive, policy, results, cancellationToken);
        }

        return results;
    }

    private static void ScanDirectory(
        string directoryPath,
        bool recursive,
        IndexPolicy policy,
        List<FileIndexEntry> results,
        CancellationToken cancellationToken)
    {
        if (!policy.ShouldEnterDirectory(directoryPath))
        {
            return;
        }

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directoryPath, "*", FileEnumOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!policy.ShouldIncludeFile(file))
            {
                continue;
            }

            if (TryCreateEntry(file, out var entry))
            {
                results.Add(entry);
            }
        }

        if (!recursive)
        {
            return;
        }

        IEnumerable<string> children;
        try
        {
            children = Directory.EnumerateDirectories(directoryPath, "*", FileEnumOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var child in children)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ScanDirectory(child, recursive: true, policy, results, cancellationToken);
        }
    }

    private static bool TryCreateEntry(string fullPath, out FileIndexEntry entry)
    {
        try
        {
            var info = new FileInfo(fullPath);
            if (!info.Exists)
            {
                entry = null!;
                return false;
            }

            var fileName = info.Name;
            var extension = IndexPolicy.NormalizeExtension(info.Extension);
            entry = new FileIndexEntry(
                FullPath: info.FullName,
                FileName: fileName,
                Extension: extension,
                LastWriteTimeUtc: info.LastWriteTimeUtc,
                LastAccessTimeUtc: info.LastAccessTimeUtc,
                FileReferenceNumber: FileReferenceNumber.TryRead(info.FullName));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            entry = null!;
            return false;
        }
    }
}
