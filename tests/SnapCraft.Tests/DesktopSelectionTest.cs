using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using SnapCraft;

internal static class DesktopSelectionTest
{
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    private sealed class Host : Form { protected override bool ShowWithoutActivation => true; }

    public static Task RunAsync()
    {
        Point? exposed = null;
        foreach (var monitor in Screen.AllScreens)
        {
            var area = monitor.WorkingArea;
            for (var y = area.Top + 40; y < area.Bottom - 20 && exposed is null; y += 100)
                for (var x = area.Left + 40; x < area.Right - 20; x += 100)
                {
                    var point = new Point(x, y);
                    if (!NativeInput.IsDesktopWindow(NativeInput.RootWindowAt(point))) continue;
                    exposed = point;
                    break;
                }
            if (exposed is not null) break;
        }
        if (exposed is null) throw new Exception("Desktop click verification requires an exposed Desktop point. No user windows were moved or closed.");

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var host = new Host { StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000),
                Size = new Size(1, 1), ShowInTaskbar = false, FormBorderStyle = FormBorderStyle.None };
            host.Shown += async (_, _) =>
            {
                var cursor = Cursor.Position;
                SelectionOverlay? overlay = null;
                string? path = null;
                try
                {
                    var point = exposed.Value;
                    var screen = Screen.FromPoint(point).Bounds;
                    Cursor.Position = point;
                    var capture = new CaptureCoordinator().CaptureAsync(CaptureKind.Window, 0);
                    await Task.Delay(150);
                    overlay = Application.OpenForms.OfType<SelectionOverlay>().Single();
                    var local = overlay.PointToClient(point);
                    var clock = Stopwatch.StartNew();
                    SendMessage(overlay.Handle, 0x0201, new IntPtr(1), new IntPtr((local.Y << 16) | (local.X & 0xffff)));
                    path = await capture.WaitAsync(TimeSpan.FromSeconds(10));
                    if (path is null) throw new Exception("Desktop click was canceled instead of captured.");
                    using var image = new Bitmap(path);
                    if (image.Size != screen.Size) throw new Exception("Desktop click did not retain the clicked monitor's native dimensions.");
                    if (clock.ElapsedMilliseconds >= 2000) throw new Exception($"Desktop click used slow window retries: {clock.ElapsedMilliseconds}ms.");
                    Console.WriteLine($"actual Desktop click -> coordinator -> PNG: {clock.ElapsedMilliseconds}ms, {image.Width}x{image.Height}, native resolution");
                    completion.TrySetResult();
                }
                catch (Exception error) { completion.TrySetException(error); }
                finally
                {
                    if (overlay is { IsDisposed: false }) overlay.Close();
                    Cursor.Position = cursor;
                    if (path is not null) File.Delete(path);
                    host.Close();
                }
            };
            Application.Run(host);
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }
}
