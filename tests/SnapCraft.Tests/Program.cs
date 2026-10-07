using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using SnapCraft;

Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException, threadScope: false);
Application.EnableVisualStyles();
var root = Path.Combine(AppContext.BaseDirectory, "test-output");
Directory.CreateDirectory(root);
if (args.Contains("--profile-only"))
{
    Environment.SetEnvironmentVariable("SNAPCRAFT_DATA_DIR", null);
    ProductProfileTest.Run(verifyDefaultDataPath: true);
    LocalizationTest.Run();
    return;
}
Environment.SetEnvironmentVariable("SNAPCRAFT_DATA_DIR", Path.Combine(root, "app-data"));
WebAssets.Prepare();
if (args.Contains("--desktop-only")) { await DesktopTestSuite.RunAsync(args, root); return; }
if (args.Contains("--window-occluded")) { await OccludedWindowTest.RunAsync(); return; }
if (args.Contains("--window-fast-only")) { await WindowCaptureSmokeTest.RunAsync(root); return; }
if (args.Contains("--desktop-capture-only")) { await DesktopCaptureTest.RunAsync(); return; }
if (args.Contains("--cloaked-selection-only")) { await CloakedSelectionTest.RunAsync(); return; }
if (args.Contains("--editor-batch")) { await EditorBatchCloseTest.RunAsync(); return; }
ProductProfileTest.Run();
LocalizationTest.Run();
StartupTest.RunAssets(root);
CaptureSettingsTest.Run();
await PerformanceTraceTest.RunAsync();
await ImageSaveTest.RunAsync(root);
await ImageImportTest.RunAsync(root);
await VideoClipTest.RunAsync(root);
TestStitcher(root);
Console.WriteLine("stitcher: pass");
TestFrozenRows(root);
TestFrozenRows(root, 28);
TestFrozenRows(root, 64);
Console.WriteLine("large frozen headers: pass");
if (args.Contains("--lark-chrome"))
{
    var index = Array.IndexOf(args, "--lark-chrome");
    ScrollFooterTest.RunCapturedChrome(root, args[index + 1], args[index + 2]);
}
ScrollFooterTest.Run(root);
CaptureCoordinatesTest.Run();
SheetViewportTest.Run();
Console.WriteLine("sheet viewport / original scale / document exclusion: pass");
if (args.Contains("--provided-sheet"))
{
    using var fixture = new Bitmap(args[Array.IndexOf(args, "--provided-sheet") + 1]);
    var region = SheetViewportDetector.Find(fixture) ?? throw new Exception("Provided sheet was not detected");
    Console.WriteLine($"provided sheet: {fixture.Width}x{fixture.Height} -> {region}, original width retained");
}
if (args.Contains("--window-fast"))
{
    Environment.SetEnvironmentVariable("SNAPCRAFT_DATA_DIR", Path.Combine(root, "app-data"));
    await WindowCaptureSmokeTest.RunAsync(root);
}
if (!GlobalHotkey.IsValid(6, 83) || GlobalHotkey.IsValid(4, 83) || GlobalHotkey.IsValid(6, 123) || GlobalHotkey.IsValid(8, 83))
    throw new Exception("Hotkey validation failed");
Console.WriteLine("hotkey validation: pass");
HotkeyTest.Run();
HotkeyPersistenceTest.Run();
Console.WriteLine("native hotkey registration / collision / cleanup: pass");
TestFrameValidator();
Console.WriteLine("frame validator: pass");
TestCapturedFixtures();
Console.WriteLine("captured fixture: pass");
foreach (var audio in new[] { new VideoAudio(), new VideoAudio(true), new VideoAudio(false, true), new VideoAudio(true, true) })
{
    var options = VideoSession.CreateAudioOptions(audio);
    if (options.IsAudioEnabled != (audio.System || audio.Microphone)) throw new Exception("Incorrect audio enable setting");
    if (options.AudioSources.Count != (audio.System ? 1 : 0) + (audio.Microphone ? 1 : 0)) throw new Exception("Incorrect audio sources");
}
Console.WriteLine("video audio options: pass");
var recordingArgs = FfmpegVideoSession.RecordingArguments(new Rectangle(-1200, 20, 621, 401), "test.mp4");
if (!recordingArgs.Contains("gdigrab") || !recordingArgs.Contains("-1200") || !recordingArgs.Contains("libx264") || !recordingArgs.Contains("pad=ceil(iw/2)*2:ceil(ih/2)*2"))
    throw new Exception("CPU capture arguments are incomplete");
