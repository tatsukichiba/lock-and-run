using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace LockRun.Services;

public sealed class WindowsPowerGuard : IDisposable
{
    private const uint PowerRequestContextVersion = 0;
    private const uint PowerRequestContextSimpleString = 0x00000001;
    private const string PowerRequestReason =
        "Lock & Run is keeping monitored jobs active while the workstation is locked.";

    private SafeFileHandle? _powerRequestHandle;
    private bool _disposed;

    private enum PowerRequestType
    {
        SystemRequired = 0
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ReasonContext
    {
        public uint Version;

        public uint Flags;

        public nint SimpleReasonString;
    }

    public bool IsEnabled =>
        _powerRequestHandle is { IsInvalid: false, IsClosed: false };

    public void Enable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsEnabled)
        {
            return;
        }

        var reasonPointer = Marshal.StringToHGlobalUni(PowerRequestReason);
        SafeFileHandle requestHandle;

        try
        {
            var reasonContext = new ReasonContext
            {
                Version = PowerRequestContextVersion,
                Flags = PowerRequestContextSimpleString,
                SimpleReasonString = reasonPointer
            };

            requestHandle = PowerCreateRequest(ref reasonContext);
        }
        finally
        {
            Marshal.FreeHGlobal(reasonPointer);
        }

        if (requestHandle.IsInvalid)
        {
            var errorCode = Marshal.GetLastWin32Error();
            requestHandle.Dispose();
            throw new Win32Exception(
                errorCode,
                "Failed to create the Windows power request.");
        }

        if (!PowerSetRequest(requestHandle, PowerRequestType.SystemRequired))
        {
            var error = Marshal.GetLastWin32Error();
            requestHandle.Dispose();
            throw new Win32Exception(error, "Failed to enable sleep prevention.");
        }

        _powerRequestHandle = requestHandle;
    }

    public void Disable()
    {
        var requestHandle = _powerRequestHandle;
        _powerRequestHandle = null;

        if (requestHandle is null)
        {
            return;
        }

        try
        {
            if (!PowerClearRequest(requestHandle, PowerRequestType.SystemRequired))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Failed to disable sleep prevention.");
            }
        }
        finally
        {
            requestHandle.Dispose();
        }
    }

    public static void LockWorkStation()
    {
        if (!NativeLockWorkStation())
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to lock Windows.");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            Disable();
        }
        finally
        {
            _disposed = true;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeFileHandle PowerCreateRequest(ref ReasonContext context);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PowerSetRequest(
        SafeFileHandle powerRequest,
        PowerRequestType requestType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PowerClearRequest(
        SafeFileHandle powerRequest,
        PowerRequestType requestType);

    [DllImport("user32.dll", EntryPoint = "LockWorkStation", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool NativeLockWorkStation();
}
