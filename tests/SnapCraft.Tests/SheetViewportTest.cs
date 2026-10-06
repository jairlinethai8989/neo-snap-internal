using System.Drawing;
using SnapCraft;

internal static class SheetViewportTest
{
    public static void Run()
    {
        foreach (var scale in new[] { 1, 2 })
        {
            using var image = new Bitmap(960 * scale, 720 * scale);
            using (var graphics = Graphics.FromImage(image))
            {
                graphics.Clear(Color.White);
                graphics.FillRectangle(Brushes.Gainsboro, 0, 0, image.Width, 140 * scale);
                graphics.FillRectangle(Brushes.WhiteSmoke, 0, 140 * scale, image.Width, 20 * scale);
                graphics.FillRectangle(Brushes.SeaGreen, 0, 180 * scale, image.Width, 110 * scale);
                using var pen = new Pen(Color.FromArgb(150, 150, 150), scale);
                foreach (var x in new[] { 40, 130, 230, 470, 610, 720, 840 })
                {
                    graphics.DrawLine(pen, x * scale, 160 * scale, x * scale, 180 * scale);
                    graphics.DrawLine(pen, x * scale, 240 * scale, x * scale, image.Height);
                }
                for (var y = 290; y < 720; y += 30) graphics.DrawLine(pen, 0, y * scale, image.Width, y * scale);
            }
            var region = SheetViewportDetector.Find(image) ?? throw new Exception("Canvas sheet bounds not detected");
            if (Math.Abs(region.Top - 160 * scale) > 3 || region.Left != 0 || region.Width != image.Width || region.Bottom != image.Height)
                throw new Exception($"Sheet framing changed scale/columns or lost headers: {region}");
            using var cropped = image.Clone(region, image.PixelFormat);
            if (SheetViewportDetector.Find(cropped) is not null) throw new Exception("Already-framed sheet was cropped again");
        }
        using var ordinary = new Bitmap(960, 720);
        using (var graphics = Graphics.FromImage(ordinary))
        {
            graphics.Clear(Color.White);
            graphics.FillRectangle(Brushes.SeaGreen, 0, 120, 960, 100);
            graphics.DrawString("Document, not a spreadsheet", new Font("Segoe UI", 18), Brushes.Black, 30, 270);
        }
        if (SheetViewportDetector.Find(ordinary) is not null) throw new Exception("Ordinary document falsely cropped as a sheet");
    }
}
