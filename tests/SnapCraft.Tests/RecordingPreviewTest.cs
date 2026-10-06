using System.Drawing;
using System.Reflection;
using System.Threading;
using Microsoft.Web.WebView2.WinForms;
using SnapCraft;

internal static class RecordingPreviewTest
{
    public static Task RunAsync(string root)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            WebAssets.Prepare();
            using var launcher = new MainForm();
            using var target = new Form { Text = "Neo Snap recording fixture", BackColor = Color.LightCoral, StartPosition = FormStartPosition.Manual, Location = new Point(60, 60), ClientSize = new Size(640, 360) };
            target.Controls.Add(new Label { Text = "Generated recording fixture", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 24), TextAlign = ContentAlignment.MiddleCenter });
            launcher.Shown += async (_, _) =>
            {
                try
                {
                    target.Show(); target.Activate();
                    using var session = new VideoSession(target.Handle, System.IO.Path.Combine(root, "stop-preview.mp4"), forceCpu: true);
                    await session.StartAsync(); await Task.Delay(1500);
                    typeof(MainForm).GetField("video", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(launcher, session);
                    var stop = typeof(MainForm).GetMethod("StopVideoAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    await (Task)stop.Invoke(launcher, [true, null])!;
                    var preview = Application.OpenForms.OfType<Form>().Single(form => form.GetType().Name == "VideoPreviewForm");
                    var web = preview.Controls.OfType<WebView2>().Single();
                    var ready = false;
                    for (var i = 0; i < 120 && !ready; i++) { await Task.Delay(100); ready = web.CoreWebView2 is not null && await web.ExecuteScriptAsync("document.querySelector('video')?.readyState >= 2") == "true"; }
                    if (!ready) throw new Exception("Stopping a real recording did not open a playable preview");
                    launcher.BeginInvoke(new Action(() =>
                    {
                        typeof(MainForm).GetField("exitRequested", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(launcher, true);
                        launcher.Close();
                    }));
                    for (var i = 0; i < 100 && !Application.OpenForms.Cast<Form>().Any(form => form.Name == "closeVideoDialog"); i++) await Task.Delay(100);
                    Application.OpenForms.Cast<Form>().Single(form => form.Name == "closeVideoDialog").Controls.OfType<Button>().Single(button => button.Name == "cancelClose").PerformClick();
                    await Task.Delay(150);
                    if (launcher.IsDisposed || preview.IsDisposed) throw new Exception("Canceling exit must preserve the app and unkept video");
                    Task<bool>? close = null;
                    preview.BeginInvoke(new Action(() => close = (Task<bool>)preview.GetType().GetMethod("RequestCloseAsync")!.Invoke(preview, null)!));
                    for (var i = 0; i < 100 && !Application.OpenForms.Cast<Form>().Any(form => form.Name == "closeVideoDialog"); i++) await Task.Delay(100);
                    Application.OpenForms.Cast<Form>().Single(form => form.Name == "closeVideoDialog").Controls.OfType<Button>().Single(button => button.Name == "discardClose").PerformClick();
                    while (close is null) await Task.Delay(20);
                    if (!await close) throw new Exception("Test recording preview did not close");
                    Console.WriteLine("real CPU recording -> stop -> playable preview / exit cancellation preserves clip: pass");
                    completion.TrySetResult();
                }
                catch (Exception error) { completion.TrySetException(error); }
                finally
                {
                    ((NotifyIcon)typeof(MainForm).GetField("tray", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(launcher)!).Dispose();
                    launcher.Dispose(); target.Dispose(); Application.ExitThread();
                }
            };
            launcher.Show(); Application.Run();
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(60));
    }
}
