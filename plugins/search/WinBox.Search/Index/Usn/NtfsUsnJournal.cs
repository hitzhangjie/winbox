using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace WinBox.Search.Index.Usn;

/// <summary>
/// NTFS USN Journal reader via DeviceIoControl. Requires privileges that allow journal query/read.
/// Failures return false so callers can fall back to Watcher / full rebuild.
/// </summary>
public sealed class NtfsUsnJournal : IUsnJournal
{
    private const uint FileReadData = 0x0001;
    private const uint FileListDirectory = 0x0001;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;

    private const uint FsctlQueryUsnJournal = 0x000900f4;
    private const uint FsctlReadUsnJournal = 0x000900bb;

    private const uint UsnReasonFileCreate = 0x00000100;
    private const uint UsnReasonFileDelete = 0x00000200;
    private const uint UsnReasonRenameOldName = 0x00001000;
    private const uint UsnReasonRenameNewName = 0x00002000;
    private const uint UsnReasonDataExtend = 0x00000002;
    private const uint UsnReasonDataOverwrite = 0x00000001;
    private const uint UsnReasonBasicInfoChange = 0x00008000;
    private const uint UsnReasonClose = 0x80000000;

    private SafeFileHandle? _volumeHandle;
    private string? _volumeRoot;

    public bool TryOpen(string anyPathOnVolume, out UsnJournalState state, out string? error)
    {
        Close();
        state = default!;
        error = null;

        try
        {
            var full = Path.GetFullPath(anyPathOnVolume);
            var root = Path.GetPathRoot(full);
            if (string.IsNullOrEmpty(root))
            {
                error = "Could not resolve volume root.";
                return false;
            }

            var drive = root.TrimEnd('\\', '/');
            if (drive.Length == 2 && drive[1] == ':')
            {
                // \\.\C:
                var volumePath = @"\\.\" + drive;
                _volumeHandle = CreateFile(
                    volumePath,
                    FileReadData | FileListDirectory,
                    FileShareRead | FileShareWrite,
                    IntPtr.Zero,
                    OpenExisting,
                    FileFlagBackupSemantics,
                    IntPtr.Zero);

                if (_volumeHandle.IsInvalid)
                {
                    error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                    _volumeHandle.Dispose();
                    _volumeHandle = null;
                    return false;
                }

                if (!TryQueryJournal(_volumeHandle, out var journalId, out var nextUsn, out error))
                {
                    Close();
                    return false;
                }

                _volumeRoot = root.EndsWith('\\') || root.EndsWith('/')
                    ? root
                    : root + Path.DirectorySeparatorChar;
                state = new UsnJournalState(_volumeRoot, journalId, nextUsn);
                return true;
            }

            error = "USN journal requires a drive-letter volume.";
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            error = ex.Message;
            Close();
            return false;
        }
    }

