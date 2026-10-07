param([int]$X = 20, [int]$Y = 40)
$ErrorActionPreference = 'Stop'
# Metadata only: never collect window titles, document text, or screen images.
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class CaptureTargetInspection {
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    private delegate bool Callback(IntPtr window, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumWindows(Callback callback, IntPtr data);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder value, int length);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] public static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] public static extern IntPtr GetDesktopWindow();
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
    public static object Describe(IntPtr window, int x, int y) {
        var name = new StringBuilder(256); GetClassName(window, name, name.Capacity);
        uint process; GetWindowThreadProcessId(window, out process);
        Rect rect; GetWindowRect(window, out rect);
        int cloaked; var result = DwmGetWindowAttribute(window, 14, out cloaked, 4);
        return new { Handle=window.ToInt64(), Class=name.ToString(), Process=process,
            Visible=IsWindowVisible(window), Minimized=IsIconic(window), Cloaked=result == 0 ? cloaked : -1,
            Left=rect.Left, Top=rect.Top, Right=rect.Right, Bottom=rect.Bottom,
            ContainsProbe=x>=rect.Left && x<rect.Right && y>=rect.Top && y<rect.Bottom };
    }
    public static object[] Windows(int x, int y) {
        var result = new List<object>();
        EnumWindows((window, data) => { if (IsWindowVisible(window)) result.Add(Describe(window, x, y)); return true; }, IntPtr.Zero);
        return result.ToArray();
    }
}
'@
$point = New-Object CaptureTargetInspection+Point
$point.X = $X; $point.Y = $Y
$actual = [CaptureTargetInspection]::GetAncestor([CaptureTargetInspection]::WindowFromPoint($point), 2)
@{
    Probe = @{ X = $X; Y = $Y }
    Shell = [CaptureTargetInspection]::Describe([CaptureTargetInspection]::GetShellWindow(), $X, $Y)
    Desktop = [CaptureTargetInspection]::Describe([CaptureTargetInspection]::GetDesktopWindow(), $X, $Y)
    ActualPointTarget = [CaptureTargetInspection]::Describe($actual, $X, $Y)
    WindowsInZOrder = [CaptureTargetInspection]::Windows($X, $Y)
} | ConvertTo-Json -Depth 5
