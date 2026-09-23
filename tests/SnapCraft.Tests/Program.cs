using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using SnapCraft;

var root = Path.Combine(AppContext.BaseDirectory, "test-output");
Directory.CreateDirectory(root);
TestStitcher(root);
Console.WriteLine("stitcher: pass");
if (args.Contains("--assets"))
{
    Environment.SetEnvironmentVariable("SNAPCRAFT_DATA_DIR", Path.Combine(root, "app-data"));
    WebAssets.Prepare();
    if (!File.Exists(Path.Combine(WebAssets.WebRoot, "editor.html"))) throw new Exception("Editor asset missing");
    Console.WriteLine("assets: pass");
}
if (args.Contains("--capture"))
{
    Environment.SetEnvironmentVariable("SNAPCRAFT_DATA_DIR", Path.Combine(root, "app-data"));
    WebAssets.Prepare();
    var backend = new CaptureBackend();
    try
    {
        var image = await backend.CaptureDisplayAsync(Screen.PrimaryScreen!.DeviceName);
        using var bitmap = new Bitmap(image);
        Console.WriteLine($"screenshot: {bitmap.Width}x{bitmap.Height}, {new FileInfo(image).Length} bytes");
        if (new FileInfo(image).Length < 10_000) Console.WriteLine("screenshot warning: virtual display appears blank");
    }
    catch (Exception error) { Console.WriteLine($"screenshot unavailable in test session: {error.Message}"); }
    try
    {
        using var video = new VideoSession(IntPtr.Zero, Path.Combine(root, "smoke.mp4"));
        video.Start();
        await Task.Delay(2500);
        var file = await video.StopAsync(true);
        Console.WriteLine($"video: {new FileInfo(file!).Length} bytes");
    }
    catch (Exception error) { Console.WriteLine($"video unavailable in test session: {error.Message}"); }
}

static void TestStitcher(string root)
{
    const int width = 900, fullHeight = 2200, viewport = 500;
    using var full = new Bitmap(width, fullHeight, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(full))
    {
        g.Clear(Color.White);
        using var pen = new Pen(Color.FromArgb(190, 205, 200), 1);
        using var font = new Font("Segoe UI", 12);
        for (var y = 0; y < fullHeight; y += 37)
        {
            g.DrawLine(pen, 0, y, width, y);
            g.DrawString($"Row {y / 37}: scroll capture alignment check", font, Brushes.Black, 24, y + 4);
        }
    }
    using var stitcher = new ScrollStitcher();
    foreach (var offset in new[] { 0, 330, 710, 1050, 1430 })
    {
        using var tile = full.Clone(new Rectangle(0, offset, width, viewport), PixelFormat.Format32bppArgb);
        var result = stitcher.Add(tile);
        if (result != TileResult.Added) throw new Exception($"Expected added at {offset}, got {result}");
    }
    using (var duplicate = full.Clone(new Rectangle(0, 1430, width, viewport), PixelFormat.Format32bppArgb))
        if (stitcher.Add(duplicate) != TileResult.Unchanged) throw new Exception("Duplicate tile was not detected");
    if (Math.Abs(stitcher.TotalHeight - 1930) > 5) throw new Exception($"Wrong stitched height: {stitcher.TotalHeight}");
    var output = Path.Combine(root, "stitched.png");
    stitcher.Save(output);
    using var image = new Bitmap(output);
    if (image.Width != width || image.Height != 1930) throw new Exception("Wrong output dimensions");

    using var pinned = new ScrollStitcher();
    foreach (var offset in new[] { 0, 330, 710, 1050, 1430 })
    {
        using var tile = new Bitmap(width, viewport, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(tile))
        {
            g.DrawImage(full, new Rectangle(0, 40, width, viewport - 40), new Rectangle(0, offset + 40, width, viewport - 40), GraphicsUnit.Pixel);
            g.FillRectangle(Brushes.DarkGreen, 0, 0, width, 40);
            g.DrawString("FIXED HEADER", new Font("Segoe UI", 13), Brushes.White, 10, 6);
        }
        if (pinned.Add(tile) != TileResult.Added) throw new Exception($"Pinned header failed at {offset}");
    }
    if (Math.Abs(pinned.TotalHeight - 1930) > 5) throw new Exception($"Pinned header height wrong: {pinned.TotalHeight}");
    using var unrelated = new Bitmap(width, viewport, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(unrelated)) g.Clear(Color.Red);
    if (pinned.Add(unrelated) != TileResult.Ambiguous) throw new Exception("Unrelated frame was not rejected");
}
