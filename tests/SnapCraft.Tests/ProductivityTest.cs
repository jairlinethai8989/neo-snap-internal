using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using System.Reflection;
using SnapCraft;

internal static class ProductivityTest
{
    public static async Task RunAsync(string root)
    {
        using var original = new Bitmap(500, 240);
        using (var graphics = Graphics.FromImage(original)) { graphics.Clear(Color.White); using var font = new Font("Segoe UI", 30); graphics.DrawString("SnapZy Test 12345", font, Brushes.Black, 20, 25); }
        using var bytes = new MemoryStream(); original.Save(bytes, ImageFormat.Png); var png = bytes.ToArray();
        var regions = new[] { new Rectangle(10, 10, 440, 100), new Rectangle(-10, 200, 40, 40) };
        var redacted = SafeRedaction.Apply(original, regions);
        using (var input = new MemoryStream(redacted)) using (var image = new Bitmap(input))
        {
            if (image.Size != original.Size || image.GetPixel(30, 40).ToArgb() != Color.Black.ToArgb() || image.GetPixel(470, 150).ToArgb() != Color.White.ToArgb()) throw new Exception("Redaction changed dimensions or left selected pixels exposed");
            if (image.PropertyItems.Any(p => p.Id is 0x010e or 0x013b or 0x9286)) throw new Exception("Safe image preserved descriptive metadata");
        }
        if (original.GetPixel(15, 15).ToArgb() != Color.White.ToArgb()) throw new Exception("Redaction modified the original");
        if (!SafeRedaction.Suggest("test@example.com") || !SafeRedaction.Suggest("0891234567") || SafeRedaction.Suggest("Row 12")) throw new Exception("Sensitive-data suggestions are incorrect");
        using var changed = new Bitmap(original); changed.SetPixel(490, 210, Color.Red);
        using (var difference = ImageDifference.Render(original, changed, 16))
        {
            if (difference.GetPixel(490, 210).ToArgb() != Color.FromArgb(240, 40, 60).ToArgb()) throw new Exception("Difference view missed a changed pixel");
            if (difference.GetPixel(480, 200).ToArgb() == Color.FromArgb(240, 40, 60).ToArgb()) throw new Exception("Difference view marked unchanged pixels");
        }
        using var larger = new Bitmap(550, 240); using (var g = Graphics.FromImage(larger)) g.Clear(Color.White);
        using (var difference = ImageDifference.Render(original, larger, 16)) if (difference.Width != 550) throw new Exception("Different-size comparison was rescaled");
        var images = new[] { new ReportImage("Step <1>", "Thai: ทดสอบ\nCaption", png), new ReportImage("After", "Done", redacted) };
        var html = ReportDocument.Html("<script>alert(1)</script>", "Summary", "Instructions", images);
        if (html.Contains("<script>")) throw new Exception("Report did not escape a user heading");
        var docx = ReportDocument.Word("Report", "Summary", images); await File.WriteAllBytesAsync(Path.Combine(root, "generated-report.docx"), docx);
        using (var document = new ZipArchive(new MemoryStream(docx)))
        {
            foreach (var item in document.Entries.Where(e => e.FullName.EndsWith(".xml") || e.FullName.EndsWith(".rels"))) { using var stream = item.Open(); _ = XDocument.Load(stream); }
            using var first = document.GetEntry("word/media/0.png")!.Open(); using var saved = new MemoryStream(); first.CopyTo(saved); if (!saved.ToArray().SequenceEqual(png)) throw new Exception("Word export reduced original image pixels");
            using var xml = document.GetEntry("word/document.xml")!.Open(); var body = XDocument.Load(xml); if (!body.ToString().Contains("Step &lt;1&gt;")) throw new Exception("Word report lost headings");
        }
        var lines = new[] { new RecognizedLine("Name Value", new Rectangle(0, 0, 150, 20), new[] { new RecognizedWord("Name", new Rectangle(0, 0, 40, 20)), new RecognizedWord("Value", new Rectangle(100, 0, 40, 20)) }) };
        if (OcrService.TableText(lines) != "Name\tValue") throw new Exception("TSV cell gap grouping failed");
        var thaiTableLine = new[] { new RecognizedLine("ทดสอบภาษาไทย 12345", new Rectangle(0, 0, 250, 28), new[]
        {
            new RecognizedWord("ท", new Rectangle(0, 0, 18, 24)), new RecognizedWord("ด", new Rectangle(22, 0, 18, 24)),
            new RecognizedWord("สอบ", new Rectangle(44, 0, 38, 24)), new RecognizedWord("ภาษาไทย", new Rectangle(86, 0, 72, 24)),
            new RecognizedWord("12345", new Rectangle(210, 0, 40, 24))
        }) };
        if (OcrService.TableText(thaiTableLine) != "ทดสอบภาษาไทย\t12345") throw new Exception("TSV output inserted artificial spaces between Thai OCR fragments");
        if (!CaptureCoordinator.StableSamples(original, original) || CaptureCoordinator.StableSamples(original, larger)) throw new Exception("Scroll stability dimensions were not checked");
        using (var moving = new Bitmap(500, 240)) { using var g = Graphics.FromImage(moving); g.Clear(Color.Red); if (CaptureCoordinator.StableSamples(original, moving)) throw new Exception("Moving scroll frame was accepted"); }
        var settingsPath = WebAssets.SettingsPath; var oldSettings = File.Exists(settingsPath) ? await File.ReadAllBytesAsync(settingsPath) : null;
        try
        {
            await CaptureHistory.ClearAsync(); var id = Guid.NewGuid().ToString("N");
            new AppSettings { HistoryEnabled = false }.Save(); await CaptureHistory.SaveAsync(id, png, ".png"); if (CaptureHistory.Entries().Length != 0) throw new Exception("Disabled history stored an image");
            new AppSettings { HistoryEnabled = true, HistoryDays = 7 }.Save(); await CaptureHistory.SaveAsync(id, png, ".png"); var entry = CaptureHistory.Entries().Single();
            var restored = await CaptureHistory.RestoreAsync(entry.FullName); if (!(await File.ReadAllBytesAsync(restored)).SequenceEqual(png)) throw new Exception("History restore changed pixels"); File.Delete(restored);
            File.SetLastWriteTimeUtc(entry.FullName, DateTime.UtcNow.AddDays(-10)); CaptureHistory.Cleanup(7); if (CaptureHistory.Entries().Length != 0) throw new Exception("History retention did not expire old work");
            var protectedPath = WebAssets.NewCapturePath(); await File.WriteAllBytesAsync(protectedPath, png); File.SetLastWriteTimeUtc(protectedPath, DateTime.UtcNow.AddDays(-10)); WebAssets.CleanupCaptures(new[] { protectedPath }); if (!File.Exists(protectedPath)) throw new Exception("Cleanup deleted an active image"); File.Delete(protectedPath);
        }
        finally { await CaptureHistory.ClearAsync(); if (oldSettings is null) File.Delete(settingsPath); else await File.WriteAllBytesAsync(settingsPath, oldSettings); }
        var languages = OcrService.Languages(); Console.WriteLine("OCR languages installed: " + string.Join(", ", languages));
        if (!languages.Contains("th+en")) throw new Exception("Thai/English offline OCR option is unavailable");
        var modelFixture = Environment.GetEnvironmentVariable("SNAPCRAFT_OCR_FIXTURE");
        if (modelFixture is not null)
        {
            Directory.CreateDirectory(OcrModels.DirectoryPath);
            foreach (var code in new[] { "eng", "tha" }) File.Copy(Path.Combine(modelFixture, code + ".traineddata"), Path.Combine(OcrModels.DirectoryPath, code + ".traineddata"), true);
            if (!await OcrModels.ReadyAsync()) throw new Exception("Verified offline OCR models were not accepted");
            var result = await OcrService.ReadAsync(png, "th+en");
            if (!string.Join(" ", result.Select(l => l.Text)).Contains("12345")) throw new Exception("Offline Thai/English OCR missed the generated fixture");
            Console.WriteLine("live offline Thai/English OCR: pass");
            using var thaiImage = new Bitmap(850, 180);
            using (var g = Graphics.FromImage(thaiImage)) { g.Clear(Color.White); using var font = new Font("Tahoma", 32); g.DrawString("ทดสอบภาษาไทย 12345", font, Brushes.Black, 20, 25); }
            using var thaiBytes = new MemoryStream(); thaiImage.Save(thaiBytes, ImageFormat.Png);
            var thai = string.Join(" ", (await OcrService.ReadAsync(thaiBytes.ToArray(), "th+en")).Select(l => l.Text));
            if (!thai.Any(c => c >= '\u0e01' && c <= '\u0e5b') || !thai.Contains("12345")) throw new Exception("Offline OCR failed the Thai fixture");
            if (!thai.Contains("ทดสอบภาษาไทย"))
            {
                var geometry = string.Join(" | ", (await OcrService.ReadAsync(thaiBytes.ToArray(), "th+en")).SelectMany(line => line.Words).Select(word => $"{word.Text}:{word.Bounds}"));
                throw new Exception($"Offline OCR split a continuous Thai phrase: {thai}; {geometry}");
            }
            using var darkThaiImage = new Bitmap(900, 150);
            using (var g = Graphics.FromImage(darkThaiImage))
            {
                g.Clear(Color.FromArgb(24, 24, 24));
                using var font = new Font("Tahoma", 24, FontStyle.Bold);
                g.DrawString("ปรับหน้า OCR แล้วทั้ง Neo Snap และ SnapZy ครับ", font, Brushes.White, 12, 12);
                g.DrawString("อ่านข้อความ/ตาราง และคัดลอกที่ชัดเจน", font, Brushes.White, 12, 68);
            }
            using var darkThaiBytes = new MemoryStream(); darkThaiImage.Save(darkThaiBytes, ImageFormat.Png);
            var darkThaiLines = await OcrService.ReadAsync(darkThaiBytes.ToArray(), "th+en");
            var darkThai = string.Join(" ", darkThaiLines.Select(line => line.Text));
            if (!darkThai.Contains("ปรับหน้า") || !darkThai.Contains("แล้วทั้ง") || !darkThai.Contains("Neo Snap") || !darkThai.Contains("SnapZy") || !darkThai.Contains("อ่านข้อความ"))
            {
                var geometry = string.Join(" | ", darkThaiLines.SelectMany(line => line.Words).Select(word => $"{word.Text}:{word.Bounds}"));
                throw new Exception($"Offline OCR did not preserve Thai/English spacing from a dark screenshot: {darkThai}; {geometry}");
            }
            Console.WriteLine("live offline generated Thai image: pass");
        }
        var english = languages.FirstOrDefault(l => l.StartsWith("en"));
        if (english is not null) { var recognized = await OcrService.ReadAsync(png, english); if (!string.Join(" ", recognized.Select(l => l.Text)).Contains("12345")) throw new Exception("Windows OCR did not read the generated fixture"); Console.WriteLine("live Windows OCR generated English image: pass"); }
        Console.WriteLine("redaction pixels / unchanged source / differences / report XML and original PNG / TSV / scroll stability / opt-in history and retention: pass");
    }
}
