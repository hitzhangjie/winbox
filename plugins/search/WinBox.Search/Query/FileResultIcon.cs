using WinBox.Abstractions;

namespace WinBox.Search.Query;

/// <summary>
/// Maps file paths / extensions to <see cref="ResultIconKeys"/> for launcher scanability.
/// Host renders the glyph; this type stays free of UI dependencies.
/// </summary>
public static class FileResultIcon
{
    private static readonly HashSet<string> Markdown = NewSet("md", "mdx", "markdown");
    private static readonly HashSet<string> Document = NewSet(
        "txt", "rtf", "log", "doc", "docx", "odt", "pages", "tex", "rst", "org");
    private static readonly HashSet<string> Pdf = NewSet("pdf");
    private static readonly HashSet<string> Spreadsheet = NewSet(
        "xls", "xlsx", "csv", "tsv", "ods", "numbers");
    private static readonly HashSet<string> Presentation = NewSet(
        "ppt", "pptx", "odp", "key");
    private static readonly HashSet<string> Image = NewSet(
        "png", "jpg", "jpeg", "gif", "webp", "svg", "bmp", "ico", "tif", "tiff", "heic", "avif");
    private static readonly HashSet<string> Audio = NewSet(
        "mp3", "wav", "flac", "m4a", "aac", "ogg", "wma", "opus");
    private static readonly HashSet<string> Video = NewSet(
        "mp4", "mkv", "avi", "mov", "webm", "wmv", "m4v", "flv");
    private static readonly HashSet<string> Archive = NewSet(
        "zip", "rar", "7z", "tar", "gz", "tgz", "bz2", "xz", "cab");
    private static readonly HashSet<string> Executable = NewSet(
        "exe", "msi", "bat", "cmd", "ps1", "com", "scr", "lnk");
    private static readonly HashSet<string> Code = NewSet(
        "cs", "fs", "vb", "go", "rs", "c", "h", "cpp", "hpp", "cc", "cxx",
        "java", "kt", "kts", "scala", "swift", "m", "mm",
        "js", "jsx", "ts", "tsx", "mjs", "cjs",
        "py", "rb", "php", "lua", "r", "pl", "pm",
        "html", "htm", "css", "scss", "less", "sass",
        "json", "jsonc", "xml", "yml", "yaml", "toml", "ini", "cfg", "conf",
        "sql", "sh", "bash", "zsh", "fish",
        "proto", "graphql", "gql", "dockerfile",
        "cmake", "makefile", "gradle", "sln", "csproj", "fsproj", "vbproj",
        "ipynb");

    public static string FromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ResultIconKeys.File;
        }

        // Trailing separator → directory-like path.
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (trimmed.Length < path.Length && trimmed.Length > 0)
        {
            return ResultIconKeys.Folder;
        }

        var extension = Path.GetExtension(path);
        if (extension.Length > 1 && extension[0] == '.')
        {
            extension = extension[1..];
        }

        return FromExtension(extension);
    }

    public static string FromExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return ResultIconKeys.File;
        }

        var ext = extension.Trim().TrimStart('.').ToLowerInvariant();
        if (ext.Length == 0)
        {
            return ResultIconKeys.File;
        }

        if (Markdown.Contains(ext))
        {
            return ResultIconKeys.Markdown;
        }

        if (Pdf.Contains(ext))
        {
            return ResultIconKeys.Pdf;
        }

        if (Spreadsheet.Contains(ext))
        {
            return ResultIconKeys.Spreadsheet;
        }

        if (Presentation.Contains(ext))
        {
            return ResultIconKeys.Presentation;
        }

        if (Image.Contains(ext))
        {
            return ResultIconKeys.Image;
        }

        if (Audio.Contains(ext))
        {
            return ResultIconKeys.Audio;
        }

        if (Video.Contains(ext))
        {
            return ResultIconKeys.Video;
        }

        if (Archive.Contains(ext))
        {
            return ResultIconKeys.Archive;
        }

        if (Executable.Contains(ext))
        {
            return ResultIconKeys.Executable;
        }

        if (Code.Contains(ext))
        {
            return ResultIconKeys.Code;
        }

        if (Document.Contains(ext))
        {
            return ResultIconKeys.Document;
        }

        return ResultIconKeys.File;
    }

    private static HashSet<string> NewSet(params string[] extensions) =>
        new(extensions, StringComparer.Ordinal);
}
