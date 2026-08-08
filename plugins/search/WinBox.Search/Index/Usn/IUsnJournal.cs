namespace WinBox.Search.Index.Usn;

/// <summary>One USN journal record relevant to path indexing.</summary>
public sealed record UsnChange(
    long Usn,
    UsnChangeReason Reason,
    ulong FileReferenceNumber,
    string? FileName,
    string? OldFileName);

public enum UsnChangeReason
{
    CreateOrUpdate,
    Delete,
    Rename,
}

/// <summary>Cursor for resuming USN reads.</summary>
public sealed record UsnJournalState(string VolumeRoot, ulong JournalId, long NextUsn);

public interface IUsnJournal
{
    /// <summary>Open journal for the volume that contains <paramref name="anyPathOnVolume"/>.</summary>
    bool TryOpen(string anyPathOnVolume, out UsnJournalState state, out string? error);

    /// <summary>
    /// Read changes starting at <paramref name="state"/>.NextUsn.
    /// Returns false when journal id mismatch / lost (caller must rebuild).
    /// </summary>
    bool TryReadChanges(
        UsnJournalState state,
        out IReadOnlyList<UsnChange> changes,
        out UsnJournalState nextState,
        out string? error);

    void Close();
}

/// <summary>
/// Test double that replays a scripted sequence of changes.
/// </summary>
public sealed class FakeUsnJournal : IUsnJournal
{
    private readonly Queue<UsnChange> _changes;
    private UsnJournalState? _state;
    private readonly ulong _journalId;
    private bool _forceLost;

    public FakeUsnJournal(ulong journalId = 1, IEnumerable<UsnChange>? seed = null)
    {
        _journalId = journalId;
        _changes = new Queue<UsnChange>(seed ?? []);
    }

    public void Enqueue(params UsnChange[] changes)
    {
        foreach (var change in changes)
        {
            _changes.Enqueue(change);
        }
    }

    public void ForceJournalLost() => _forceLost = true;

    public bool TryOpen(string anyPathOnVolume, out UsnJournalState state, out string? error)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(anyPathOnVolume)) ?? anyPathOnVolume;
        _state = new UsnJournalState(root, _journalId, 0);
        state = _state;
        error = null;
        return true;
    }

    public bool TryReadChanges(
        UsnJournalState state,
        out IReadOnlyList<UsnChange> changes,
        out UsnJournalState nextState,
        out string? error)
    {
        if (_forceLost || state.JournalId != _journalId)
        {
            changes = [];
            nextState = state;
            error = "journal id mismatch";
            return false;
        }

        var batch = new List<UsnChange>();
        long next = state.NextUsn;
        while (_changes.Count > 0)
        {
            var item = _changes.Dequeue();
            if (item.Usn < state.NextUsn)
            {
                continue;
            }

            batch.Add(item);
            next = Math.Max(next, item.Usn + 1);
        }

        changes = batch;
        nextState = state with { NextUsn = next };
        _state = nextState;
        error = null;
        return true;
    }

    public void Close()
    {
        _state = null;
        _changes.Clear();
    }
}
