using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace SnapCraft;

internal static class NativeInput
{
    [StructLayout(LayoutKind.Sequential)] private struct PointNative { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] private struct RectNative { public int Left; public int Top; public int Right; public int Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput
    {
        public int Dx, Dy;
        public uint MouseData, Flags, Time;
        public IntPtr ExtraInfo;
    }
    [StructLayout(LayoutKind.Explicit, Size = 40)] private struct Input
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public MouseInput Mouse;
    }

    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(PointNative point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr handle, uint flags);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr handle, out RectNative rectangle);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr handle, out RectNative rectangle);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr handle, ref PointNative point);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr handle, int attribute, out RectNative value, int size);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint SendInput(uint count, ref Input input, int size);
    private delegate bool EnumWindowProc(IntPtr handle, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowProc callback, IntPtr data);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr handle);

    public static IntPtr WindowBelow(Point point, IntPtr excluded)
    {
        var found = IntPtr.Zero;
        EnumWindows((handle, _) =>
        {
            if (handle == excluded || !IsWindowVisible(handle) || !GetWindowRect(handle, out var rect)) return true;
            if (point.X < rect.Left || point.X >= rect.Right || point.Y < rect.Top || point.Y >= rect.Bottom) return true;
            found = handle;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    public static IntPtr RootWindowAt(Point point) => GetAncestor(WindowFromPoint(new PointNative { X = point.X, Y = point.Y }), 2);

    public static Rectangle WindowBounds(IntPtr handle)
    {
        if (!GetWindowRect(handle, out var rect)) throw new InvalidOperationException("อ่านขอบหน้าต่างไม่ได้");
        return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    public static Rectangle ClientBounds(IntPtr handle)
    {
        if (!GetClientRect(handle, out var rect)) return VisibleWindowBounds(handle);
        var point = new PointNative();
        if (!ClientToScreen(handle, ref point)) return VisibleWindowBounds(handle);
        return new Rectangle(point.X, point.Y, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    public static Rectangle VisibleWindowBounds(IntPtr handle)
    {
        if (DwmGetWindowAttribute(handle, 9, out var rect, Marshal.SizeOf<RectNative>()) == 0)
            return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        return WindowBounds(handle);
    }

    public static bool TryAutomationScroll(Point point)
    {
        try
        {
            var element = AutomationElement.FromPoint(new System.Windows.Point(point.X, point.Y));
            var walker = TreeWalker.ControlViewWalker;
            for (var depth = 0; element is not null && depth < 12; depth++, element = walker.GetParent(element))
            {
                if (!element.TryGetCurrentPattern(ScrollPattern.Pattern, out var pattern)) continue;
                var scroll = (ScrollPattern)pattern;
                if (!scroll.Current.VerticallyScrollable || scroll.Current.VerticalScrollPercent >= 99.5) continue;
                scroll.Scroll(ScrollAmount.NoAmount, ScrollAmount.LargeIncrement);
                return true;
            }
        }
        catch (Exception error) when (error is ElementNotAvailableException or InvalidOperationException or COMException) { }
        return false;
    }

    public static void FocusWindow(IntPtr handle)
    {
        if (handle != IntPtr.Zero && GetForegroundWindow() != handle) SetForegroundWindow(handle);
    }


    public static void WheelDown(Point point, int notches = 5)
    {
        SetCursorPos(point.X, point.Y);
        var input = new Input { Type = 0, Mouse = new MouseInput { MouseData = unchecked((uint)(-120 * notches)), Flags = 0x0800 } };
        if (SendInput(1, ref input, Marshal.SizeOf<Input>()) != 1)
            throw new InvalidOperationException("Windows ไม่ยอมรับคำสั่งเลื่อน โปรดลองเลื่อนเอง");
    }
}

internal sealed class EscapeHook : IDisposable
{
    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint threadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern IntPtr GetModuleHandle(string? module);
    private readonly HookProc callback;
    private readonly Action onEscape;
    private IntPtr hook;

    public EscapeHook(Action onEscape)
    {
        this.onEscape = onEscape;
        callback = HandleKey;
        hook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
        if (hook == IntPtr.Zero) throw new InvalidOperationException("เปิดปุ่ม Esc สำหรับหยุดจับภาพไม่ได้");
    }

    private IntPtr HandleKey(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && (wParam == (IntPtr)0x100 || wParam == (IntPtr)0x104) && Marshal.ReadInt32(lParam) == 0x1B)
        {
            onEscape();
            return (IntPtr)1;
        }
        return CallNextHookEx(hook, code, wParam, lParam);
    }

    public void Dispose()
    {
        if (hook == IntPtr.Zero) return;
        UnhookWindowsHookEx(hook);
        hook = IntPtr.Zero;
    }
}
