using System.Diagnostics;
using System.Reflection;
using System.IO;
using Microsoft.Web.WebView2.WinForms;
using SnapCraft;

internal static class FastStartupTest
{
    public static Task RunAsync()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var form = new MainForm(startInTray: true);
            using var timer = new System.Windows.Forms.Timer { Interval = 50 };
            var clock = Stopwatch.StartNew();
            timer.Tick += async (_, _) =>
            {
                timer.Stop();
                try
                {
                    var panel = form.Controls.OfType<Panel>().SingleOrDefault(p => p.Name == "fastLauncher");
                    if (panel is null || panel.Controls.OfType<Button>().Count() != 4)
                        throw new Exception("Capture controls are not available before browser startup");
                    if (form.Visible) throw new Exception("Tray startup unexpectedly showed the launcher");
                    var web = form.Controls.OfType<WebView2>().Single();
                    while (web.CoreWebView2 is null)
                    {
                        if (clock.Elapsed > TimeSpan.FromSeconds(12)) throw new Exception("Silent tray startup did not prepare its browser");
                        await Task.Delay(20);
                    }
                    Console.WriteLine($"tray browser prepared without opening launcher: {clock.ElapsedMilliseconds} ms");
                    var warm = typeof(MainForm).GetField("warmEditor", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    EditorHubForm? editor;
                    while ((editor = warm.GetValue(form) as EditorHubForm) is null ||
                        !((Task?)typeof(EditorHubForm).GetField("warmup", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(editor) is { IsCompletedSuccessfully: true }))
                    {
                        if (clock.Elapsed > TimeSpan.FromSeconds(15)) throw new Exception("Editor warmup did not finish");
                        await Task.Delay(25);
                    }
                    var path = WebAssets.NewCapturePath();
                    using (var image = new Bitmap(1200, 800)) { using var g = Graphics.FromImage(image); g.Clear(Color.Crimson); image.Save(path); }
                    clock.Restart();
                    var opened = (EditorHubForm)typeof(MainForm).GetMethod("OpenEditor", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, new object[] { path })!;
                    if (opened != editor) throw new Exception("First capture did not use the prepared editor");
                    var view = opened.Controls.OfType<TabControl>().Single().SelectedTab!.Controls.OfType<WebView2>().Single();
                    while (await view.ExecuteScriptAsync("Boolean(window.neoSnapEditor && document.querySelector('#canvas').width === 1200 && document.querySelector('#loading').hidden)") != "true")
                    {
                        if (clock.Elapsed > TimeSpan.FromSeconds(2)) throw new Exception("Prepared editor did not display the image within 2 seconds");
                        await Task.Delay(10);
                    }
                    Console.WriteLine($"prepared capture-to-editor image: {clock.ElapsedMilliseconds} ms");
                    var pixels = await view.ExecuteScriptAsync("(() => { render(false); return [...ctx.getImageData(1199,799,1,1).data].join(','); })()");
                    if (pixels != "\"220,20,60,255\"") throw new Exception("Preloading changed capture pixels");
                    var settings = AppSettings.Load(); settings.HistoryEnabled = true; settings.Save();
                    await view.ExecuteScriptAsync("objects.push({tool:'text',x1:20,y1:20,text:'Recovered edit',size:5,color:'#ff0000',opacity:1});render()");
                    await (Task)typeof(EditorHubForm).GetMethod("SaveRecoveryAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(opened, null)!;
                    var entry = CaptureHistory.Entries().FirstOrDefault(e => e.Extension == ".neosnap") ?? throw new Exception("Editable recovery was not stored");
                    if (!File.ReadAllText(entry.FullName).Contains("Recovered edit")) throw new Exception("Recovery lost editable objects");
                    settings.HistoryEnabled = false; settings.Save(); await CaptureHistory.ClearAsync();
                    Console.WriteLine("prepared editor editable recovery: pass");
                    await view.ExecuteScriptAsync("neoSnapEditor.markKept(imageSnapshot())");
                    if (!await opened.RequestCloseAsync()) throw new Exception("Prepared editor could not close safely");
                    completion.TrySetResult();
                }
                catch (Exception error) { completion.TrySetException(error); }
                finally
                {
                    foreach (var editor in Application.OpenForms.OfType<EditorHubForm>().ToArray()) editor.Dispose();
                    var tray = (NotifyIcon)typeof(MainForm).GetField("tray", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
                    tray.ContextMenuStrip!.Items[^1].PerformClick();
                }
            };
            timer.Start(); Application.Run(form);
        });
        thread.IsBackground = true; thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(25));
    }
}
