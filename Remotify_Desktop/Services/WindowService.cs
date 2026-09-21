using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using Remotify.Models;

namespace Remotify.Services;

public class WindowService
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const uint GW_OWNER = 4;

    private const uint WM_GETICON = 0x007F;
    private const int ICON_BIG = 1;
    private const int ICON_SMALL2 = 2;
    private const int GCLP_HICON = -14;

    public List<AppInfo> GetVisibleWindows()
    {
        var windows = new List<AppInfo>();

        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd))
                return true;

            var titleLength = GetWindowTextLength(hWnd);
            if (titleLength == 0)
                return true;

            var exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);

            // Skip tool windows unless they have WS_EX_APPWINDOW
            if ((exStyle & WS_EX_TOOLWINDOW) != 0 && (exStyle & WS_EX_APPWINDOW) == 0)
                return true;

            // Skip noactivate windows
            if ((exStyle & WS_EX_NOACTIVATE) != 0)
                return true;

            // Skip owned windows (child dialogs, popups)
            var owner = GetWindow(hWnd, GW_OWNER);
            if (owner != IntPtr.Zero && (exStyle & WS_EX_APPWINDOW) == 0)
                return true;

            var titleBuilder = new StringBuilder(titleLength + 1);
            GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
            var title = titleBuilder.ToString();

            GetWindowThreadProcessId(hWnd, out var pid);

            string processName;
            try
            {
                using var process = Process.GetProcessById((int)pid);
                processName = process.ProcessName;
            }
            catch
            {
                processName = "Unknown";
            }

            windows.Add(new AppInfo
            {
                Id = hWnd.ToString("X"),
                Title = title,
                ProcessName = processName,
                MayHaveUnsavedChanges = DetectUnsavedChanges(title)
            });

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    public byte[]? GetAppIcon(string id)
    {
        if (!TryParseHwnd(id, out var hWnd))
            return null;

        if (!IsWindowVisible(hWnd))
            return null;

        var hIcon = GetWindowIcon(hWnd);
        if (hIcon == IntPtr.Zero)
        {
            hIcon = GetProcessIcon(hWnd);
        }

        if (hIcon == IntPtr.Zero)
            return null;

        try
        {
            using var icon = Icon.FromHandle(hIcon);
            using var bitmap = icon.ToBitmap();
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static IntPtr GetWindowIcon(IntPtr hWnd)
    {
        var hIcon = SendMessage(hWnd, WM_GETICON, (IntPtr)ICON_BIG, IntPtr.Zero);
        if (hIcon != IntPtr.Zero)
            return hIcon;

        hIcon = SendMessage(hWnd, WM_GETICON, (IntPtr)ICON_SMALL2, IntPtr.Zero);
        if (hIcon != IntPtr.Zero)
            return hIcon;

        hIcon = GetClassLongPtr(hWnd, GCLP_HICON);
        return hIcon;
    }

    private static IntPtr GetProcessIcon(IntPtr hWnd)
    {
        GetWindowThreadProcessId(hWnd, out var pid);

        try
        {
            using var process = Process.GetProcessById((int)pid);
            var exePath = process.MainModule?.FileName;

            if (string.IsNullOrEmpty(exePath))
                return IntPtr.Zero;

            var icon = Icon.ExtractAssociatedIcon(exePath);
            return icon?.Handle ?? IntPtr.Zero;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private static bool TryParseHwnd(string id, out IntPtr hWnd)
    {
        hWnd = IntPtr.Zero;

        if (string.IsNullOrEmpty(id))
            return false;

        if (long.TryParse(id, System.Globalization.NumberStyles.HexNumber, null, out var value))
        {
            hWnd = (IntPtr)value;
            return true;
        }

        return false;
    }

    private static bool DetectUnsavedChanges(string title)
    {
        return title.StartsWith('*') || title.EndsWith('*');
    }
}
