using Microsoft.Data.Sqlite;

namespace WinBox.Search.Index;

/// <summary>
/// SQLite persistence for filename-index entries. Query hot path stays in <see cref="InMemoryFileIndex"/>.
/// </summary>
public sealed class SqliteFileIndexStore : IDisposable
{
    public const string DatabaseFileName = "files.db";
    public const int SchemaVersion = 1;

    public const string MetaSchemaVersion = "schema_version";
    public const string MetaOptionsFingerprint = "options_fingerprint";
    public const string MetaBuiltAtUtc = "built_at_utc";
    public const string MetaUsnJournalId = "usn_journal_id";
    public const string MetaUsnNextUsn = "usn_next_usn";
    public const string MetaUsnVolume = "usn_volume";

    private readonly object _gate = new();
    private SqliteConnection? _connection;
    private string? _directory;

    public string? DirectoryPath => _directory;

    public string DatabasePath =>
        _directory is null
            ? string.Empty
            : Path.Combine(_directory, DatabaseFileName);

    public bool IsOpen => _connection is not null;

    public void Open(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Index store directory is required.", nameof(directory));
        }

        var resolved = Path.GetFullPath(directory.Trim());
        lock (_gate)
        {
            if (_connection is not null
                && _directory is not null
                && _directory.Equals(resolved, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            CloseUnlocked();
            Directory.CreateDirectory(resolved);
            var dbPath = Path.Combine(resolved, DatabaseFileName);
            var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
            }.ToString());
            connection.Open();
            ApplyPragmas(connection);
            EnsureSchema(connection);
            _connection = connection;
            _directory = resolved;
        }
    }

    public bool TryOpen(string directory)
    {
        try
        {
            Open(directory);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException or ArgumentException)
        {
            return false;
        }
    }

    public IReadOnlyList<FileIndexEntry> LoadAll()
    {
        lock (_gate)
        {
            EnsureOpen();
            var list = new List<FileIndexEntry>();
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText =
                """
                SELECT full_path, file_name, extension, last_write_utc, last_access_utc, file_ref
                FROM entries;
                """;
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new FileIndexEntry(
                    FullPath: reader.GetString(0),
                    FileName: reader.GetString(1),
                    Extension: reader.GetString(2),
                    LastWriteTimeUtc: ParseUtc(reader.GetString(3)),
                    LastAccessTimeUtc: ParseUtc(reader.GetString(4)),
                    FileReferenceNumber: reader.IsDBNull(5) ? 0UL : (ulong)reader.GetInt64(5)));
            }

            return list;
        }
    }

    public bool TryFindByFrn(ulong frn, out FileIndexEntry entry)
    {
        entry = null!;
        if (frn == 0)
        {
            return false;
        }

        lock (_gate)
        {
            EnsureOpen();
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText =
                """
                SELECT full_path, file_name, extension, last_write_utc, last_access_utc, file_ref
                FROM entries
                WHERE file_ref = $frn
                LIMIT 1;
                """;
            cmd.Parameters.AddWithValue("$frn", unchecked((long)frn));
            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return false;
            }

            entry = new FileIndexEntry(
                FullPath: reader.GetString(0),
                FileName: reader.GetString(1),
                Extension: reader.GetString(2),
                LastWriteTimeUtc: ParseUtc(reader.GetString(3)),
                LastAccessTimeUtc: ParseUtc(reader.GetString(4)),
                FileReferenceNumber: reader.IsDBNull(5) ? 0UL : (ulong)reader.GetInt64(5));
            return true;
        }
    }

    public int CountEntries()
    {
        lock (_gate)
        {
            EnsureOpen();
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM entries;";
            var result = cmd.ExecuteScalar();
            return result is long l ? (int)Math.Min(l, int.MaxValue) : Convert.ToInt32(result);
        }
    }

    /// <summary>Hottest rows for seeding a bounded memory cache (access time, then write time).</summary>
    public IReadOnlyList<FileIndexEntry> LoadHottest(int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        lock (_gate)
        {
            EnsureOpen();
            var list = new List<FileIndexEntry>();
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText =
                """
                SELECT full_path, file_name, extension, last_write_utc, last_access_utc, file_ref
                FROM entries
                ORDER BY last_access_utc DESC, last_write_utc DESC
                LIMIT $limit;
                """;
            cmd.Parameters.AddWithValue("$limit", limit);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new FileIndexEntry(
                    FullPath: reader.GetString(0),
                    FileName: reader.GetString(1),
                    Extension: reader.GetString(2),
                    LastWriteTimeUtc: ParseUtc(reader.GetString(3)),
                    LastAccessTimeUtc: ParseUtc(reader.GetString(4)),
                    FileReferenceNumber: reader.IsDBNull(5) ? 0UL : (ulong)reader.GetInt64(5)));
            }

            return list;
        }
    }

    /// <summary>
    /// Candidate rows for store-backed search. Over-fetches for in-process ranking
    /// (<paramref name="fetchLimit"/>), then caller applies ranking.
    /// </summary>
    public IReadOnlyList<FileIndexEntry> QueryCandidates(
        string? text,
        IReadOnlyList<string>? extensions,
        DateTime? modifiedAfterUtc,
        DateTime? rarelyUsedBeforeUtc,
        int fetchLimit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fetchLimit);

        lock (_gate)
        {
            EnsureOpen();
            var sql = new System.Text.StringBuilder(
                """
                SELECT full_path, file_name, extension, last_write_utc, last_access_utc, file_ref
                FROM entries
                WHERE 1=1
                """);

            using var cmd = _connection!.CreateCommand();

            if (!string.IsNullOrWhiteSpace(text))
            {
                sql.Append(" AND (file_name LIKE $q ESCAPE '\\' OR full_path LIKE $q ESCAPE '\\')");
                var pattern = "%" + EscapeLike(text.Trim()) + "%";
                cmd.Parameters.AddWithValue("$q", pattern);
            }

            if (extensions is { Count: > 0 })
            {
                sql.Append(" AND lower(extension) IN (");
                for (var i = 0; i < extensions.Count; i++)
                {
                    if (i > 0)
                    {
                        sql.Append(',');
                    }

                    var name = "$e" + i;
                    sql.Append(name);
                    cmd.Parameters.AddWithValue(name, IndexPolicy.NormalizeExtension(extensions[i]).ToLowerInvariant());
                }

                sql.Append(')');
            }

            if (modifiedAfterUtc is not null)
            {
                sql.Append(" AND last_write_utc >= $mtime");
                cmd.Parameters.AddWithValue("$mtime", FormatUtc(modifiedAfterUtc.Value));
            }

            if (rarelyUsedBeforeUtc is not null)
            {
                sql.Append(" AND last_access_utc < $rare");
                cmd.Parameters.AddWithValue("$rare", FormatUtc(rarelyUsedBeforeUtc.Value));
            }

            sql.Append(" ORDER BY last_write_utc DESC LIMIT $limit;");
            cmd.Parameters.AddWithValue("$limit", fetchLimit);
            cmd.CommandText = sql.ToString();

            var list = new List<FileIndexEntry>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new FileIndexEntry(
                    FullPath: reader.GetString(0),
                    FileName: reader.GetString(1),
                    Extension: reader.GetString(2),
                    LastWriteTimeUtc: ParseUtc(reader.GetString(3)),
                    LastAccessTimeUtc: ParseUtc(reader.GetString(4)),
                    FileReferenceNumber: reader.IsDBNull(5) ? 0UL : (ulong)reader.GetInt64(5)));
            }

            return list;
        }
    }

    private static string EscapeLike(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    public void ReplaceAll(IEnumerable<FileIndexEntry> entries, string optionsFingerprint)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(optionsFingerprint);

        lock (_gate)
        {
            EnsureOpen();
            using var tx = _connection!.BeginTransaction();
            using (var clear = _connection.CreateCommand())
            {
                clear.Transaction = tx;
                clear.CommandText = "DELETE FROM entries;";
                clear.ExecuteNonQuery();
            }

            using (var insert = _connection.CreateCommand())
            {
                insert.Transaction = tx;
                insert.CommandText =
                    """
                    INSERT INTO entries (full_path, file_name, extension, last_write_utc, last_access_utc, file_ref)
                    VALUES ($path, $name, $ext, $mtime, $atime, $frn);
                    """;
                var pPath = insert.Parameters.Add("$path", SqliteType.Text);
                var pName = insert.Parameters.Add("$name", SqliteType.Text);
                var pExt = insert.Parameters.Add("$ext", SqliteType.Text);
                var pMtime = insert.Parameters.Add("$mtime", SqliteType.Text);
                var pAtime = insert.Parameters.Add("$atime", SqliteType.Text);
                var pFrn = insert.Parameters.Add("$frn", SqliteType.Integer);

                foreach (var entry in entries)
                {
                    if (entry is null || string.IsNullOrWhiteSpace(entry.FullPath))
                    {
                        continue;
                    }

                    pPath.Value = entry.FullPath.Trim();
                    pName.Value = entry.FileName;
                    pExt.Value = entry.Extension ?? string.Empty;
                    pMtime.Value = FormatUtc(entry.LastWriteTimeUtc);
                    pAtime.Value = FormatUtc(entry.LastAccessTimeUtc);
                    pFrn.Value = unchecked((long)entry.FileReferenceNumber);
                    insert.ExecuteNonQuery();
                }
            }

            SetMetaUnlocked(tx, MetaOptionsFingerprint, optionsFingerprint);
            SetMetaUnlocked(tx, MetaBuiltAtUtc, FormatUtc(DateTime.UtcNow));
            SetMetaUnlocked(tx, MetaSchemaVersion, SchemaVersion.ToString());
            tx.Commit();
        }
    }

    public void Upsert(IEnumerable<FileIndexEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        lock (_gate)
        {
            EnsureOpen();
            using var tx = _connection!.BeginTransaction();
            using var cmd = _connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
                """
                INSERT INTO entries (full_path, file_name, extension, last_write_utc, last_access_utc, file_ref)
                VALUES ($path, $name, $ext, $mtime, $atime, $frn)
                ON CONFLICT(full_path) DO UPDATE SET
                  file_name = excluded.file_name,
                  extension = excluded.extension,
                  last_write_utc = excluded.last_write_utc,
                  last_access_utc = excluded.last_access_utc,
                  file_ref = excluded.file_ref;
                """;
            var pPath = cmd.Parameters.Add("$path", SqliteType.Text);
            var pName = cmd.Parameters.Add("$name", SqliteType.Text);
            var pExt = cmd.Parameters.Add("$ext", SqliteType.Text);
            var pMtime = cmd.Parameters.Add("$mtime", SqliteType.Text);
            var pAtime = cmd.Parameters.Add("$atime", SqliteType.Text);
            var pFrn = cmd.Parameters.Add("$frn", SqliteType.Integer);

            foreach (var entry in entries)
            {
                if (entry is null || string.IsNullOrWhiteSpace(entry.FullPath))
                {
                    continue;
                }

                pPath.Value = entry.FullPath.Trim();
                pName.Value = entry.FileName;
                pExt.Value = entry.Extension ?? string.Empty;
                pMtime.Value = FormatUtc(entry.LastWriteTimeUtc);
                pAtime.Value = FormatUtc(entry.LastAccessTimeUtc);
                pFrn.Value = unchecked((long)entry.FileReferenceNumber);
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }
    }

    public void Remove(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        lock (_gate)
        {
            EnsureOpen();
            using var tx = _connection!.BeginTransaction();
            using var cmd = _connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM entries WHERE full_path = $path COLLATE NOCASE;";
            var pPath = cmd.Parameters.Add("$path", SqliteType.Text);
            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                pPath.Value = path.Trim();
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }
    }

    public string? GetMeta(string key)
    {
        lock (_gate)
        {
            EnsureOpen();
            return GetMetaUnlocked(key);
        }
    }

    public void SetMeta(string key, string value)
    {
        lock (_gate)
        {
            EnsureOpen();
            using var tx = _connection!.BeginTransaction();
            SetMetaUnlocked(tx, key, value);
            tx.Commit();
        }
    }

    public bool TryGetUsnCursor(out string volume, out string journalId, out long nextUsn)
    {
        volume = GetMeta(MetaUsnVolume) ?? string.Empty;
        journalId = GetMeta(MetaUsnJournalId) ?? string.Empty;
        var nextRaw = GetMeta(MetaUsnNextUsn);
        if (string.IsNullOrEmpty(volume)
            || string.IsNullOrEmpty(journalId)
            || !long.TryParse(nextRaw, out nextUsn))
        {
            nextUsn = 0;
            return false;
        }

        return true;
    }

    public void SetUsnCursor(string volume, string journalId, long nextUsn)
    {
        lock (_gate)
        {
            EnsureOpen();
            using var tx = _connection!.BeginTransaction();
            SetMetaUnlocked(tx, MetaUsnVolume, volume);
            SetMetaUnlocked(tx, MetaUsnJournalId, journalId);
            SetMetaUnlocked(tx, MetaUsnNextUsn, nextUsn.ToString());
            tx.Commit();
        }
    }

    public void ClearUsnCursor()
    {
        lock (_gate)
        {
            EnsureOpen();
            using var tx = _connection!.BeginTransaction();
            DeleteMetaUnlocked(tx, MetaUsnVolume);
            DeleteMetaUnlocked(tx, MetaUsnJournalId);
            DeleteMetaUnlocked(tx, MetaUsnNextUsn);
            tx.Commit();
        }
    }

    public void Close()
    {
        lock (_gate)
        {
            CloseUnlocked();
        }
    }

    public void Dispose() => Close();

    private void EnsureOpen()
    {
        if (_connection is null)
        {
            throw new InvalidOperationException("Index store is not open.");
        }
    }

    private void CloseUnlocked()
    {
        _connection?.Dispose();
        _connection = null;
        _directory = null;
    }

    private string? GetMetaUnlocked(string key)
    {
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = "SELECT value FROM meta WHERE key = $key LIMIT 1;";
        cmd.Parameters.AddWithValue("$key", key);
        return cmd.ExecuteScalar() as string;
    }

    private void SetMetaUnlocked(SqliteTransaction tx, string key, string value)
    {
        using var cmd = _connection!.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            """
            INSERT INTO meta (key, value) VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
        cmd.ExecuteNonQuery();
    }

    private void DeleteMetaUnlocked(SqliteTransaction tx, string key)
    {
        using var cmd = _connection!.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "DELETE FROM meta WHERE key = $key;";
        cmd.Parameters.AddWithValue("$key", key);
        cmd.ExecuteNonQuery();
    }

    private static void ApplyPragmas(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
        cmd.ExecuteNonQuery();
    }

    private static void EnsureSchema(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS meta (
              key TEXT PRIMARY KEY NOT NULL,
              value TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS entries (
              full_path TEXT PRIMARY KEY COLLATE NOCASE NOT NULL,
              file_name TEXT NOT NULL,
              extension TEXT NOT NULL,
              last_write_utc TEXT NOT NULL,
              last_access_utc TEXT NOT NULL,
              file_ref INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS ix_entries_file_name ON entries(file_name);
            CREATE INDEX IF NOT EXISTS ix_entries_file_ref ON entries(file_ref);
            """;
        cmd.ExecuteNonQuery();

        // Migrate older DBs created before file_ref existed.
        using (var migrate = connection.CreateCommand())
        {
            migrate.CommandText = "PRAGMA table_info(entries);";
            using var reader = migrate.ExecuteReader();
            var hasFrn = false;
            while (reader.Read())
            {
                if (reader.GetString(1).Equals("file_ref", StringComparison.OrdinalIgnoreCase))
                {
                    hasFrn = true;
                    break;
                }
            }

            reader.Close();
            if (!hasFrn)
            {
                using var alter = connection.CreateCommand();
                alter.CommandText = "ALTER TABLE entries ADD COLUMN file_ref INTEGER NOT NULL DEFAULT 0;";
                alter.ExecuteNonQuery();
            }
        }

        using var versionCmd = connection.CreateCommand();
        versionCmd.CommandText =
            """
            INSERT INTO meta (key, value) VALUES ($key, $value)
            ON CONFLICT(key) DO NOTHING;
            """;
        versionCmd.Parameters.AddWithValue("$key", MetaSchemaVersion);
        versionCmd.Parameters.AddWithValue("$value", SchemaVersion.ToString());
        versionCmd.ExecuteNonQuery();
    }

    private static string FormatUtc(DateTime value) =>
        (value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime()).ToString("O");

    private static DateTime ParseUtc(string raw) =>
        DateTime.TryParse(
            raw,
            null,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out var parsed)
            ? (parsed.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
                : parsed.ToUniversalTime())
            : DateTime.MinValue;
}
