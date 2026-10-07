using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using SnapCraft;

internal static class DesktopCaptureTest
{
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetDesktopWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string name, string? title);

    public static async Task RunAsync()
    {
        var shell = GetShellWindow();
        if (shell == IntPtr.Zero) throw new Exception("This desktop test requires the Windows shell.");
        if (!NativeInput.IsDesktopWindow(shell) || !NativeInput.IsDesktopWindow(GetDesktopWindow()) || NativeInput.IsDesktopWindow(IntPtr.Zero))
            throw new Exception("Shell/desktop classification is incorrect.");
        var taskbar = FindWindow("Shell_TrayWnd", null);
        if (taskbar != IntPtr.Zero && NativeInput.IsDesktopWindow(taskbar)) throw new Exception("Taskbar was classified as desktop.");
        var backend = new CaptureBackend();
        var screen = Screen.FromHandle(shell);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var clock = Stopwatch.StartNew();
            var path = await backend.CaptureWindowAsync(shell);
            try
            {
                using var image = new Bitmap(path);
                if (image.Size != screen.Bounds.Size) throw new Exception("Desktop capture must retain native monitor dimensions.");
                if (clock.ElapsedMilliseconds >= 2000) throw new Exception($"Desktop capture retried a GPU window source: {clock.ElapsedMilliseconds}ms.");
                Console.WriteLine($"desktop capture {attempt + 1}: {clock.ElapsedMilliseconds}ms, {image.Width}x{image.Height}, native resolution");
            }
            finally { File.Delete(path); }
        }
        foreach (var monitor in Screen.AllScreens)
        {
            var path = await backend.CaptureWindowAsync(shell, desktopRegion: monitor.Bounds);
            try
            {
                using var image = new Bitmap(path);
                if (image.Size != monitor.Bounds.Size) throw new Exception("Selected monitor bounds were ignored.");
            }
            finally { File.Delete(path); }
        }
        var invalidRegionRejected = false;
        try { await backend.CaptureWindowAsync(shell, desktopRegion: new Rectangle(int.MinValue, 0, 100, 100)); }
        catch (InvalidOperationException) { invalidRegionRejected = true; }
        if (!invalidRegionRejected) throw new Exception("Desktop capture accepted an off-screen region.");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try { await backend.CaptureWindowAsync(shell, cancellation.Token); }
        catch (OperationCanceledException) { Console.WriteLine("desktop cancellation: pass"); return; }
        throw new Exception("Desktop capture ignored cancellation.");
    }
}
