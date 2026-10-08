using System.IO;
using SnapCraft;

internal static class OcrInterfaceTest
{
    public static Task RunAsync(string root)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var originalLanguage = Localization.CurrentLanguage;
            using var fixture = new Bitmap(700, 180);
            using (var g = Graphics.FromImage(fixture)) { g.Clear(Color.White); using var font = new Font("Segoe UI", 32); g.DrawString("Sample OCR text", font, Brushes.Black, 20, 30); }
            using var stream = new MemoryStream(); fixture.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            using var blank = new Bitmap(240, 100);
            using (var g = Graphics.FromImage(blank)) g.Clear(Color.White);
            using var blankStream = new MemoryStream(); blank.Save(blankStream, System.Drawing.Imaging.ImageFormat.Png);
            using var host = new Form { ShowInTaskbar = false, Opacity = 0, Size = new Size(1, 1) };
            host.Shown += async (_, _) =>
            {
                try
                {
                    foreach (var locale in new[] { "en", "th" })
                    {
                        Localization.SetLanguage(locale);
                        using var form = new OcrReviewForm(stream.ToArray()); form.Show(host); await Task.Delay(80);
                        T Find<T>(string name) where T : Control => form.Controls.Find(name, true).OfType<T>().SingleOrDefault() ?? throw new Exception("Missing themed OCR control: " + name);
                        var output = Find<TextBox>("ocrText"); var copy = Find<Button>("ocrCopy"); var read = Find<Button>("ocrRead");
                        var language = Find<ComboBox>("ocrLanguage"); var plain = Find<RadioButton>("ocrTextMode"); var table = Find<RadioButton>("ocrTableMode");
                        var status = Find<Label>("ocrStatus"); var progress = Find<ProgressBar>("ocrProgress");
                        if (Find<PictureBox>("ocrScanIcon").Image is null || copy.Image is null || read.Image is null) throw new Exception("Themed OCR command icons are missing");
                        if (read.Height != plain.Height || plain.Height != table.Height) throw new Exception("OCR toolbar command heights differ");
                        if (!status.Text.Contains(locale == "th" ? "พร้อม" : "Ready") || output.TextLength != 0 || copy.Enabled) throw new Exception("OCR idle state is not truthful");
                        if (language.Items.Cast<object>().Any(item => item.ToString() == "en-US")) throw new Exception("Language selector still displays raw tags only");
                        output.Text = "Manual text"; table.PerformClick(); output.Text = "Manual\ttable"; plain.PerformClick();
                        if (output.Text != "Manual text") throw new Exception("Switching output mode lost edited text");
                        table.PerformClick(); if (output.Text != "Manual\ttable") throw new Exception("Switching output mode lost edited table"); plain.PerformClick();
                        if (OcrService.Languages().All(code => !code.StartsWith("en"))) throw new Exception("Live OCR UI test requires an installed Windows English recognizer");
                        language.SelectedValue = OcrService.Languages().First(code => code.StartsWith("en"));
                        var sawBusy = false;
                        status.TextChanged += (_, _) => { if (!read.Enabled && output.ReadOnly && progress.Visible && !copy.Enabled && !language.Enabled && !table.Enabled) sawBusy = true; };
                        read.PerformClick();
                        if (!sawBusy) throw new Exception("OCR reading controls were not guarded");
                        if (!read.Enabled) { form.Close(); if (form.IsDisposed) throw new Exception("OCR closed while recognition was in flight"); }
                        for (var i = 0; !read.Enabled; i++) { if (i > 300) throw new Exception("Live OCR UI timed out"); await Task.Delay(30); }
                        if (!output.Text.Contains("Sample", StringComparison.OrdinalIgnoreCase) || !copy.Enabled || progress.Visible || output.ReadOnly) throw new Exception("Live OCR result or final state was not displayed");
                        output.AppendText(" EDITED"); table.PerformClick(); plain.PerformClick();
                        if (!output.Text.EndsWith(" EDITED")) throw new Exception("OCR mode change erased manual corrections");
                        var savedClipboard = new DataObject();
                        var previousClipboard = Clipboard.GetDataObject();
                        var ownedClipboardData = new List<IDisposable>();
                        foreach (var format in previousClipboard?.GetFormats(false) ?? Array.Empty<string>())
                        {
                            var value = previousClipboard!.GetData(format, false);
                            if (value is MemoryStream bytes) { value = new MemoryStream(bytes.ToArray()); ownedClipboardData.Add((IDisposable)value); }
                            else if (value is Image bitmap) { value = bitmap.Clone(); ownedClipboardData.Add((IDisposable)value); }
                            if (value is not null) savedClipboard.SetData(format, false, value);
                        }
                        try
                        {
                            copy.PerformClick();
                            if (Clipboard.GetText() != output.Text || copy.Text != (locale == "th" ? "คัดลอกแล้ว" : "Copied")) throw new Exception("Copy did not acknowledge the actual clipboard write");
                            output.AppendText("!"); if (copy.Text == (locale == "th" ? "คัดลอกแล้ว" : "Copied")) throw new Exception("Copied feedback remained stale after an edit");
                        }
                        finally { Clipboard.SetDataObject(savedClipboard, true); foreach (var owned in ownedClipboardData) owned.Dispose(); }
                        foreach (var size in new[] { new Size(720, 540), new Size(500, 400) })
                        {
                            form.Size = size; await Task.Delay(80);
                            using var shot = new Bitmap(form.Width, form.Height); form.DrawToBitmap(shot, new Rectangle(Point.Empty, shot.Size));
                            shot.Save(Path.Combine(root, $"ocr-{locale}-{size.Width}.png"));
                            VerifyBounds(form);
                        }
                        form.Scale(new SizeF(1.5f, 1.5f)); await Task.Delay(80); VerifyBounds(form);
                        using (var shot = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(shot, new Rectangle(Point.Empty, shot.Size)); shot.Save(Path.Combine(root, $"ocr-{locale}-scaled.png")); }
                        form.Close();
                        using var failed = new OcrReviewForm(new byte[] { 1, 2, 3 }); failed.Show(host);
                        var failedOutput = (TextBox)failed.Controls.Find("ocrText", true).Single(); failedOutput.Text = "Keep my corrections";
                        ((ComboBox)failed.Controls.Find("ocrLanguage", true).Single()).SelectedValue = OcrService.Languages().First(code => code.StartsWith("en"));
                        var failedRead = (Button)failed.Controls.Find("ocrRead", true).Single(); failedRead.PerformClick();
                        for (var i = 0; !failedRead.Enabled; i++) { if (i > 300) throw new Exception("OCR failure state timed out"); await Task.Delay(30); }
                        var failureStatus = (Label)failed.Controls.Find("ocrStatus", true).Single();
                        if (failedOutput.Text != "Keep my corrections" || !failureStatus.Text.Contains(locale == "th" ? "ไม่สำเร็จ" : "failed")) throw new Exception("OCR failure lost existing edits or showed success");
                        failed.Close();
                        using var empty = new OcrReviewForm(blankStream.ToArray()); empty.Show(host);
                        var emptyOutput = (TextBox)empty.Controls.Find("ocrText", true).Single(); emptyOutput.Text = "Previous result";
                        ((ComboBox)empty.Controls.Find("ocrLanguage", true).Single()).SelectedValue = OcrService.Languages().First(code => code.StartsWith("en"));
                        var emptyRead = (Button)empty.Controls.Find("ocrRead", true).Single(); emptyRead.PerformClick();
                        for (var i = 0; !emptyRead.Enabled; i++) { if (i > 300) throw new Exception("Empty OCR state timed out"); await Task.Delay(30); }
                        if (emptyOutput.TextLength != 0 || ((Button)empty.Controls.Find("ocrCopy", true).Single()).Enabled || ((Label)empty.Controls.Find("ocrStatus", true).Single()).Text != (locale == "th" ? "ไม่พบข้อความ" : "No text found")) throw new Exception("Empty OCR showed stale output or success");
                        ((RadioButton)empty.Controls.Find("ocrTableMode", true).Single()).PerformClick();
                        if (emptyOutput.TextLength != 0) throw new Exception("Empty recognition retained stale table output");
                        empty.Close();
                    }
                    Console.WriteLine("themed native OCR / EN-TH / live recognition / editable mode buffers / clipboard feedback / busy and failure states / compact and scaled layouts: pass");
                    done.TrySetResult();
                }
                catch (Exception error) { done.TrySetException(error); }
                finally { foreach (var form in Application.OpenForms.OfType<OcrReviewForm>().ToArray()) form.Dispose(); Localization.SetLanguage(originalLanguage); host.Close(); }
            };
            Application.Run(host);
        });
        thread.IsBackground = true; thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return done.Task.WaitAsync(TimeSpan.FromSeconds(45));
    }

    private static void VerifyBounds(Control parent)
    {
        var controls = parent.Controls.Cast<Control>().Where(c => c.Visible).ToArray();
        foreach (var control in controls)
        {
            if (control.Left < 0 || control.Top < 0 || control.Right > parent.ClientSize.Width + 1 || control.Bottom > parent.ClientSize.Height + 1) throw new Exception($"OCR control clipped: {control.Name} {control.Bounds} inside {parent.GetType().Name} {parent.ClientSize}");
            foreach (var other in controls.Where(c => c != control)) if (control.Bounds.IntersectsWith(other.Bounds)) throw new Exception("OCR controls overlap: " + control.Name + "/" + other.Name);
            VerifyBounds(control);
        }
    }
}
