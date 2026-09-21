using System.Runtime.InteropServices;

namespace Remotify.Services;

public static class TrayPositionService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    public static (int X, int Y) GetWorkAreaBottomRight()
    {
        var taskbar = FindWindow("Shell_TrayWnd", null);
        if (taskbar != IntPtr.Zero && GetWindowRect(taskbar, out var taskbarRect))
        {
            return (taskbarRect.Right, taskbarRect.Top);
        }

        return (0, 0);
    }
}
