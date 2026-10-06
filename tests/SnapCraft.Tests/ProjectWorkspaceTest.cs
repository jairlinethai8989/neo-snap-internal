using Microsoft.Web.WebView2.WinForms;
using SnapCraft;
using System.Reflection;
using System.Text.Json;
using System.IO;

internal static class ProjectWorkspaceTest
{
    public static Task RunAsync(string root)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            WebAssets.Prepare();
            using var editor = new EditorHubForm(() => { }) { Size = new Size(1000, 750), TopMost = true };
            for (var i = 0; i < 2; i++)
            {
                var path = WebAssets.NewCapturePath();
                using var bitmap = new Bitmap(400, 240);
                using var graphics = Graphics.FromImage(bitmap);
                graphics.Clear(i == 0 ? Color.AliceBlue : Color.Honeydew);
                graphics.FillRectangle(i == 0 ? Brushes.RoyalBlue : Brushes.SeaGreen, 0, 0, 400, 42);
                using var font = new Font("Segoe UI", 14);
                graphics.DrawString(i == 0 ? "Step 1: Open report" : "Step 2: Review results", font, Brushes.White, 16, 10);
                for (var y = 70; y < 220; y += 40) { graphics.DrawLine(Pens.LightGray, 12, y + 25, 388, y + 25); graphics.DrawString("Sample team report", font, Brushes.DarkSlateGray, 20, y); }
                bitmap.Save(path); editor.AddCapture(path);
            }
            editor.Shown += async (_, _) =>
            {
                try
                {
                    var tabs = editor.Controls.OfType<TabControl>().Single();
                    var sources = tabs.TabPages.Cast<TabPage>().Select(p => p.Controls.OfType<WebView2>().Single()).ToArray();
                    foreach (var web in sources) await Ready(web);
                    await sources[0].ExecuteScriptAsync("objects.push({tool:'text',x1:25,y1:200,text:'Editable note',size:5,color:'#ef3340',opacity:1});render()");
                    var sourceBefore = await sources[0].ExecuteScriptAsync("neoSnapEditor.exportProject().project");
                    using var timer = new System.Windows.Forms.Timer { Interval = 100 };
                    timer.Tick += (_, _) =>
                    {
                        var dialog = Application.OpenForms.OfType<CombineImagesDialog>().FirstOrDefault();
                        if (dialog is not null) { timer.Stop(); ((Button)dialog.AcceptButton!).PerformClick(); }
                    };
                    timer.Start();
                    var combine = typeof(EditorHubForm).GetMethod("CombineTabsAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    await (Task)combine.Invoke(editor, new object[] { sources[1], false })!;
                    if (tabs.TabCount != 3) throw new Exception("Combine must create an additional tab");
                    var combined = tabs.SelectedTab!.Controls.OfType<WebView2>().Single();
                    await Ready(combined);
                    if (await combined.ExecuteScriptAsync("canvas.width === 400 && canvas.height === 496 && objects.filter(o=>o.tool==='image').length === 2 && objects.some(o=>o.text==='Editable note')") != "true") throw new Exception("Native combine lost image size or editable text");
                    if (await sources[0].ExecuteScriptAsync("neoSnapEditor.exportProject().project") != sourceBefore) throw new Exception("Combining changed a source tab");
                    var exported = await combined.ExecuteScriptAsync("neoSnapEditor.exportProject().project");
                    using var project = JsonDocument.Parse(exported);
                    var output = Path.Combine(root, "project-roundtrip.neosnap");
                    await EditorHubForm.WritePngAsync(output, System.Text.Encoding.UTF8.GetBytes(project.RootElement.GetRawText()));
                    var original = File.ReadAllBytes(output);
                    using (var locked = new FileStream(output, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        try { await EditorHubForm.WritePngAsync(output, new byte[] { 1 }); throw new Exception("Locked project output must reject replacement"); }
                        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
                    }
                    if (!original.SequenceEqual(File.ReadAllBytes(output))) throw new Exception("Project save failure damaged existing file");
                    var temporary = Path.ChangeExtension(WebAssets.NewCapturePath(), ".neosnap");
                    File.Copy(output, temporary); editor.AddCapture(temporary);
                    var reopened = tabs.SelectedTab!.Controls.OfType<WebView2>().Single();await Ready(reopened);
                    if (await reopened.ExecuteScriptAsync("neoSnapEditor.exportProject().project") != exported) throw new Exception("Native project reopen did not preserve editable objects");
                    if (await reopened.ExecuteScriptAsync("neoSnapEditor.hasUnkeptChanges()") != "false") throw new Exception("Opening a saved project must restore kept state");
                    await reopened.ExecuteScriptAsync("objects.find(o=>o.tool==='text').text='Edited again';render()");
                    if (await reopened.ExecuteScriptAsync("neoSnapEditor.hasUnkeptChanges()") != "true") throw new Exception("Project edits must warn before close");
                    foreach (var page in tabs.TabPages.Cast<TabPage>())
                        await page.Controls.OfType<WebView2>().Single().ExecuteScriptAsync("neoSnapEditor.markKept(neoSnapEditor.exportImage().snapshot)");
                    if (!await editor.RequestCloseAsync()) throw new Exception("Kept project tabs could not close");
                    if (!File.Exists(output) || File.Exists(temporary)) throw new Exception("Close deleted user project or left temporary file");
                    done.TrySetResult();
                }
                catch (Exception error) { done.TrySetException(error); }
                finally { editor.Dispose(); Application.ExitThread(); }
            };
            editor.Show(); Application.Run();
        });
        thread.IsBackground = true; thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return done.Task.WaitAsync(TimeSpan.FromSeconds(60));
    }

    private static async Task Ready(WebView2 web)
    {
        for (var i = 0; i < 200; i++)
        {
            await Task.Delay(50);
            if (web.CoreWebView2 is not null && await web.ExecuteScriptAsync("Boolean(window.neoSnapEditor && document.querySelector('#loading').hidden && baseImage)") == "true") return;
        }
        throw new Exception("Native project editor did not become ready");
    }
}
