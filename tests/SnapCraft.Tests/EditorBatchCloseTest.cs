using System.Drawing;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.WinForms;
using SnapCraft;

internal static class EditorBatchCloseTest
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string className, string title);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    public static Task RunAsync()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var editors = new List<EditorHubForm>();
            var paths = new List<string>();
            var previousLanguage = Localization.CurrentLanguage;
            Localization.SetLanguage("en");
            for (var i = 0; i < 3; i++)
            {
                var path = WebAssets.NewCapturePath(); paths.Add(path);
                using (var image = new Bitmap(240, 120)) image.Save(path);
                var editor = new EditorHubForm(() => { }, () => editors.ToArray()) { Size = new Size(800, 550) };
                editor.AddCapture(path); editors.Add(editor);
            }
            editors[0].Shown += async (_, _) =>
            {
                try
                {
                    foreach (var editor in editors.Skip(1)) editor.Show();
                    foreach (var editor in editors) await ReadyAsync(editor);
                    editors[0].Close();
                    await WaitAsync(() => Prompt() is not null);
                    if (Prompt()!.Name != "closeEditorScopeDialog") throw new Exception("Multiple editor windows must offer current-window / all-images close choices first.");
                    SavePreview(Prompt()!, "editor-close-scope-en.png");
                    Choose("cancelScope");
                    await Task.Delay(100);
                    if (editors.Any(e => e.IsDisposed) || paths.Any(p => !File.Exists(p))) throw new Exception("Canceling scope discarded an image.");
                    await Web(editors[0]).ExecuteScriptAsync("neoSnapEditor.markKept(neoSnapEditor.exportImage().snapshot)");
                    editors[0].Close();
                    await WaitAsync(() => Prompt()?.Name == "closeEditorScopeDialog"); Choose("closeCurrentWindow");
                    await WaitAsync(() => editors[0].IsDisposed);
                    if (editors.Skip(1).Any(e => e.IsDisposed) || paths.Skip(1).Any(p => !File.Exists(p))) throw new Exception("Current-window close affected another editor.");
                    editors[1].Close();
                    await WaitAsync(() => Prompt()?.Name == "closeEditorScopeDialog"); Choose("closeAllImages");
                    await WaitAsync(() => Prompt()?.Name == "closeImagesDialog");
                    if (await editors[2].RequestCloseAsync()) throw new Exception("A concurrent close bypassed the batch lock.");
                    Choose("cancelClose"); await Task.Delay(100);
                    if (editors.Skip(1).Any(e => e.IsDisposed) || paths.Skip(1).Any(p => !File.Exists(p))) throw new Exception("Canceling batch close must retain all sources.");
                    editors[1].Close();
                    await WaitAsync(() => Prompt()?.Name == "closeEditorScopeDialog"); Choose("closeAllImages");
                    await WaitAsync(() => Prompt()?.Name == "closeImagesDialog"); Choose("saveClose");
                    var saveDialog = IntPtr.Zero;
                    await WaitAsync(() => { saveDialog = FindWindow("#32770", $"{AppInfo.ProductName} | PNG"); if (saveDialog == IntPtr.Zero) return false; GetWindowThreadProcessId(saveDialog, out var process); return process == Environment.ProcessId; });
                    SendMessage(saveDialog, 0x0010, IntPtr.Zero, IntPtr.Zero);
                    await Task.Delay(100);
                    if (editors.Skip(1).Any(e => e.IsDisposed) || paths.Skip(1).Any(p => !File.Exists(p))) throw new Exception("Canceling Save all must retain every editor and source.");
                    editors[1].Close();
                    await WaitAsync(() => Prompt()?.Name == "closeEditorScopeDialog"); Choose("closeAllImages");
                    await WaitAsync(() => Prompt()?.Name == "closeImagesDialog");
                    SavePreview(Prompt()!, "editor-close-all-en.png");
                    var late = WebAssets.NewCapturePath(); paths.Add(late); File.Copy(paths[2], late);
                    editors[2].AddCapture(late);
                    Choose("discardClose");
                    await WaitAsync(() => editors[1].IsDisposed && Tabs(editors[2]).TabCount == 1);
                    if (editors[2].IsDisposed || !File.Exists(late) || File.Exists(paths[1]) || File.Exists(paths[2])) throw new Exception("Batch close must remove approved sources but preserve a newly added image.");
                    await ReadyAsync(editors[2]);
                    await Web(editors[2]).ExecuteScriptAsync("neoSnapEditor.markKept(neoSnapEditor.exportImage().snapshot)");
                    if (!await editors[2].RequestCloseAsync() || File.Exists(late)) throw new Exception("Final editor did not close cleanly.");
                    Console.WriteLine("batch close: scope/cancel/current/all/save cancellation/reentrancy/late image preservation: pass");
                    completion.TrySetResult();
                }
                catch (Exception error) { completion.TrySetException(error); }
                finally
                {
                    foreach (var prompt in Application.OpenForms.Cast<Form>().Where(f => f.Name.StartsWith("close")).ToArray()) { prompt.DialogResult = DialogResult.Cancel; prompt.Close(); }
                    foreach (var editor in editors) editor.Dispose();
                    Localization.SetLanguage(previousLanguage);
                    Application.ExitThread();
                }
            };
            editors[0].Show(); Application.Run();
        });
        thread.IsBackground = true; thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(40));
    }
    private static TabControl Tabs(EditorHubForm editor) => editor.Controls.OfType<TabControl>().Single();
    private static WebView2 Web(EditorHubForm editor) => Tabs(editor).SelectedTab!.Controls.OfType<WebView2>().Single();
    private static Form? Prompt() => Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.Name.StartsWith("close"));
    private static void Choose(string name) => Prompt()!.Controls.OfType<Button>().Single(b => b.Name == name).PerformClick();
    private static void SavePreview(Form form, string name)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "test-output");
        Directory.CreateDirectory(root);
        using var image = new Bitmap(form.Width, form.Height); form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size)); image.Save(Path.Combine(root, name));
    }
    private static async Task ReadyAsync(EditorHubForm editor)
    {
        await WaitAsync(() => Web(editor).CoreWebView2 is not null);
        for (var i = 0; i < 200; i++) { if (await Web(editor).ExecuteScriptAsync("Boolean(window.neoSnapEditor && document.querySelector('#loading').hidden)") == "true") return; await Task.Delay(50); }
        throw new Exception("Batch fixture did not load.");
    }
    private static async Task WaitAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200; i++) { if (condition()) return; await Task.Delay(50); }
        throw new Exception("Batch close did not reach the expected state.");
    }
}
