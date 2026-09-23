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
        var maximum = Math.Max(1, (int)(height * 0.86));
        var bestShift = -1;
        var bestScore = 0d;
        var oldRows = RowHashes(oldPixels, width, height);
        var newRows = RowHashes(newPixels, width, height);
        var positions = new Dictionary<uint, List<int>>();
        for (var y = 1; y < height - 1; y++)
        {
            if (oldRows[y] == 0) continue;
            if (!positions.TryGetValue(oldRows[y], out var rows)) positions[oldRows[y]] = rows = new List<int>();
            rows.Add(y);
        }
        var votes = new Dictionary<int, int>();
        for (var y = 2; y < Math.Min(height / 3, 220); y += 2)
        {
            if (newRows[y] == 0 || !positions.TryGetValue(newRows[y], out var matches)) continue;
            foreach (var row in matches)
            {
                var shift = row - y;
                if (shift > 0 && shift <= maximum) votes[shift] = votes.GetValueOrDefault(shift) + 1;
            }
        }
        foreach (var shift in votes.OrderByDescending(pair => pair.Value).Take(20).Select(pair => pair.Key))
        {
            var score = Score(oldPixels, newPixels, width, height, shift);
            if (score > bestScore) { bestScore = score; bestShift = shift; }
        }
        if (bestScore >= 0.88) return bestShift;
        for (var shift = 8; shift <= maximum; shift += 8)
        {
            var score = Score(oldPixels, newPixels, width, height, shift);
            if (score > bestScore) { bestScore = score; bestShift = shift; }
        }
        if (bestShift < 0) return -1;
        for (var shift = Math.Max(1, bestShift - 8); shift <= Math.Min(maximum, bestShift + 8); shift++)
        {
            var score = Score(oldPixels, newPixels, width, height, shift);
            if (score > bestScore) { bestScore = score; bestShift = shift; }
        }
        return bestScore >= 0.76 ? bestShift : -1;
    }

    private static uint[] RowHashes(byte[] pixels, int width, int height)
    {
        var hashes = new uint[height];
        var step = Math.Max(4, width / 120);
        for (var y = 0; y < height; y++)
        {
            var hash = 2166136261u;
            var informative = 0;
            for (var x = 8; x < width - 8; x += step)
            {
                var index = (y * width + x) * 4;
                for (var channel = 0; channel < 3; channel++)
                {
                    var value = pixels[index + channel];
                    hash = unchecked((hash ^ value) * 16777619u);
                    if (value < 235) informative++;
                }
            }
            if (informative >= 5) hashes[y] = hash;
        }
        return hashes;
    }

    private static double Score(byte[] oldPixels, byte[] newPixels, int width, int height, int shift)
    {
        var overlap = height - shift;
        if (overlap < height * 0.12) return 0;
        var xStep = Math.Max(7, width / 90);
        var yStep = Math.Max(3, overlap / 80);
        var matching = 0;
        var total = 0;
        var topMargin = Math.Max(4, Math.Min(100, height / 10));
        for (var y = topMargin; y < overlap - 4; y += yStep)
        {
            var oldRow = (y + shift) * width * 4;
            var newRow = y * width * 4;
            for (var x = 6; x < width - 6; x += xStep)
            {
                var a = oldRow + x * 4;
                var b = newRow + x * 4;
                var dark = oldPixels[a] < 235 || oldPixels[a + 1] < 235 || oldPixels[a + 2] < 235 ||
                           newPixels[b] < 235 || newPixels[b + 1] < 235 || newPixels[b + 2] < 235;
                if (!dark) continue;
                total++;
                if (Math.Abs(oldPixels[a] - newPixels[b]) < 25 &&
                    Math.Abs(oldPixels[a + 1] - newPixels[b + 1]) < 25 &&
                    Math.Abs(oldPixels[a + 2] - newPixels[b + 2]) < 25) matching++;
            }
        }
        return total < 20 ? 0 : (double)matching / total;
    }

    public void Dispose()
    {
        foreach (var tile in tiles) tile.Image.Dispose();
        tiles.Clear();
    }
}
