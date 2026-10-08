using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using Microsoft.Web.WebView2.WinForms;
using SnapCraft;

internal static class EditorLatencyTest
{
    public static Task RunTimingAsync() => RunAsync(false);
    public static Task RunPreviewAsync() => RunAsync(true);
    public static Task RunLargePreviewAsync() => RunAsync(true, 1920, 5000);

    public static Task RunPendingAsync() => RunPendingAsync(false);
    public static Task RunPendingFallbackAsync() => RunPendingAsync(true);

    private static Task RunPendingAsync(bool failWarmup)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var form = new MainForm(startInTray: true);
            using var timer = new System.Windows.Forms.Timer { Interval = 20 };
            EditorHubForm? opened = null;
            timer.Tick += async (_, _) =>
            {
                timer.Stop();
                try
                {
                    await (Task)typeof(MainForm).GetField("assetsPrepared", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
                    var preparation = (Task)typeof(MainForm).GetMethod("PrepareNextEditorAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null)!;
                    var pending = (EditorHubForm?)typeof(MainForm).GetField("warmEditor", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)
                        ?? throw new Exception("Editor preparation did not start");
                    if (pending.IsWarm) throw new Exception("Pending editor test ran after preparation completed");
                    var path = WebAssets.NewCapturePath();
                    using (var image = new Bitmap(1200, 800))
                    {
                        using var graphics = Graphics.FromImage(image);
                        graphics.Clear(Color.Crimson);
                        image.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    var clock = Stopwatch.StartNew();
                    opened = (EditorHubForm)typeof(MainForm).GetMethod("OpenEditor", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, new object[] { path })!;
                    if (!ReferenceEquals(pending, opened))
                        throw new Exception("Capture arriving during warmup created a second editor instead of reusing the pending one");
                    if (typeof(MainForm).GetField("warmEditor", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form) is EditorHubForm)
                        throw new Exception("A standby editor started preparing before the current capture was rendered");
                    if (failWarmup)
                    {
                        var ready = (TaskCompletionSource)typeof(EditorHubForm).GetField("warmReady", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pending)!;
                        ready.TrySetException(new TimeoutException("Simulated editor preparation failure"));
                    }
                    var page = opened.Controls.OfType<TabControl>().Single().SelectedTab!;
                    var view = page.Controls.OfType<WebView2>().Single();
                    while (view.CoreWebView2 is null || await view.ExecuteScriptAsync("Boolean(window.neoSnapEditor && canvas.width === 1200 && document.querySelector('#loading').hidden)") != "true")
                    {
                        if (clock.Elapsed > TimeSpan.FromSeconds(15)) throw new Exception("Capture received during preparation did not load");
                        await Task.Delay(10);
                    }
                    await preparation;
                    if (await view.ExecuteScriptAsync("(() => { render(false); return [...ctx.getImageData(1199,799,1,1).data].join(','); })()") != "\"220,20,60,255\"")
                        throw new Exception("Capture received during preparation changed original pixels");
                    Console.WriteLine($"pending editor reused, fallback={failWarmup}, original pixels preserved; first capture image: {clock.ElapsedMilliseconds} ms");
                    await view.ExecuteScriptAsync("neoSnapEditor.markKept(imageSnapshot())");
                    if (!await opened.RequestCloseAsync()) throw new Exception("Pending capture editor did not close");
                    completion.TrySetResult();
                }
                catch (Exception error) { completion.TrySetException(error); }
                finally
                {
                    opened?.Dispose();
                    var tray = (NotifyIcon)typeof(MainForm).GetField("tray", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
                    tray.ContextMenuStrip!.Items[^1].PerformClick();
                }
            };
            timer.Start();
            Application.Run(form);
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    private static Task RunAsync(bool blockImage, int width = 1200, int height = 800)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var host = new Form { ShowInTaskbar = false, Size = new Size(1, 1), Opacity = 0 };
            host.Shown += async (_, _) =>
            {
                using var editor = new EditorHubForm(() => { });
                try
                {
                    await editor.PrepareWarmAsync();
                    var page = editor.Controls.OfType<TabControl>().Single().TabPages[0];
                    var view = page.Controls.OfType<WebView2>().Single();
                    var path = WebAssets.NewCapturePath();
                    using (var image = new Bitmap(width, height))
                    {
                        using var graphics = Graphics.FromImage(image);
                        graphics.Clear(Color.Crimson);
                        image.SetPixel(width - 1, height - 1, Color.Lime);
                        image.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    if (blockImage)
                    {
                        await view.ExecuteScriptAsync("window.latencyImageGate = new Promise(resolve => window.releaseLatencyImage = resolve); window.realLoadImage = loadImage; loadImage = async src => { await latencyImageGate; return realLoadImage(src); }");
                    }
                    // Time spent ready but idle must not count as loading the next capture.
                    await Task.Delay(1200);
                    var clock = Stopwatch.StartNew();
                    editor.AddCapture(path);
                    editor.Show();
                    if (blockImage)
                    {
                        while (page.Controls.OfType<PictureBox>().SingleOrDefault(p => p.Name == "nativePreview") is null)
                        {
                            if (clock.ElapsedMilliseconds > 1500)
                                throw new Exception("Prepared editor did not show a native preview while its image response was blocked");
                            await Task.Delay(10);
                        }
                        var preview = page.Controls.OfType<PictureBox>().Single(p => p.Name == "nativePreview");
                        var bitmap = (Bitmap)preview.Image!;
                        if (bitmap.Size != new Size(width, height) || bitmap.GetPixel(width - 1, height - 1).ToArgb() != Color.Lime.ToArgb())
                            throw new Exception("Native preview changed the original image dimensions or edge pixels");
                        Console.WriteLine($"prepared native preview {width}x{height} with blocked WebView image: {clock.ElapsedMilliseconds} ms");
                        if (await view.ExecuteScriptAsync($"canvas.width === {width}") == "true")
                            throw new Exception("Test image gate did not delay browser loading");
                        await view.ExecuteScriptAsync("releaseLatencyImage()");
                    }
                    while (view.CoreWebView2 is null || await view.ExecuteScriptAsync($"Boolean(window.neoSnapEditor && canvas.width === {width} && document.querySelector('#loading').hidden)") != "true")
                    {
                        if (clock.Elapsed > TimeSpan.FromSeconds(10)) throw new Exception("Prepared capture did not load");
                        await Task.Delay(10);
                    }
                    var elapsed = clock.Elapsed.TotalMilliseconds;
                    if (await view.ExecuteScriptAsync($"(() => {{ render(false); return [...ctx.getImageData({width - 1},{height - 1},1,1).data].join(','); }})()") != "\"0,255,0,255\"")
                        throw new Exception("Prepared editor changed original edge pixels");
                    if (!blockImage)
                    {
                        var recorded = await ReadReadyTimingAsync();
                        if (recorded > elapsed + 250)
                            throw new Exception($"Editor ready timing included idle warmup: recorded {recorded:F1} ms, actual load {elapsed:F1} ms");
                        Console.WriteLine($"prepared capture image: {elapsed:F1} ms; ready timing excludes idle: {recorded:F1} ms");
                    }
                    await view.ExecuteScriptAsync("neoSnapEditor.markKept(imageSnapshot())");
                    if (!await editor.RequestCloseAsync()) throw new Exception("Test editor did not close");
                    completion.TrySetResult();
                }
                catch (Exception error) { completion.TrySetException(error); }
                finally { host.Close(); }
            };
            Application.Run(host);
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    private static async Task<double> ReadReadyTimingAsync()
    {
        var path = Path.Combine(WebAssets.DataRoot, "Logs", "performance.log");
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (File.Exists(path))
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                var line = (await reader.ReadToEndAsync()).Split('\n').LastOrDefault(line => line.Contains($" pid={Environment.ProcessId} ") && line.Contains(" editor.ready "));
                if (line is not null)
                    return double.Parse(line.Trim().Split(' ')[^1][..^2], System.Globalization.CultureInfo.InvariantCulture);
            }
            await Task.Delay(20);
        }
        throw new Exception("Capture ready timing was not recorded");
    }
}
