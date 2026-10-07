using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Web.WebView2.WinForms;
using SnapCraft;

internal static class EditorTabsSmokeTest
{
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string className, string title);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    public static Task RunAsync(string root)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            WebAssets.Prepare();
            var first = WebAssets.NewCapturePath();
            var second = WebAssets.NewCapturePath();
            using (var image = new Bitmap(700, 350))
            {
                using var graphics = Graphics.FromImage(image);
                graphics.Clear(Color.AliceBlue);
                graphics.FillRectangle(Brushes.RoyalBlue, 40, 40, 220, 90);
                image.Save(first); image.Save(second);
            }
            using var editor = new EditorHubForm(() => { }) { TopMost = true, Size = new Size(900, 600) };
            string? lateCapture = null;
            var lateCaptureConfirmed = false;
            editor.FormClosed += (_, _) =>
            {
                if (lateCapture is not null && !lateCaptureConfirmed)
                    completion.TrySetException(new Exception("A capture added during window close was silently discarded"));
            };
            editor.AddCapture(first); editor.AddCapture(second);
            editor.Shown += async (_, _) =>
            {
                try
                {
                    var tabs = editor.Controls.OfType<TabControl>().Single();
                    var web = tabs.SelectedTab!.Controls.OfType<WebView2>().Single();
                    var loaded = false;
                    for (var attempt = 0; attempt < 100 && !loaded; attempt++)
                    {
                        await Task.Delay(100);
                        if (web.CoreWebView2 is not null)
                            loaded = await web.ExecuteScriptAsync("document.querySelector('#canvas')?.width === 700") == "true";
                    }
                    if (!loaded) throw new Exception("Native editor did not load the fixture image");
                    var bounds = tabs.GetTabRect(1);
                    var point = tabs.PointToScreen(new Point(bounds.Right - 18, bounds.Top + bounds.Height / 2));
                    Cursor.Position = point;
                    await Task.Delay(150);
                    using (var preview = new Bitmap(tabs.Width, 42))
                    {
                        using var graphics = Graphics.FromImage(preview);
                        graphics.CopyFromScreen(tabs.PointToScreen(Point.Empty), Point.Empty, preview.Size);
                        preview.Save(Path.Combine(root, "editor-tabs.png"));
                    }
                    var packedPoint = new IntPtr(((bounds.Top + bounds.Height / 2) << 16) | (bounds.Right - 18));
                    SendMessage(tabs.Handle, 0x0201, new IntPtr(1), packedPoint);
                    SendMessage(tabs.Handle, 0x0202, IntPtr.Zero, packedPoint);
                    await WaitAsync(() => tabs.TabCount != 2 || Application.OpenForms.Cast<Form>().Any(form => form.Name == "closeImageDialog"));
                    if (tabs.TabCount != 2 || !File.Exists(second)) throw new Exception("Closing an unkept capture must ask before removing its tab/source");
                    var prompt = Application.OpenForms.Cast<Form>().Single(form => form.Name == "closeImageDialog");
                    prompt.Controls.OfType<Button>().Single(button => button.Name == "cancelClose").PerformClick();
                    await Task.Delay(100);
                    if (tabs.TabCount != 2 || !File.Exists(second)) throw new Exception("Cancel lost the pending capture");
                    SendMessage(tabs.Handle, 0x0201, new IntPtr(1), packedPoint);
                    SendMessage(tabs.Handle, 0x0202, IntPtr.Zero, packedPoint);
                    await WaitAsync(() => Application.OpenForms.Cast<Form>().Any(form => form.Name == "closeImageDialog"));
                    prompt = Application.OpenForms.Cast<Form>().Single(form => form.Name == "closeImageDialog");
                    prompt.Controls.OfType<Button>().Single(button => button.Name == "saveClose").PerformClick();
                    var saveDialog = IntPtr.Zero;
                    await WaitAsync(() =>
                    {
                        saveDialog = FindWindow("#32770", $"{AppInfo.ProductName} | PNG");
                        if (saveDialog == IntPtr.Zero) return false;
                        GetWindowThreadProcessId(saveDialog, out var processId);
                        return processId == Environment.ProcessId;
                    });
                    SendMessage(saveDialog, 0x0010, IntPtr.Zero, IntPtr.Zero);
                    await Task.Delay(100);
                    if (tabs.TabCount != 2 || !File.Exists(second)) throw new Exception("Canceling PNG save must cancel tab close too");
                    SendMessage(tabs.Handle, 0x0201, new IntPtr(1), packedPoint);
                    SendMessage(tabs.Handle, 0x0202, IntPtr.Zero, packedPoint);
                    await WaitAsync(() => Application.OpenForms.Cast<Form>().Any(form => form.Name == "closeImageDialog"));
                    prompt = Application.OpenForms.Cast<Form>().Single(form => form.Name == "closeImageDialog");
                    prompt.Controls.OfType<Button>().Single(button => button.Name == "discardClose").PerformClick();
                    await WaitAsync(() => tabs.TabCount == 1);
                    if (tabs.TabCount != 1 || File.Exists(second) || !File.Exists(first)) throw new Exception("Close icon did not close only the selected tab");
                    var firstWeb = tabs.SelectedTab!.Controls.OfType<WebView2>().Single();
                    await WaitAsync(() => firstWeb.CoreWebView2 is not null);
                    for (var attempt = 0; attempt < 100; attempt++)
                    {
                        if (await firstWeb.ExecuteScriptAsync("Boolean(window.neoSnapEditor && document.querySelector('#canvas').width === 700)") == "true") break;
                        if (attempt == 99) throw new Exception("First editor did not load");
                        await Task.Delay(50);
                    }
                    await firstWeb.ExecuteScriptAsync("neoSnapEditor.markKept(neoSnapEditor.exportImage().snapshot)");
                    var third = WebAssets.NewCapturePath();
                    lateCapture = third;
                    File.Copy(first, third);
                    editor.Close();
                    editor.AddCapture(third);
                    await WaitAsync(() => Application.OpenForms.Cast<Form>().Any(form => form.Name == "closeEditorScopeDialog"));
                    var scope = Application.OpenForms.Cast<Form>().Single(form => form.Name == "closeEditorScopeDialog");
                    scope.Controls.OfType<Button>().Single(button => button.Name == "closeCurrentWindow").PerformClick();
                    await WaitAsync(() => editor.IsDisposed || Application.OpenForms.Cast<Form>().Any(form => form.Name == "closeImageDialog"));
                    if (editor.IsDisposed || !File.Exists(third)) throw new Exception("A capture added during window close must also be confirmed");
                    prompt = Application.OpenForms.Cast<Form>().Single(form => form.Name == "closeImageDialog");
                    lateCaptureConfirmed = true;
                    prompt.Controls.OfType<Button>().Single(button => button.Name == "discardClose").PerformClick();
                    await WaitAsync(() => editor.IsDisposed);
                    if (File.Exists(first) || File.Exists(third)) throw new Exception("Closed editor left its capture behind");
                    completion.TrySetResult();
                }
                catch (Exception error)
                {
                    Console.WriteLine($"Editor failure state: disposed={editor.IsDisposed}, visible={editor.Visible}, tabs={editor.Controls.OfType<TabControl>().FirstOrDefault()?.TabCount}");
                    completion.TrySetException(error);
                }
                finally
                {
                    foreach (var prompt in Application.OpenForms.Cast<Form>().Where(form => form.Name == "closeImageDialog").ToArray())
                        prompt.Controls.OfType<Button>().Single(button => button.Name == "discardClose").PerformClick();
                    editor.Dispose();
                    Application.ExitThread();
                }
            };
            editor.Show();
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
        throw new Exception("Editor lifecycle did not reach the expected state");
    }
}
