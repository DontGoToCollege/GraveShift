using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GraveShift.Core;

static class Native
{
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] static extern IntPtr CreateDC(string? driver, string device, string? output, IntPtr init);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool SetDeviceGammaRamp(IntPtr dc, ushort[] ramp);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out Rect r);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder s, int max);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr mon, ref MonitorInfo info);
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    [StructLayout(LayoutKind.Sequential)] struct Rect { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }

    /// <summary>Gives every display the same ramp. False if Windows refused it on any display.</summary>
    public static bool ApplyRamp(ushort[] ramp)
    {
        bool ok = true;
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            var dc = CreateDC(null, screen.DeviceName, null, IntPtr.Zero);
            if (dc == IntPtr.Zero) { ok = false; continue; }
            ok &= SetDeviceGammaRamp(dc, ramp);
            DeleteDC(dc);
        }
        return ok;
    }

    public static void RestoreRamp() => ApplyRamp(ScreenColor.Ramp(6500, 1, Effect.None));

    /// <summary>Process name of the window in front, or null for the desktop/taskbar or our own windows.</summary>
    public static string? ForegroundApp(out bool fullscreen)
    {
        fullscreen = false;
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;
        var cls = new System.Text.StringBuilder(64);
        GetClassName(hwnd, cls, 64);
        if (cls.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return null;
        GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == Environment.ProcessId) return null;

        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (GetWindowRect(hwnd, out var r) && GetMonitorInfo(MonitorFromWindow(hwnd, 2), ref info))
            fullscreen = r.L <= info.Monitor.L && r.T <= info.Monitor.T && r.R >= info.Monitor.R && r.B >= info.Monitor.B;
        try { return Process.GetProcessById((int)pid).ProcessName; }
        catch { return null; } // process already gone
    }
}
