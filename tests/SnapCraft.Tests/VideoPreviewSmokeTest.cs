using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.WinForms;
using SnapCraft;

internal static class VideoPreviewSmokeTest
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string className, string title);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    public static Task RunAsync(string root)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var type = typeof(VideoSession).Assembly.GetType("SnapCraft.VideoPreviewForm");
            if (type is null) { completion.SetException(new Exception("Stopping MP4 needs a guarded preview form, not an immediate Save dialog")); return; }
            var source = Path.Combine(root, "preview-fixture.mp4");
            using (var process = Process.Start(new ProcessStartInfo(FfmpegVideoSession.FindExecutable())
            {
                Arguments = $"-hide_banner -loglevel error -y -f lavfi -i testsrc2=size=640x360:rate=10 -t 3 -c:v libx264 -pix_fmt yuv420p -movflags +faststart \"{source}\"",
                UseShellExecute = false, CreateNoWindow = true
            })!) { process.WaitForExit(); if (process.ExitCode != 0) { completion.SetException(new Exception("Could not generate preview fixture")); return; } }
            WebAssets.Prepare();
            var retained = typeof(VideoSession).Assembly.GetType("SnapCraft.VideoClipStore")!.GetMethod("Retain", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [source]) as string;
            using var preview = (Form)Activator.CreateInstance(type, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, [retained!], null)!;
            var clipboard = new DataObject();
            try { var previous = Clipboard.GetDataObject(); if (previous is not null) foreach (var format in previous.GetFormats(false)) { var value = previous.GetData(format, false); if (value is not null) clipboard.SetData(format, false, value); } }
            catch (Exception error) { completion.SetException(new Exception("Cannot safely preserve clipboard before native copy test", error)); return; }
            preview.Shown += async (_, _) =>
            {
                Exception? failure = null;
                var restored = false;
                try
                {
                    var web = preview.Controls.OfType<WebView2>().Single();
                    await WaitAsync(async () => web.CoreWebView2 is not null && await web.ExecuteScriptAsync("document.querySelector('video')?.readyState >= 2") == "true");
                    Localization.SetLanguage("en");
                    ((VideoPreviewForm)preview).RefreshLanguage();
                    await WaitAsync(async () => await web.ExecuteScriptAsync("document.documentElement.lang === 'en' && document.querySelector('#copyClip').textContent.trim() === 'Copy clip'") == "true");
                    if (!preview.Text.EndsWith("Video preview")) throw new Exception("Native preview title was not localized.");
                    Localization.SetLanguage("th");
                    ((VideoPreviewForm)preview).RefreshLanguage();
                    await WaitAsync(async () => await web.ExecuteScriptAsync("document.documentElement.lang === 'th'") == "true");
                    await web.ExecuteScriptAsync("document.querySelector('video').play()");
                    await WaitAsync(async () => await web.ExecuteScriptAsync("document.querySelector('video').currentTime > .3") == "true");
                    Console.WriteLine("preview fixture decoded; testing close cancel");
                    var close = type.GetMethod("RequestCloseAsync", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
                    Task<bool>? pendingClose = null;
                    preview.BeginInvoke(new Action(() => pendingClose = (Task<bool>)close.Invoke(preview, null)!));
                    await WaitAsync(() => Task.FromResult(Application.OpenForms.Cast<Form>().Any(form => form.Name == "closeVideoDialog")));
                    var prompt = Application.OpenForms.Cast<Form>().Single(form => form.Name == "closeVideoDialog");
                    prompt.Controls.OfType<Button>().Single(button => button.Name == "cancelClose").PerformClick();
                    await WaitAsync(() => Task.FromResult(pendingClose is not null));
                    if (await pendingClose! || !File.Exists(retained) || preview.IsDisposed) throw new Exception("Cancel must preserve the video preview and clip");
                    Console.WriteLine("close canceled; testing Save As cancel");
                    await web.ExecuteScriptAsync("document.querySelector('#saveClip').click()");
                    Console.WriteLine("save click dispatched");
                    var saveDialog = IntPtr.Zero;
                    await WaitAsync(() =>
                    {
                        saveDialog = FindWindow("#32770", $"{AppInfo.ProductName} | MP4");
                        if (saveDialog == IntPtr.Zero) return Task.FromResult(false);
                        GetWindowThreadProcessId(saveDialog, out var processId);
                        return Task.FromResult(processId == Environment.ProcessId);
                    });
                    SendMessage(saveDialog, 0x0010, IntPtr.Zero, IntPtr.Zero);
                    await WaitAsync(async () => await web.ExecuteScriptAsync("document.querySelector('#saveClip').disabled") == "false");
                    if (!File.Exists(retained) || preview.IsDisposed) throw new Exception("Canceled Save As must preserve the clip and preview");
                    await web.ExecuteScriptAsync("document.querySelector('#copyClip').click()");
                    await WaitAsync(() => Task.FromResult(Clipboard.ContainsFileDropList() && Clipboard.GetFileDropList().Contains(retained)));
                    if (await web.ExecuteScriptAsync("document.querySelector('#copyClip').getAttribute('aria-pressed')") != "\"true\"") await Task.Delay(200);
                    if (await web.ExecuteScriptAsync("document.querySelector('#copyClip').getAttribute('aria-pressed')") != "\"true\"") throw new Exception("Native copy success must be visible");
                    await RestoreClipboardAsync(clipboard);
                    restored = true;
                    if (!await (Task<bool>)close.Invoke(preview, null)!) throw new Exception("Copied video should close without discard prompt");
                    if (!File.Exists(retained)) throw new Exception("Closing copied video broke the file-drop clipboard");
                    var unkept = Path.Combine(root, "unkept.mp4"); File.Copy(retained!, unkept, true);
                    var secondPath = (string)typeof(VideoSession).Assembly.GetType("SnapCraft.VideoClipStore")!.GetMethod("Retain", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [unkept])!;
                    using var second = (Form)Activator.CreateInstance(type, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, [secondPath], null)!;
                    second.Show();
                    var secondWeb = second.Controls.OfType<WebView2>().Single();
                    await WaitAsync(async () => secondWeb.CoreWebView2 is not null && await secondWeb.ExecuteScriptAsync("document.querySelector('video')?.readyState >= 2") == "true");
                    Task<bool>? discardClose = null;
                    second.BeginInvoke(new Action(() => discardClose = (Task<bool>)close.Invoke(second, null)!));
                    await WaitAsync(() => Task.FromResult(Application.OpenForms.Cast<Form>().Any(form => form.Name == "closeVideoDialog")));
                    prompt = Application.OpenForms.Cast<Form>().Single(form => form.Name == "closeVideoDialog");
                    prompt.Controls.OfType<Button>().Single(button => button.Name == "discardClose").PerformClick();
                    await WaitAsync(() => Task.FromResult(discardClose is not null));
                    if (!await discardClose! || File.Exists(secondPath)) throw new Exception("Explicit discard must close the preview and remove its managed clip");
                    if (!File.Exists(retained)) throw new Exception("Discarding another video must not delete the clipboard clip");
                    Console.WriteLine("native video playback / close cancel / save cancel / file-drop copy / close lifetime / discard: pass");
                }
                catch (Exception error) { failure = error; }
                finally
                {
                    try { if (!restored) await RestoreClipboardAsync(clipboard); }
                    catch (Exception error) { failure = new AggregateException("Clipboard restoration failed; the test must not report success.", failure is null ? [error] : [failure, error]); }
                    finally { preview.Dispose(); Application.ExitThread(); }
                    if (failure is null) completion.TrySetResult(); else completion.TrySetException(failure);
                }
            };
            preview.Show(); Application.Run();
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(65));
    }

    private static async Task RestoreClipboardAsync(DataObject snapshot)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { Clipboard.SetDataObject(snapshot, true, 20, 100); return; }
            catch (ExternalException) when (attempt < 3) { await Task.Delay(250); }
        }
    }

    private static async Task WaitAsync(Func<Task<bool>> ready)
    {
        for (var attempt = 0; attempt < 120; attempt++) { if (await ready()) return; await Task.Delay(100); }
        throw new Exception("Native video preview did not reach expected state");
    }
}
