using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Web.WebView2.WinForms;
using SnapCraft;

internal static class EditorExitSmokeTest
{
    public static Task RunAsync(string root)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            WebAssets.Prepare();
            using var launcher = new MainForm();
            var tray = (NotifyIcon)typeof(MainForm).GetField("tray", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(launcher)!;
            EditorHubForm? editor = null;
            launcher.Shown += async (_, _) =>
            {
                try
                {
                    var path = WebAssets.NewCapturePath();
                    using (var image = new Bitmap(400, 250)) image.Save(path);
                    editor = (EditorHubForm)typeof(MainForm).GetMethod("OpenEditor", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(launcher, new object[] { path })!;
                    var web = editor.Controls.OfType<TabControl>().Single().TabPages[0].Controls.OfType<WebView2>().Single();
                    for (var attempt = 0; attempt < 100; attempt++)
                    {
                        if (web.CoreWebView2 is not null && await web.ExecuteScriptAsync("Boolean(window.neoSnapEditor && document.querySelector('#canvas').width === 400)") == "true") break;
                        if (attempt == 99) throw new Exception("Editor did not become ready for exit testing");
                        await Task.Delay(50);
                    }
                    tray.ContextMenuStrip!.Items[^1].PerformClick();
                    await WaitAsync(() => Application.OpenForms.Cast<Form>().Any(form => form.Name == "closeImageDialog"));
                    var prompt = Application.OpenForms.Cast<Form>().Single(form => form.Name == "closeImageDialog");
                    prompt.Controls.OfType<Button>().Single(button => button.Name == "cancelClose").PerformClick();
                    await Task.Delay(150);
                    if (launcher.IsDisposed || editor.IsDisposed || !File.Exists(path)) throw new Exception("Canceling editor close must cancel application exit");
                    launcher.Close();
                    if (launcher.IsDisposed || launcher.Visible) throw new Exception("Canceled exit must restore normal close-to-tray behavior");
                    await web.ExecuteScriptAsync("neoSnapEditor.markKept(neoSnapEditor.exportImage().snapshot)");
                    tray.ContextMenuStrip.Items[^1].PerformClick();
                    await WaitAsync(() => launcher.IsDisposed);
                    if (!editor.IsDisposed || File.Exists(path)) throw new Exception("Approved app exit did not close and clean its editor");
                    completion.TrySetResult();
                }
                catch (Exception error) { completion.TrySetException(error); }
                finally
                {
                    foreach (var prompt in Application.OpenForms.Cast<Form>().Where(form => form.Name == "closeImageDialog").ToArray())
                        prompt.Controls.OfType<Button>().Single(button => button.Name == "discardClose").PerformClick();
                    editor?.Dispose(); tray.Dispose(); launcher.Dispose();
                    Application.ExitThread();
                }
            };
            launcher.Show();
            Application.Run();
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(25));
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (condition()) return;
            await Task.Delay(50);
        }
        throw new Exception("Application exit did not reach expected state");
    }
}
