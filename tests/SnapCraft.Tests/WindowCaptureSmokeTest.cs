using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using SnapCraft;
using SelectionMode = SnapCraft.SelectionMode;

internal static class WindowCaptureSmokeTest
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    public static Task RunAsync(string root)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var form = new Form { Text = "Neo Snap exact-pixel test", StartPosition = FormStartPosition.Manual, Location = new Point(80, 120), ClientSize = new Size(740, 430), TopMost = true };
            var panel = new Panel { Dock = DockStyle.Fill };
            var black = false;
            form.Controls.Add(panel);
            panel.Paint += (_, e) =>
            {
                if (black) { e.Graphics.Clear(Color.Black); return; }
                e.Graphics.Clear(Color.White);
                e.Graphics.FillRectangle(Brushes.RoyalBlue, 20, 20, 110, 100);
                e.Graphics.FillRectangle(Brushes.Coral, panel.Width - 70, 20, 50, panel.Height - 40);
                e.Graphics.FillRectangle(Brushes.LimeGreen, 20, panel.Height - 40, panel.Width - 40, 20);
                e.Graphics.DrawString("1:1 pixels", panel.Font, Brushes.Black, 150, 30);
            };
            form.Shown += async (_, _) =>
            {
                try
                {
                    await Task.Delay(180);
                    WebAssets.Prepare();
                    var backend = new CaptureBackend();
                    for (var attempt = 0; attempt < 3; attempt++)
                    {
                        var clock = Stopwatch.StartNew();
                        var path = await backend.CaptureWindowAsync(form.Handle);
                        clock.Stop();
                        using (var image = new Bitmap(path))
                        {
                            var bounds = Rectangle.Intersect(NativeInput.VisibleWindowBounds(form.Handle), SystemInformation.VirtualScreen);
                            if (image.Size != bounds.Size) throw new Exception("Window capture was resized");
                            var origin = panel.PointToScreen(Point.Empty);
                            foreach (var (point, color) in new[] { (new Point(50, 50), Color.RoyalBlue), (new Point(panel.Width - 50, 50), Color.Coral), (new Point(50, panel.Height - 30), Color.LimeGreen) })
                            {
                                var x = origin.X - bounds.Left + point.X;
                                var y = origin.Y - bounds.Top + point.Y;
                                if (image.GetPixel(x, y).ToArgb() != color.ToArgb())
                                {
                                    image.Save(Path.Combine(root, "window-capture-failure.png"));
                                    throw new Exception($"Window capture pixel mismatch at {x},{y}: actual={image.GetPixel(x,y)}, expected={color}; image={image.Size}, window={bounds}, panel={origin}");
                                }
                            }
                            if (clock.ElapsedMilliseconds > 2000) throw new Exception($"Visible window capture took {clock.ElapsedMilliseconds}ms");
                            Console.WriteLine($"window capture {attempt + 1}: {clock.ElapsedMilliseconds}ms, {image.Width}x{image.Height}, exact edge pixels");
                        }
                        File.Delete(path);
                    }
                    var region = panel.RectangleToScreen(panel.ClientRectangle);
                    using var area = await backend.CaptureRegionAsync(region);
                    if (area.Size != region.Size || area.GetPixel(area.Width - 50, 50).ToArgb() != Color.Coral.ToArgb()) throw new Exception("Visible region capture changed scale");
                    area.Save(Path.Combine(root, "exact-window-client.png"));
                    black = true;
                    panel.Refresh();
                    await Task.Delay(100);
                    var blackClock = Stopwatch.StartNew();
                    using (var dark = await backend.CaptureRegionAsync(region))
                    {
                        if (!CaptureFrameValidator.IsBlank(dark)) throw new Exception("Black fixture did not stay black.");
                        Console.WriteLine($"legitimate black region: {blackClock.ElapsedMilliseconds}ms, native dimensions preserved");
                    }
                    black = false;
                    panel.Refresh();
                    foreach (var mode in new[] { SelectionMode.Window, SelectionMode.Scroll })
                    {
                        form.Activate();
                        Cursor.Position = panel.PointToScreen(new Point(200, 200));
                        var foreground = GetForegroundWindow();
                        var selection = SelectionOverlay.ChooseAsync(mode);
                        await Task.Delay(180);
                        var overlay = Application.OpenForms.OfType<SelectionOverlay>().Single();
                        if (GetForegroundWindow() != foreground) throw new Exception($"Selection overlay stole focus: mode={mode}, before={foreground}, after={GetForegroundWindow()}, overlay={overlay.Handle}");
                        var point = overlay.PointToClient(Cursor.Position);
                        var packed = new IntPtr((point.Y << 16) | point.X);
                        SendMessage(overlay.Handle, 0x0201, new IntPtr(1), packed);
                        var selected = await selection;
                        if (selected?.WindowHandle != form.Handle || GetForegroundWindow() != foreground) throw new Exception("Selection changed the foreground window");
                    }
                    var cancelSelection = SelectionOverlay.ChooseAsync(SelectionMode.Region);
                    await Task.Delay(80);
                    SendKeys.SendWait("{ESC}");
                    if (await cancelSelection.WaitAsync(TimeSpan.FromSeconds(2)) is not null) throw new Exception("Esc did not cancel nonactivating selection");
                    Console.WriteLine("window/scroll selection keeps target focus; global Esc selection cancel: pass");
                    completion.TrySetResult();
                }
                catch (Exception error) { completion.TrySetException(error); }
                finally { form.Close(); }
            };
            Application.Run(form);
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(25));
    }
}
