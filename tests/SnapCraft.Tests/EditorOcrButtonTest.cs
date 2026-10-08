using System.IO;
using System.Reflection;
using Microsoft.Web.WebView2.WinForms;
using SnapCraft;

internal static class EditorOcrButtonTest
{
    public static Task RunAsync()
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            WebAssets.Prepare();
            var path = WebAssets.NewCapturePath();
            using (var image = new Bitmap(400, 250)) { using var g = Graphics.FromImage(image); g.Clear(Color.White); image.Save(path); }
            using var editor = new EditorHubForm(() => { });
            editor.AddCapture(path);
            editor.Shown += async (_, _) =>
            {
                try
                {
                    var view = editor.Controls.OfType<TabControl>().Single().SelectedTab!.Controls.OfType<WebView2>().Single();
                    for (var i = 0; i < 150; i++)
                    {
                        if (view.CoreWebView2 is not null && await view.ExecuteScriptAsync("Boolean(window.neoSnapEditor && document.querySelector('#loading').hidden && canvas.width === 400)") == "true") break;
                        if (i == 149) throw new Exception("OCR editor fixture did not load");
                        await Task.Delay(100);
                    }
                    await view.ExecuteScriptAsync("setTool('pen')");
                    var busy = typeof(EditorHubForm).GetField("projectCommandBusy", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    foreach (var region in new[] { false, true })
                    {
                        await view.ExecuteScriptAsync(region ? "cropDraft={x:30,y:40,width:110,height:70}" : "cropDraft=null;selected=-1");
                        await view.ExecuteScriptAsync("document.querySelector('#ocrButton').click();document.querySelector('#ocrButton').click()");
                        OcrReviewForm? dialog = null;
                        for (var i = 0; i < 100 && dialog is null; i++) { await Task.Delay(50); dialog = Application.OpenForms.OfType<OcrReviewForm>().SingleOrDefault(); }
                        if (dialog is null) throw new Exception("Direct OCR button did not open the native review dialog");
                        try
                        {
                            var png = (byte[])typeof(OcrReviewForm).GetField("image", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dialog)!;
                            using var stream = new MemoryStream(png); using var image = new Bitmap(stream);
                            var expected = region ? new Size(110, 70) : new Size(400, 250);
                            if (image.Size != expected) throw new Exception("OCR received wrong full-image or selected-region dimensions");
                            if (dialog.Controls.Find("ocrText", true).OfType<TextBox>().Single().Text.Length != 0) throw new Exception("Opening OCR unexpectedly started recognition");
                        }
                        finally { dialog.Close(); }
                        for (var i = 0; (bool)busy.GetValue(editor)!; i++) { if (i > 100) throw new Exception("OCR busy guard did not release"); await Task.Delay(20); }
                        if (await view.ExecuteScriptAsync("canvas.width === 400 && canvas.height === 250 && objects.length === 0 && activeTool === 'pen'") != "true") throw new Exception("OCR changed drawing state or destructively cropped the source");
                    }
                    await view.ExecuteScriptAsync("cropDraft=null;neoSnapEditor.markKept(imageSnapshot())");
                    if (!await editor.RequestCloseAsync()) throw new Exception("Editor did not close after OCR");
                    Console.WriteLine("direct native OCR button / whole image / selected region / repeated-click guard / non-destructive state: pass");
                    done.TrySetResult();
                }
                catch (Exception error) { done.TrySetException(error); }
                finally
                {
                    foreach (var dialog in Application.OpenForms.OfType<OcrReviewForm>().ToArray()) dialog.Dispose();
                    editor.Dispose();
                }
            };
            Application.Run(editor);
        });
        thread.IsBackground = true; thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return done.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
}
