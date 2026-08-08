namespace WinBox.Search.Index;

/// <summary>
/// Capacity helpers for the in-memory path cache.
/// Disk can hold multi‑GB indexes; RAM holds at most <see cref="IndexOptions.MaxInMemoryMegabytes"/>.
/// </summary>
public static class IndexMemoryBudget
{
    /// <summary>Rough bytes per entry (path + metadata + dictionary / LRU overhead).</summary>
    public const int EstimatedBytesPerEntry = 320;

    public static long EstimateBytes(int entryCount) =>
        Math.Max(0, (long)entryCount * EstimatedBytesPerEntry);

    public static double EstimateMegabytes(int entryCount) =>
        EstimateBytes(entryCount) / (1024.0 * 1024.0);

    /// <summary>
    /// Max entries that fit in the budget. ≤ 0 MB means unlimited.
    /// </summary>
    public static int MaxEntries(int maxInMemoryMegabytes)
    {
        if (maxInMemoryMegabytes <= 0)
        {
            return int.MaxValue;
        }

        var bytes = (long)maxInMemoryMegabytes * 1024L * 1024L;
        var count = bytes / EstimatedBytesPerEntry;
        return count <= 0 ? 1 : (int)Math.Min(count, int.MaxValue);
    }

    /// <summary>True when the entire persisted index fits in the memory budget.</summary>
    public static bool CanFullyReside(int entryCount, int maxInMemoryMegabytes) =>
        entryCount <= MaxEntries(maxInMemoryMegabytes);
}
