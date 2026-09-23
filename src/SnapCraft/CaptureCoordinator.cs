using System.Drawing.Imaging;

namespace SnapCraft;

internal enum CaptureKind { Area, Window, Scroll }

internal sealed class CaptureCoordinator
{
    private readonly CaptureBackend backend = new();

    public async Task<string?> CaptureAsync(CaptureKind kind, int delayMs)
    {
        var selection = SelectionOverlay.Choose(kind == CaptureKind.Window ? SelectionMode.Window : SelectionMode.Region);
        if (selection is null) return null;
        if (kind == CaptureKind.Scroll && selection.WindowHandle == IntPtr.Zero)
            throw new InvalidOperationException("ไม่พบหน้าต่างใต้กรอบที่เลือก");
        if (delayMs > 0) await Task.Delay(delayMs);
        await Task.Delay(120);
        return kind switch
        {
            CaptureKind.Area => await CaptureAreaAsync(selection.Region),
            CaptureKind.Window => await backend.CaptureWindowAsync(selection.WindowHandle),
            CaptureKind.Scroll => await CaptureScrollAsync(selection),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private async Task<string> CaptureAreaAsync(Rectangle region)
    {
        var screen = Screen.FromRectangle(region);
        if (!screen.Bounds.Contains(region)) throw new InvalidOperationException("กรอบจับภาพต้องอยู่ภายในจอเดียวกัน");
        var raw = await backend.CaptureDisplayAsync(screen.DeviceName);
        try
        {
            using var bitmap = new Bitmap(raw);
            var relative = new Rectangle(region.X - screen.Bounds.X, region.Y - screen.Bounds.Y, region.Width, region.Height);
            var crop = ScaleRegion(relative, screen.Bounds.Size, bitmap.Size);
            using var image = bitmap.Clone(crop, PixelFormat.Format32bppArgb);
            var path = WebAssets.NewCapturePath();
            image.Save(path, ImageFormat.Png);
            return path;
        }
        finally { File.Delete(raw); }
    }

    private async Task<string?> CaptureScrollAsync(CaptureSelection selection)
    {
        using var stitcher = new ScrollStitcher();
        using var progress = new ScrollProgressForm(selection.Region);
        var finish = false;
        using var escape = new EscapeHook(() => finish = true);
        progress.Show();
        progress.UpdateProgress(0, 0);
        var point = new Point(selection.Region.Left + selection.Region.Width / 2, selection.Region.Top + selection.Region.Height / 2);
        var unchanged = 0;
        var ambiguous = 0;
        var forceWheel = false;
        var started = DateTime.UtcNow;
        try
        {
            var attempt = 0;
            while (stitcher.Count < 80)
            {
                if (progress.CancelRequested) return null;
                if (finish || progress.FinishRequested) break;
                if (attempt > 0)
                {
                    if (!progress.Manual)
                    {
                        var usedAutomation = !forceWheel && NativeInput.TryAutomationScroll(point);
                        if (!usedAutomation) NativeInput.WheelDown(point);
                    }
                    await Task.Delay(progress.Manual ? 550 : 220);
                }
                using var tile = await CaptureWindowRegionAsync(selection.WindowHandle, selection.Region);
                var result = stitcher.Add(tile);
                if (result == TileResult.Added)
                {
                    unchanged = 0;
                    ambiguous = 0;
                    progress.UpdateProgress(stitcher.Count, stitcher.TotalHeight);
                }
                else if (result == TileResult.Unchanged)
                {
                    unchanged++;
                    if (unchanged >= 2) forceWheel = true;
                    if (unchanged >= 4 && !progress.Manual) break;
                }
                else if (result == TileResult.Ambiguous)
                {
                    ambiguous++;
                    if (ambiguous >= 2 && !progress.Manual)
                    {
                        progress.UpdateProgress(stitcher.Count, stitcher.TotalHeight, "เลื่อนเอง แล้วกด เสร็จ หรือ Esc");
                        progress.SetManual(true);
                    }
                }
                else break;
                if (!progress.Manual && DateTime.UtcNow - started > TimeSpan.FromMinutes(2)) break;
                if (progress.Manual && DateTime.UtcNow - started > TimeSpan.FromMinutes(7)) break;
                attempt++;
            }
            if (progress.CancelRequested || stitcher.Count == 0) return null;
            var output = WebAssets.NewCapturePath();
            stitcher.Save(output);
            return output;
        }
        finally { progress.Close(); }
    }

    private async Task<Bitmap> CaptureWindowRegionAsync(IntPtr handle, Rectangle region)
    {
        var raw = await backend.CaptureWindowAsync(handle);
        try
        {
            using var bitmap = new Bitmap(raw);
            var bounds = NativeInput.VisibleWindowBounds(handle);
            if (!bounds.Contains(region)) throw new InvalidOperationException("กรอบที่เลือกอยู่นอกหน้าต่างหรือหน้าต่างขยับระหว่างจับภาพ");
            var relative = new Rectangle(region.X - bounds.X, region.Y - bounds.Y, region.Width, region.Height);
            var crop = ScaleRegion(relative, bounds.Size, bitmap.Size);
            return bitmap.Clone(crop, PixelFormat.Format32bppArgb);
        }
        finally { File.Delete(raw); }
    }

    private static Rectangle ScaleRegion(Rectangle relative, Size source, Size image)
    {
        var x = (int)Math.Round((double)relative.X * image.Width / source.Width);
        var y = (int)Math.Round((double)relative.Y * image.Height / source.Height);
        var width = (int)Math.Round((double)relative.Width * image.Width / source.Width);
        var height = (int)Math.Round((double)relative.Height * image.Height / source.Height);
        var crop = Rectangle.Intersect(new Rectangle(x, y, width, height), new Rectangle(Point.Empty, image));
        if (crop.Width < 20 || crop.Height < 20) throw new InvalidOperationException("พื้นที่จับภาพเล็กเกินไป");
        return crop;
    }
}
