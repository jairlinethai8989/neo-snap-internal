using SnapCraft;

internal static class CaptureCoordinatesTest
{
    public static void Run()
    {
        var screen = new Rectangle(-1920, -200, 1920, 1080);
        var region = new Rectangle(-1819, -97, 701, 403);
        foreach (var scale in new[] { 1d, 1.25, 1.5, 2 })
        {
            var display = new Size((int)(screen.Width * scale), (int)(screen.Height * scale));
            var actual = CaptureBackend.MapDisplayRegion(region, screen, display);
            var expected = Rectangle.FromLTRB((int)Math.Round(101 * scale), (int)Math.Round(103 * scale),
                (int)Math.Round(802 * scale), (int)Math.Round(506 * scale));
            if (actual != expected || !new Rectangle(Point.Empty, display).Contains(actual))
                throw new Exception($"GPU crop mapping failed at scale {scale}: {actual}, expected {expected}");
            if (scale == 1 && actual.Size != region.Size) throw new Exception("Native crop dimensions changed");
            if (CaptureBackend.MapDisplayRegion(screen, screen, display) != new Rectangle(Point.Empty, display))
                throw new Exception("Full display crop does not preserve all native pixels");
        }
        Console.WriteLine("capture coordinate mapping / negative monitor origin / source pixel dimensions: pass");
    }
}
