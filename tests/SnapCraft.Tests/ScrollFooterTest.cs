using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using SnapCraft;

internal static class ScrollFooterTest
{
    public static void Run(string root)
    {
        foreach (var (name, footerHeight, background) in new[]
        {
            ("table-without-footer", 0, Color.White),
            ("white-scrollbar", 12, Color.White),
            ("white-scrollbar-status", 42, Color.White),
            ("light-scrollbar-status", 64, Color.FromArgb(248, 249, 250))
        })
            Verify(root, name, footerHeight, background);
        Verify(root, "lark-blended-separator", 42, Color.FromArgb(245, 246, 247), 1);
        Verify(root, "lark-blended-separator-2px", 64, Color.FromArgb(245, 246, 247), 2);
        Verify(root, "scrolling-light-grid-without-footer", 0, Color.White, lightGrid: true);
        Verify(root, "footer-changing-first-pair", 42, Color.White, initialNoiseFrames: 2);
        Verify(root, "footer-changing-first-three", 64, Color.FromArgb(248, 249, 250), initialNoiseFrames: 3);
        Verify(root, "footer-changing-thumb-first-pair", 42, Color.White, initialNoiseFrames: 2, thumbOnly: true);
        Verify(root, "footer-initially-hidden", 42, Color.White, hiddenFooterFrames: 2);
        Verify(root, "variable-row-heights-without-footer", 0, Color.White, variableRows: true);
        Verify(root, "variable-row-heights-with-late-footer", 42, Color.White, hiddenFooterFrames: 2, variableRows: true);
    }

    public static void RunCapturedChrome(string root, string beforePath, string afterPath)
    {
        using var before = new Bitmap(beforePath);
        using var after = new Bitmap(afterPath);
        // Manually aligned chrome from the reported screenshots, not their private cell data.
        using var first = before.Clone(new Rectangle(0, 700, 1910, 45), PixelFormat.Format32bppArgb);
        using var last = (Bitmap)first.Clone();
        using (var g = Graphics.FromImage(last))
            g.DrawImage(after, new Rectangle(0, 0, 1910, 19), new Rectangle(2, 721, 1910, 19), GraphicsUnit.Pixel);
        Verify(root, "captured-lark-scrollbar", 45, Color.White, capturedChrome: new[] { first, last });
    }

