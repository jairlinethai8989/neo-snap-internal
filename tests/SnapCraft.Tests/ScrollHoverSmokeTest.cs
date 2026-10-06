using System.Drawing;
using System.Threading;
using SnapCraft;

internal static class ScrollHoverSmokeTest
{
    public static Task RunAsync()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var content = new System.Windows.Controls.StackPanel();
            for (var row = 0; row < 60; row++) content.Children.Add(new System.Windows.Controls.TextBlock { Text = $"Hover test row {row}", Height = 40 });
            var viewer = new System.Windows.Controls.ScrollViewer { Content = content };
            var window = new System.Windows.Window { Title = "SnapCraft hover test", Left = 100, Top = 100, Width = 700, Height = 500, Topmost = true, Content = viewer };
            window.ContentRendered += async (_, _) =>
            {
                try
                {
                    var location = viewer.PointToScreen(new System.Windows.Point(150, 150));
                    var point = new Point((int)location.X, (int)location.Y);
                    var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                    using var cover = new System.Windows.Forms.Form { StartPosition = System.Windows.Forms.FormStartPosition.Manual, Bounds = new Rectangle(point.X - 60, point.Y - 60, 300, 250), TopMost = true, Opacity = .2, ShowInTaskbar = false };
                    cover.Show();
                    var excluded = cover.Handle;
                    var target = await Task.Run(() => ScrollTargetDetector.Find(point, excluded));
                    if (target is null || target.WindowHandle != handle || !target.Region.Contains(point)) throw new Exception("Scrollable viewer was not detected below the overlay");
                    if (target.Region.Width < 500 || target.Region.Height < 350) throw new Exception("Hover target bounds too small");
                    var whole = await Task.Run(() => ScrollTargetDetector.FindWindow(point, excluded));
                    if (whole is null || whole.WindowHandle != handle || whole.Region != Rectangle.Intersect(NativeInput.ClientBounds(handle), Screen.FromPoint(point).Bounds)) throw new Exception("Default full-window scroll target was not highlighted");
                    var outside = new Point((int)window.Left + 5, (int)window.Top + 5);
                    var missing = await Task.Run(() => ScrollTargetDetector.Find(outside, excluded));
                    if (missing is not null) throw new Exception("Window chrome incorrectly marked scrollable");
                    completion.TrySetResult();
                }
                catch (Exception error) { completion.TrySetException(error); }
                finally { window.Close(); System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Background); }
            };
            window.Show();
            System.Windows.Threading.Dispatcher.Run();
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }
}