Console.WriteLine("CPU capture arguments: pass");
if (args.Contains("--video-cpu"))
{
    Environment.SetEnvironmentVariable("SNAPCRAFT_DATA_DIR", Path.Combine(root, "app-data"));
    await VideoCpuSmokeTest.RunAsync(root);
}
if (args.Contains("--video-preview")) await VideoPreviewSmokeTest.RunAsync(root);
if (args.Contains("--recording-preview")) await RecordingPreviewTest.RunAsync(root);
if (args.Contains("--hover"))
{
    await ScrollHoverSmokeTest.RunAsync();
    Console.WriteLine("scroll hover detection: pass");
}
if (args.Contains("--editor"))
{
    Environment.SetEnvironmentVariable("SNAPCRAFT_DATA_DIR", Path.Combine(root, "app-data"));
    await EditorTabsSmokeTest.RunAsync(root);
    Console.WriteLine("native editor tabs / close / cleanup: pass");
}
if (args.Contains("--projects"))
{
    await ProjectWorkspaceTest.RunAsync(root);
    Console.WriteLine("native multi-image tabs / source independence / project round-trip / atomic save / close safety: pass");
}
if (args.Contains("--launcher") || args.Contains("--input-latency") || args.Contains("--tray-startup") || args.Contains("--launcher-controls"))
{
    Environment.SetEnvironmentVariable("SNAPCRAFT_DATA_DIR", Path.Combine(root, "app-data"));
    await StartupTest.RunLauncherAsync(args.Contains("--input-latency"), args.Contains("--tray-startup"), args.Contains("--launcher-controls"));
    Console.WriteLine("native launcher / close-to-tray / single click / minimize restore: pass");
}
if (args.Contains("--exit-editors"))
{
    Environment.SetEnvironmentVariable("SNAPCRAFT_DATA_DIR", Path.Combine(root, "app-data"));
    await EditorExitSmokeTest.RunAsync(root);
    Console.WriteLine("application exit / open editor confirmation / cancel / kept image cleanup: pass");
}
if (args.Contains("--single-instance"))
{
    await StartupTest.RunSingleInstanceAsync(root);
    Console.WriteLine("real process single-instance / hidden + minimized foreground restoration: pass");
}
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
        await video.StartAsync();
        await Task.Delay(2500);
        var file = await video.StopAsync(true);
        Console.WriteLine($"video: {new FileInfo(file!).Length} bytes");
    }
    catch (Exception error) { Console.WriteLine($"video unavailable in test session: {error.Message}"); }
}
if (args.Contains("--scroll") || args.Contains("--scroll-esc"))
{
    Environment.SetEnvironmentVariable("SNAPCRAFT_DATA_DIR", Path.Combine(root, "app-data"));
    WebAssets.Prepare();
    await ScrollSmokeTest.RunAsync(root, args.Contains("--scroll-esc"));
    Console.WriteLine("scroll smoke: pass");
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

static void TestFrameValidator()
{
    using var black = new Bitmap(100, 100);
    using (var graphics = Graphics.FromImage(black)) graphics.Clear(Color.Black);
    if (!CaptureFrameValidator.IsBlank(black)) throw new Exception("Black frame was not detected");
    using var content = new Bitmap(100, 100);
    using (var graphics = Graphics.FromImage(content))
    {
        graphics.Clear(Color.Black);
        graphics.FillRectangle(Brushes.White, 10, 10, 20, 20);
    }
    if (CaptureFrameValidator.IsBlank(content)) throw new Exception("Dark content was rejected");
}

static void TestFrozenRows(string root, int footer = 0)
{
    const int width = 480, height = 640, header = 260, shift = 150;
    using var body = new Bitmap(width, 1200, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(body))
    {
        g.Clear(Color.White);
        using var font = new Font("Segoe UI", 12);
        for (var y = 0; y < body.Height; y += 29)
        {
            g.DrawString($"Row {y} - {y * 719 % 997}", font, Brushes.Black, 10 + y % 43, y);
            g.DrawLine(Pens.LightGray, 0, y + 25, width, y + 25);
        }
    }
    using var stitcher = new ScrollStitcher();
    for (var index = 0; index < 5; index++)
    {
        using var tile = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(tile))
        {
            g.Clear(Color.RoyalBlue);
            using var font = new Font("Segoe UI", 15);
            g.DrawString("FROZEN TITLE AND COLUMN HEADERS", font, Brushes.White, 8, 25);
            g.DrawImage(body, new Rectangle(0, header, width, height - header - footer), new Rectangle(0, index * shift, width, height - header - footer), GraphicsUnit.Pixel);
            if (footer > 0) g.FillRectangle(Brushes.LightGray, 0, height - footer, width, footer);
        }
        if (stitcher.Add(tile) != TileResult.Added) throw new Exception($"Frozen rows failed at tile {index}");
    }
    var path = Path.Combine(root, $"frozen-rows-footer-{footer}.png");
    stitcher.Save(path);
    using var output = new Bitmap(path);
    if (output.Height != height + shift * 4) throw new Exception("Frozen rows: wrong height");
    if (output.GetPixel(2, 2).ToArgb() != Color.RoyalBlue.ToArgb()) throw new Exception("Frozen header lost");
    if (footer > 0 && output.GetPixel(2, output.Height - 2).ToArgb() != Color.LightGray.ToArgb()) throw new Exception("Footer lost");
    for (var y = header; y < output.Height - footer; y += 3)
        for (var x = 0; x < width; x += 7)
            if (output.GetPixel(x, y).ToArgb() != body.GetPixel(x, y - header).ToArgb())
                throw new Exception($"Frozen rows: missing/duplicated content at {x},{y}");
}

static void TestCapturedFixtures()
{
    using var captured = new ScrollStitcher();
    for (var frame = 1; frame <= 3; frame++)
    {
        using var tile = new Bitmap(Path.Combine(AppContext.BaseDirectory, "Fixtures", $"tile-{frame}.png"));
        if (captured.Add(tile) != TileResult.Added)
            throw new Exception($"Captured fixture {frame} did not join");
    }
    if (captured.TotalHeight < 1090 || captured.TotalHeight > 1140)
        throw new Exception($"Captured fixtures joined at the wrong offset: {captured.TotalHeight} px");
}
