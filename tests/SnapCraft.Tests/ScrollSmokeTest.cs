using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using SnapCraft;

internal static class ScrollSmokeTest
{
    private sealed class ScrollPanel : Panel
    {
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            AutoScrollPosition = new Point(0, -AutoScrollPosition.Y + (e.Delta < 0 ? 230 : -230));
        }
    }

    public static async Task RunAsync(string root)
    {
        var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var form = new Form
            {
                Text = "SnapCraft scroll smoke",
                StartPosition = FormStartPosition.Manual,
                Location = new Point(80, 100),
                Size = new Size(780, 540),
                TopMost = true
            };
            var panel = new ScrollPanel
            {
                Dock = DockStyle.Fill,
                TabStop = true,
                AutoScroll = true,
                AutoScrollMinSize = new Size(700, 3000),
                BackColor = Color.White
            };
            for (var row = 0; row < 70; row++)
            {
                panel.Controls.Add(new Label
                {
                    Text = $"Row {row:D3}   Capture scrolling alignment test   {row * 17:D5}",
                    Location = new Point(16, row * 42),
                    Size = new Size(650, 34),
                    Font = new Font("Segoe UI", 13),
                    BackColor = row % 2 == 0 ? Color.White : Color.FromArgb(238, 246, 242)
                });
            }
            form.Controls.Add(panel);
            using var report = new System.Windows.Forms.Timer { Interval = 3000 };
            report.Tick += (_, _) => Console.WriteLine($"scroll offset: {-panel.AutoScrollPosition.X}, {-panel.AutoScrollPosition.Y}");
            form.Shown += async (_, _) =>
            {
                try
                {
                    panel.Focus();
                    report.Start();
                    await Task.Delay(500);
                    var windowImage = await new CaptureBackend().CaptureWindowAsync(form.Handle);
                    if (CaptureFrameValidator.IsBlank(windowImage))
                        throw new Exception("First window capture was blank");
                    Console.WriteLine("first window capture: pass");
                    using (var windowVideo = new VideoSession(form.Handle, Path.Combine(root, "window-smoke.mp4")))
                    {
                        await windowVideo.StartAsync();
                        await Task.Delay(1400);
                        var videoPath = await windowVideo.StopAsync(true);
                        if (videoPath is null || new FileInfo(videoPath).Length < 10_000)
                            throw new Exception("Window MP4 was not written");
                        Console.WriteLine("window video: pass");
                    }
                    var region = panel.RectangleToScreen(new Rectangle(0, 0, 190, panel.ClientSize.Height));
                    var frame = 0;
                    var path = await new CaptureCoordinator().CaptureScrollAsync(new CaptureSelection(region, form.Handle),
                        (tile, count, result, height) =>
                        {
                            frame++;
                            Console.WriteLine($"tile: {count}, {result}, {height} px");
                            if (frame <= 4) tile.Save(Path.Combine(root, $"tile-{frame}.png"));
                        });
                    if (path is null) throw new Exception("Scroll capture was canceled");
                    using var image = new Bitmap(path);
                    if (image.Height < region.Height * 2)
                        throw new Exception($"Scroll capture stayed on one viewport: {image.Height} px");
                    if (-panel.AutoScrollPosition.Y < 500)
                        throw new Exception("Wheel input did not move the target panel");
                    completed.TrySetResult(true);
                }
                catch (Exception error) { completed.TrySetException(error); }
                finally { report.Stop(); form.Close(); }
            };
            Application.Run(form);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(90));
        thread.Join(TimeSpan.FromSeconds(5));
    }
}
