using System.Drawing.Imaging;
using System.IO;
using SnapCraft;

internal static class ImageImportTest
{
    public static async Task RunAsync(string root)
    {
        foreach (var format in new[] { ImageFormat.Png, ImageFormat.Jpeg, ImageFormat.Bmp })
        {
            var source = Path.Combine(root, $"import-{format.Guid}.image");
            using (var bitmap = new Bitmap(320, 170))
            {
                using var g = Graphics.FromImage(bitmap);
                g.Clear(Color.Red);
                bitmap.Save(source, format);
            }
            var bytes = File.ReadAllBytes(source);
            var imported = await ImageImport.CopyAsync(source);
            if (imported == source) throw new Exception("Import must own a temporary copy.");
            using (var bitmap = new Bitmap(imported))
            {
                if (bitmap.Width != 320 || bitmap.Height != 170 || bitmap.GetPixel(100, 100).R < 240)
                    throw new Exception("Import changed native dimensions or pixels.");
            }
            File.Delete(imported);
            if (!bytes.SequenceEqual(File.ReadAllBytes(source))) throw new Exception("Source image was modified.");
        }
        var invalid = Path.Combine(root, "not-an-image.txt");
        File.WriteAllText(invalid, "not an image");
        var before = Directory.GetFiles(WebAssets.Captures).Length;
        try { await ImageImport.CopyAsync(invalid); throw new Exception("Invalid image accepted."); }
        catch (InvalidDataException) { }
        if (Directory.GetFiles(WebAssets.Captures).Length != before) throw new Exception("Failed import leaked temporary files.");
        Console.WriteLine("image import: native pixels / PNG JPEG BMP / source preservation / invalid cleanup: pass");
    }
}