    private static void Verify(string root, string name, int footerHeight, Color background, int separator = 0,
        bool lightGrid = false, Bitmap[]? capturedChrome = null, int initialNoiseFrames = 0, bool thumbOnly = false,
        int hiddenFooterFrames = 0, bool variableRows = false)
    {
        const int height = 640, header = 120, shift = 150;
        var width = capturedChrome?[0].Width ?? 900;
        using var body = new Bitmap(width, 1600, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(body))
        {
            g.Clear(Color.White);
            using var font = new Font("Segoe UI", 11);
            using var grid = new Pen(lightGrid ? Color.FromArgb(222, 222, 223) : Color.Gray, lightGrid ? 3 : 1);
            for (var y = 0; y < body.Height; y += variableRows ? 24 + y * 13 % 90 : 31)
            {
                g.DrawString($"Row {y / 31}: {y * 719 % 997}", font, Brushes.Black, 12 + y % 43, y + 2);
                g.DrawString($"Content {y * 113 % 10007}", font, Brushes.Black, 340 + y % 29, y + 2);
                g.DrawString($"Notes {y * 317 % 10009}", font, Brushes.Black, 620 + y % 19, y + 2);
                g.DrawLine(grid, 0, y + 29, width, y + 29);
            }
            for (var x = 0; x < width; x += 100) g.DrawLine(Pens.Gray, x, 0, x, body.Height);
        }
        using var footer = new Bitmap(width, Math.Max(1, footerHeight), PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(footer))
        {
            g.Clear(background);
            if (separator > 0) g.FillRectangle(Brushes.White, 0, 0, width, 14);
            // Light Lark chrome: no full-width dark background or separator.
            g.FillRectangle(Brushes.LightGray, 8, 3, width * 3 / 5, 6);
            if (footerHeight > 12)
            {
                using var font = new Font("Segoe UI", 10);
                g.DrawString("Sheet 1     +", font, Brushes.DimGray, 12, 17);
                g.DrawString("100%", font, Brushes.DimGray, width - 70, 17);
            }
        }
        using var cleanFooter = (Bitmap)footer.Clone();
        using var stitcher = new ScrollStitcher();
        for (var index = 0; index < 5; index++)
        {
            using (var g = Graphics.FromImage(footer))
            {
                g.DrawImageUnscaled(cleanFooter, 0, 0);
                if (index < hiddenFooterFrames) g.Clear(background);
                if (index < initialNoiseFrames)
                {
                    // A changing light status bar isolates delayed footer recognition;
                    // a whole brightly animated bar can legitimately reject alignment.
                    var noise = footerHeight > 42
                        ? (index % 2 == 0 ? Color.FromArgb(200, 205, 210) : Color.FromArgb(232, 236, 240))
                        : (index % 2 == 0 ? Color.Coral : Color.CornflowerBlue);
                    using var changing = new SolidBrush(noise);
                    g.FillRectangle(changing, 0, thumbOnly ? 3 : 0, width * (thumbOnly ? .65f : 1f), thumbOnly ? 6 : footerHeight);
                }
            }
            if (capturedChrome is not null)
            {
                using var g = Graphics.FromImage(footer);
                g.DrawImageUnscaled(capturedChrome[index % capturedChrome.Length], 0, 0);
            }
            if (separator > 0)
            {
                // Lark's separator bleeds canvas colors even though the scrollbar stays fixed.
                using var g = Graphics.FromImage(footer);
                using var gray = new SolidBrush(Color.FromArgb(222, 222, 223));
                using var tint = new SolidBrush(index % 2 == 0 ? Color.FromArgb(222, 180, 150) : Color.FromArgb(180, 210, 170));
                foreach (var y in new[] { 0, 14 })
                {
                    g.FillRectangle(gray, 0, y, width, separator);
                    g.FillRectangle(tint, 350, y, 55, separator);
                }
            }
            using var tile = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(tile))
            {
                g.Clear(Color.SeaGreen);
                g.DrawImage(body, new Rectangle(0, header, width, height - header - footerHeight),
                    new Rectangle(0, index * shift, width, height - header - footerHeight), GraphicsUnit.Pixel);
                if (footerHeight > 0) g.DrawImageUnscaled(footer, 0, height - footerHeight);
            }
            var beforeHeight = stitcher.TotalHeight;
            var result = stitcher.Add(tile);
            if (result != TileResult.Added) throw new Exception($"{name}: tile {index} did not join ({result})");
            if (index > 0 && stitcher.TotalHeight - beforeHeight != shift)
                throw new Exception($"{name}: tile {index} shifted {stitcher.TotalHeight - beforeHeight}, expected {shift}");
        }
        var path = Path.Combine(root, $"{name}.png");
        stitcher.Save(path);
        using var output = new Bitmap(path);
        if (output.Width != width || output.Height != 1240) throw new Exception($"{name}: wrong dimensions {output.Size}");
        using var expected = new Bitmap(width, output.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(expected))
        {
            g.Clear(Color.SeaGreen);
            using var content = body.Clone(new Rectangle(0, 0, width, output.Height - header - footerHeight), PixelFormat.Format32bppArgb);
            g.DrawImageUnscaled(content, 0, header);
            if (footerHeight > 0) g.DrawImageUnscaled(footer, 0, output.Height - footerHeight);
        }
        var actualPixels = Pixels(output);
        var expectedPixels = Pixels(expected);
        for (var i = 0; i < actualPixels.Length; i++)
            if (actualPixels[i] != expectedPixels[i])
                throw new Exception($"{name}: repeated footer, changed header or missing body pixels at {i / 4 % width},{i / 4 / width}");
        Console.WriteLine($"{name}: exact body pixels / footer appears once: pass");
    }

    private static byte[] Pixels(Bitmap image)
    {
        var data = image.LockBits(new Rectangle(Point.Empty, image.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var pixels = new byte[image.Width * image.Height * 4];
            for (var y = 0; y < image.Height; y++)
                Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * image.Width * 4, image.Width * 4);
            return pixels;
        }
        finally { image.UnlockBits(data); }
    }
}
