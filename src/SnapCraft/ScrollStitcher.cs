using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SnapCraft;

internal enum TileResult { Added, Unchanged, Ambiguous, Limit }

internal sealed class ScrollStitcher : IDisposable
{
    private sealed record Tile(Bitmap Image, int Shift);
    private readonly List<Tile> tiles = new();
    private byte[]? previousPixels;
    private int width;
    private int height;
    public int Count => tiles.Count;
    public int TotalHeight { get; private set; }

    public TileResult Add(Bitmap input)
    {
        using var normalized = new Bitmap(input.Width, input.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(normalized)) graphics.DrawImageUnscaled(input, 0, 0);
        var pixels = ReadPixels(normalized);
        if (tiles.Count == 0)
        {
            width = normalized.Width;
            height = normalized.Height;
            TotalHeight = height;
            previousPixels = pixels;
            tiles.Add(new Tile((Bitmap)normalized.Clone(), 0));
            return TileResult.Added;
        }
        if (normalized.Width != width || normalized.Height != height)
            throw new InvalidOperationException("ขนาดพื้นที่จับภาพเปลี่ยนระหว่างเลื่อน กรุณาเลือกพื้นที่ใหม่");
        var shift = FindShift(previousPixels!, pixels, width, height);
        if (shift == 0) return TileResult.Unchanged;
        if (shift < 0) return TileResult.Ambiguous;
        if (TotalHeight + shift > 30000 || (long)width * (TotalHeight + shift) > 100_000_000)
            return TileResult.Limit;
        tiles.Add(new Tile((Bitmap)normalized.Clone(), shift));
        TotalHeight += shift;
        previousPixels = pixels;
        return TileResult.Added;
    }

    public void Save(string path)
    {
        if (tiles.Count == 0) throw new InvalidOperationException("ยังไม่มีภาพสำหรับต่อ");
        using var output = new Bitmap(width, TotalHeight, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(output))
        {
            graphics.DrawImageUnscaled(tiles[0].Image, 0, 0);
            var bottom = height;
            foreach (var tile in tiles.Skip(1))
            {
                var source = new Rectangle(0, height - tile.Shift, width, tile.Shift);
                graphics.DrawImage(tile.Image, new Rectangle(0, bottom, width, tile.Shift), source, GraphicsUnit.Pixel);
                bottom += tile.Shift;
            }
        }
        output.Save(path, ImageFormat.Png);
    }

    private static byte[] ReadPixels(Bitmap bitmap)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var buffer = new byte[bitmap.Width * bitmap.Height * 4];
            Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);
            return buffer;
        }
        finally { bitmap.UnlockBits(data); }
    }

    private static int FindShift(byte[] oldPixels, byte[] newPixels, int width, int height)
    {
        var same = Score(oldPixels, newPixels, width, height, 0);
        if (same > 0.985) return 0;
        var maximum = Math.Max(1, Math.Min((int)(height * 0.86), height - Math.Max(120, height / 5)));
        var bestShift = -1;
        var bestScore = 0d;
        var scores = new double[maximum + 1];
        for (var shift = 1; shift <= maximum; shift++)
        {
            var score = Score(oldPixels, newPixels, width, height, shift);
            scores[shift] = score;
            if (score > bestScore) { bestScore = score; bestShift = shift; }
        }
        if (bestScore < 0.88) return -1;
        var competingScore = 0d;
        for (var shift = 1; shift <= maximum; shift++)
        {
            if (Math.Abs(shift - bestShift) > Math.Max(5, height / 50))
                competingScore = Math.Max(competingScore, scores[shift]);
        }
        return bestScore - competingScore >= 0.001 ? bestShift : -1;
    }

    private static double Score(byte[] oldPixels, byte[] newPixels, int width, int height, int shift)
    {
        var overlap = height - shift;
        if (overlap < height * 0.12) return 0;
        var xStep = Math.Max(2, width / 160);
        var yStep = Math.Max(2, overlap / 140);
        double error = 0;
        double weightTotal = 0;
        var topMargin = Math.Max(4, Math.Min(100, height / 10));
        for (var y = topMargin; y < overlap - 4; y += yStep)
        {
            var oldRow = (y + shift) * width * 4;
            var newRow = y * width * 4;
            for (var x = 2; x < width - 2; x += xStep)
            {
                var a = oldRow + x * 4;
                var b = newRow + x * 4;
                var dark = oldPixels[a] < 235 || oldPixels[a + 1] < 235 || oldPixels[a + 2] < 235 ||
                           newPixels[b] < 235 || newPixels[b + 1] < 235 || newPixels[b + 2] < 235;
                var weight = dark ? 4 : 1;
                var difference = (Math.Abs(oldPixels[a] - newPixels[b]) +
                                  Math.Abs(oldPixels[a + 1] - newPixels[b + 1]) +
                                  Math.Abs(oldPixels[a + 2] - newPixels[b + 2])) / 3.0;
                error += weight * difference;
                weightTotal += weight;
            }
        }
        return weightTotal < 100 ? 0 : 1 - error / (255 * weightTotal);
    }

    public void Dispose()
    {
        foreach (var tile in tiles) tile.Image.Dispose();
        tiles.Clear();
    }
}
