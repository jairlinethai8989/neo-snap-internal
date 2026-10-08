using System.IO;
using System.Reflection;
using Microsoft.Web.WebView2.WinForms;
using SnapCraft;

internal static class ProductivityDesktopTest
{
    public static Task RunAsync(string root)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var fixture = new Bitmap(800, 500);
            using (var g = Graphics.FromImage(fixture)) { g.Clear(Color.White); using var font = new Font("Segoe UI", 26); g.DrawString("Report and review fixture", font, Brushes.Navy, 20, 20); }
            using var data = new MemoryStream(); fixture.Save(data, System.Drawing.Imaging.ImageFormat.Png); var png = data.ToArray();
            using var report = new ReportForm(new List<ReportImage> { new("First image", "Caption ไทย", png), new("After", "Second image", png) });
            report.Shown += async (_, _) =>
            {
                try
                {
                    var preparing = typeof(ReportForm).GetField("preparing", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    for (var i = 0; (bool)preparing.GetValue(report)!; i++) { if (i > 400) throw new Exception("Report preview timed out"); await Task.Delay(50); }
                    var web = report.Controls.OfType<WebView2>().Single();
                    if (await web.ExecuteScriptAsync("document.images.length === 2 && [...document.images].every(i => i.naturalWidth === 800)") != "true") throw new Exception("Report did not load original images");
                    var pdf = Path.Combine(root, "generated-report.pdf");
                    if (!await web.CoreWebView2.PrintToPdfAsync(pdf) || new FileInfo(pdf).Length < 1000) throw new Exception("Live PDF export failed");
                    using (var snapshot = new Bitmap(report.Width, report.Height))
                    {
                        report.DrawToBitmap(snapshot, new Rectangle(Point.Empty, report.Size));
                        using var webPng = new MemoryStream(); await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, webPng);
                        webPng.Position = 0; using var captured = new Bitmap(webPng); using var graphics = Graphics.FromImage(snapshot);
                        var offset = web.PointToScreen(Point.Empty) - new Size(report.Location); graphics.DrawImageUnscaled(captured, offset); snapshot.Save(Path.Combine(root, "report-ui.png"));
                    }
                    using (var review = new ScrollReviewForm(png, new ScrollCaptureAudit { Joins = new List<int> { 200 } })) { review.Show(); await Task.Delay(100); review.Close(); }
                    using (var compare = new CompareForm(png, png))
                    {
                        compare.Show(); var checkbox = compare.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<CheckBox>().First(); checkbox.Checked = true;
                        for (var i = 0; !(checkbox.Enabled); i++) { if (i > 200) throw new Exception("Difference preview timed out"); await Task.Delay(25); }
                        if (!checkbox.Checked) throw new Exception("Difference preview failed"); compare.Close();
                    }
                    using (var redact = new RedactionForm(png))
                    {
                        redact.Show(); var canvas = redact.Controls.OfType<ImageReviewCanvas>().Single();
                        void Mouse(string method, int x, int y) => typeof(ImageReviewCanvas).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(canvas, new object[] { new MouseEventArgs(MouseButtons.Left, 1, x, y, 0) });
                        Mouse("OnMouseDown", 20, 120); Mouse("OnMouseMove", 140, 200); Mouse("OnMouseUp", 140, 200);
                        if (canvas.Regions.Count != 1 || canvas.Regions[0].Width < 1) throw new Exception("Manual safe-sharing region was lost on mouse release"); redact.Close();
                    }
                    Console.WriteLine("live report preview / original image dimensions / PDF / scroll review / difference form: pass"); done.TrySetResult();
                }
                catch (Exception error) { done.TrySetException(error); }
                finally { report.Close(); }
            };
            Application.Run(report);
        });
        thread.IsBackground = true; thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return done.Task.WaitAsync(TimeSpan.FromSeconds(40));
    }
}
