using System.ComponentModel;
using System.Runtime.InteropServices;

namespace LockRun.Services;

public sealed class WindowsPowerGuard
{
    private bool _isEnabled;

    [Flags]
    private enum ExecutionState : uint
    {
        EsSystemRequired = 0x00000001,
        EsContinuous = 0x80000000
    }

    public bool IsEnabled => _isEnabled;

    public void Enable()
    {
        var result = SetThreadExecutionState(
            ExecutionState.EsContinuous |
            ExecutionState.EsSystemRequired);

        if (result == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to enable sleep prevention.");
        }

        _isEnabled = true;
    }

    public void Disable()
    {
        var result = SetThreadExecutionState(ExecutionState.EsContinuous);
        if (result == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to disable sleep prevention.");
        }

        _isEnabled = false;
    }

    public static void LockWorkStation()
    {
        if (!NativeLockWorkStation())
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to lock Windows.");
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SetThreadExecutionState(ExecutionState esFlags);

    [DllImport("user32.dll", EntryPoint = "LockWorkStation", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool NativeLockWorkStation();
}