    public bool TryReadChanges(
        UsnJournalState state,
        out IReadOnlyList<UsnChange> changes,
        out UsnJournalState nextState,
        out string? error)
    {
        changes = [];
        nextState = state;
        error = null;

        if (_volumeHandle is null || _volumeHandle.IsInvalid || _volumeRoot is null)
        {
            error = "USN journal is not open.";
            return false;
        }

        if (!string.Equals(state.VolumeRoot, _volumeRoot, StringComparison.OrdinalIgnoreCase))
        {
            error = "Volume mismatch.";
            return false;
        }

        if (!TryQueryJournal(_volumeHandle, out var journalId, out _, out error))
        {
            return false;
        }

        if (journalId != state.JournalId)
        {
            error = "USN journal id changed.";
            return false;
        }

        var buffer = new byte[64 * 1024];
        var readData = new ReadUsnJournalDataV0
        {
            StartUsn = state.NextUsn,
            ReasonMask = UsnReasonFileCreate
                | UsnReasonFileDelete
                | UsnReasonRenameOldName
                | UsnReasonRenameNewName
                | UsnReasonDataExtend
                | UsnReasonDataOverwrite
                | UsnReasonBasicInfoChange
                | UsnReasonClose,
            ReturnOnlyOnClose = 0,
            Timeout = 0,
            BytesToWaitFor = 0,
            UsnJournalId = state.JournalId,
        };

        var readSize = Marshal.SizeOf<ReadUsnJournalDataV0>();
        var readPtr = Marshal.AllocHGlobal(readSize);
        try
        {
            Marshal.StructureToPtr(readData, readPtr, false);
            if (!DeviceIoControl(
                    _volumeHandle,
                    FsctlReadUsnJournal,
                    readPtr,
                    (uint)readSize,
                    buffer,
                    (uint)buffer.Length,
                    out var bytesReturned,
                    IntPtr.Zero))
            {
                var code = Marshal.GetLastWin32Error();
                // ERROR_HANDLE_EOF (38) / ERROR_NO_MORE_ITEMS-ish: no new records
                if (code is 38 or 0)
                {
                    changes = [];
                    nextState = state;
                    error = null;
                    return true;
                }

                error = new Win32Exception(code).Message;
                return false;
            }

            if (bytesReturned < sizeof(long))
            {
                changes = [];
                nextState = state;
                return true;
            }

            var nextUsn = BitConverter.ToInt64(buffer, 0);
            var list = new List<UsnChange>();
            var offset = sizeof(long);
            string? pendingRenameOld = null;

            while (offset + 8 < bytesReturned)
            {
                var recordLength = BitConverter.ToInt32(buffer, offset);
                if (recordLength <= 0 || offset + recordLength > bytesReturned)
                {
                    break;
                }

                var usn = BitConverter.ToInt64(buffer, offset + 8);
                var frn = BitConverter.ToUInt64(buffer, offset + 16);
                var reason = BitConverter.ToUInt32(buffer, offset + 40);
                var fileNameLength = BitConverter.ToUInt16(buffer, offset + 56);
                var fileNameOffset = BitConverter.ToUInt16(buffer, offset + 58);
                var name = Encoding.Unicode.GetString(buffer, offset + fileNameOffset, fileNameLength);

                if ((reason & UsnReasonRenameOldName) != 0)
                {
                    pendingRenameOld = name;
                }
                else if ((reason & UsnReasonRenameNewName) != 0)
                {
                    list.Add(new UsnChange(usn, UsnChangeReason.Rename, frn, name, pendingRenameOld));
                    pendingRenameOld = null;
                }
                else if ((reason & UsnReasonFileDelete) != 0)
                {
                    list.Add(new UsnChange(usn, UsnChangeReason.Delete, frn, name, null));
                }
                else if ((reason & (UsnReasonFileCreate | UsnReasonDataExtend | UsnReasonDataOverwrite | UsnReasonBasicInfoChange)) != 0)
                {
                    list.Add(new UsnChange(usn, UsnChangeReason.CreateOrUpdate, frn, name, null));
                }

                offset += recordLength;
            }

            changes = list;
            nextState = state with { NextUsn = nextUsn };
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(readPtr);
        }
    }

    public void Close()
    {
        _volumeHandle?.Dispose();
        _volumeHandle = null;
        _volumeRoot = null;
    }

    private static bool TryQueryJournal(
        SafeFileHandle handle,
        out ulong journalId,
        out long nextUsn,
        out string? error)
    {
        journalId = 0;
        nextUsn = 0;
        error = null;

        var size = Marshal.SizeOf<UsnJournalData>();
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (!DeviceIoControl(
                    handle,
                    FsctlQueryUsnJournal,
                    IntPtr.Zero,
                    0,
                    buffer,
                    (uint)size,
                    out _,
                    IntPtr.Zero))
            {
                error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                return false;
            }

            var data = Marshal.PtrToStructure<UsnJournalData>(buffer);
            journalId = data.UsnJournalId;
            nextUsn = data.NextUsn;
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        IntPtr lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        byte[] lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    [StructLayout(LayoutKind.Sequential)]
    private struct UsnJournalData
    {
        public ulong UsnJournalId;
        public long FirstUsn;
        public long NextUsn;
        public long LowestValidUsn;
        public long MaxUsn;
        public ulong MaximumSize;
        public ulong AllocationDelta;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ReadUsnJournalDataV0
    {
        public long StartUsn;
        public uint ReasonMask;
        public uint ReturnOnlyOnClose;
        public ulong Timeout;
        public ulong BytesToWaitFor;
        public ulong UsnJournalId;
    }
}
