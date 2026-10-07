using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using SnapCraft;
using SelectionMode = SnapCraft.SelectionMode;

internal static class CloakedSelectionTest
{
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);

    private sealed class Phantom : Form { protected override bool ShowWithoutActivation => true; }

    public static Task RunAsync()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var screen = Screen.AllScreens.Last().WorkingArea;
            using var target = new Form { Text = "Generated selection target", StartPosition = FormStartPosition.Manual,
                Location = new Point(screen.Left + 100, screen.Top + 140), ClientSize = new Size(400, 260), BackColor = Color.RoyalBlue, TopMost = true };
            using var phantom = new Phantom { Text = "Generated cloaked window", FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual, BackColor = Color.Magenta, TopMost = true, ShowInTaskbar = false };
            target.Shown += async (_, _) =>
            {
                string? path = null;
                SelectionOverlay? overlay = null;
                var originalCursor = Cursor.Position;
                try
                {
                    await Task.Delay(150);
                    var bounds = NativeInput.VisibleWindowBounds(target.Handle);
                    phantom.Bounds = bounds;
                    phantom.Show();
                    var cloak = 1;
                    if (DwmSetWindowAttribute(phantom.Handle, 13, ref cloak, 4) != 0) throw new Exception("Could not cloak the generated test window.");
                    SetWindowPos(phantom.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x13);
                    SetWindowPos(target.Handle, phantom.Handle, 0, 0, 0, 0, 0x13);
                    await Task.Delay(100);
                    if (DwmGetWindowAttribute(phantom.Handle, 14, out var state, 4) != 0 || state == 0 || !IsWindowVisible(phantom.Handle))
                        throw new Exception("Fixture did not reproduce a visible-style, DWM-cloaked window.");
                    var point = target.PointToScreen(new Point(80, 80));
                    if (NativeInput.RootWindowAt(point) != target.Handle)
                        throw new Exception($"Generated target is obscured: expected={target.Handle}, actual={NativeInput.RootWindowAt(point)}, point={point}.");
                    if (NativeInput.WindowBelow(point, IntPtr.Zero) != target.Handle)
                        throw new Exception("Window selection chose the DWM-hidden phantom instead of the displayed target.");
                    if (NativeInput.IsCaptureTargetVisible(phantom.Handle)) throw new Exception("DWM-hidden window is considered capturable.");
                    if (!NativeInput.IsWindowUnobstructed(target.Handle, bounds)) throw new Exception("DWM-hidden window falsely blocks the screen-copy fast path.");
                    Cursor.Position = point;
                    var choosing = SelectionOverlay.ChooseAsync(SelectionMode.Window);
                    await Task.Delay(150);
                    overlay = Application.OpenForms.OfType<SelectionOverlay>().Single();
                    var local = overlay.PointToClient(point);
                    SendMessage(overlay.Handle, 0x0201, new IntPtr(1), new IntPtr((local.Y << 16) | (local.X & 0xffff)));
                    var selection = await choosing.WaitAsync(TimeSpan.FromSeconds(2));
                    if (selection?.WindowHandle != target.Handle || selection.Region != bounds)
                        throw new Exception("The actual selection overlay retained the hidden phantom target.");
                    path = await new CaptureBackend().CaptureWindowAsync(selection.WindowHandle, desktopRegion: selection.Region);
                    using var image = new Bitmap(path);
                    if (image.Size != bounds.Size || image.GetPixel(point.X - bounds.Left, point.Y - bounds.Top).ToArgb() != Color.RoyalBlue.ToArgb())
                        throw new Exception("Capture after window selection changed native pixels or returned the phantom.");
                    Console.WriteLine("DWM-cloaked windows: excluded from hit testing / overlay click / occlusion / capture, exact target pixels: pass");
                    completion.TrySetResult();
                }
                catch (Exception error) { completion.TrySetException(error); }
                finally
                {
                    if (overlay is { IsDisposed: false }) overlay.Close();
                    Cursor.Position = originalCursor;
                    phantom.Close(); target.Close();
                    if (path is not null) File.Delete(path);
                }
            };
            Application.Run(target);
        });
        thread.IsBackground = true; thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }
}
