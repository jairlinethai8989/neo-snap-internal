using System.IO;
using System.Runtime.InteropServices;
using SnapCraft;

internal static class OccludedWindowTest
{
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int cx, int cy, uint flags);
    private sealed class Cover : Form { protected override bool ShowWithoutActivation => true; }

    public static Task RunAsync()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var target = new Form { Text = "Generated capture target", StartPosition = FormStartPosition.Manual, Location = new Point(80, 120), ClientSize = new Size(300, 200), BackColor = Color.RoyalBlue };
            using var cover = new Cover { Text = "Generated occluder", FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, BackColor = Color.Magenta, TopMost = true };
            using var timer = new System.Windows.Forms.Timer { Interval = 10 };
            timer.Tick += (_, _) => SetWindowPos(cover.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x13);
            target.Shown += async (_, _) =>
            {
                string? path = null;
                try
                {
                    await Task.Delay(150);
                    cover.Bounds = NativeInput.VisibleWindowBounds(target.Handle);
                    cover.Show(); timer.Start();
                    await Task.Delay(120);
                    var point = target.PointToScreen(new Point(50, 50));
                    if (NativeInput.RootWindowAt(point) != cover.Handle) throw new Exception("Occlusion fixture did not cover target.");
                    path = await new CaptureBackend().CaptureWindowAsync(target.Handle);
                    using var image = new Bitmap(path);
                    var bounds = NativeInput.VisibleWindowBounds(target.Handle);
                    if (image.GetPixel(point.X - bounds.X, point.Y - bounds.Y).ToArgb() != Color.RoyalBlue.ToArgb())
                        throw new Exception("Window capture returned the occluder's pixels rather than target pixels.");
                    Console.WriteLine("occluded window: target pixels retained, no obscuring application pixels: pass");
                    completion.TrySetResult();
                }
                catch (Exception error) { completion.TrySetException(error); }
                finally { timer.Stop(); cover.Close(); target.Close(); if (path is not null) File.Delete(path); }
            };
            Application.Run(target);
        });
        thread.IsBackground = true; thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }
}
