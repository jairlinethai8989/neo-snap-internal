using System.Reflection;
using System.IO;
using SnapCraft;

internal static class ImageSaveTest
{
    public static async Task RunAsync(string root)
    {
        var writer = typeof(EditorHubForm).GetMethod("WritePngAsync", BindingFlags.Static | BindingFlags.NonPublic);
        if (writer is null) throw new Exception("PNG writer must preserve an existing output until export write succeeds");
        var directory = Path.Combine(root, "image-save-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var output = Path.Combine(directory, "saved.png");
        using var image = new System.Drawing.Bitmap(40, 30);
        using var data = new MemoryStream();
        image.Save(data, System.Drawing.Imaging.ImageFormat.Png);
        var bytes = data.ToArray();
        async Task Write(string path) => await (Task)writer.Invoke(null, new object[] { path, bytes })!;
        await Write(output);
        if (!File.ReadAllBytes(output).SequenceEqual(bytes)) throw new Exception("PNG was not completely saved");
        File.WriteAllText(output, "previous saved file");
        using (var locked = new FileStream(output, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            try { await Write(output); throw new Exception("Replacing a locked file must fail"); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
        if (File.ReadAllText(output) != "previous saved file") throw new Exception("Failed save damaged an existing file");
        if (Directory.EnumerateFiles(directory, "*.tmp").Any()) throw new Exception("Failed save left a temporary output");
        await Write(output);
        if (!File.ReadAllBytes(output).SequenceEqual(bytes)) throw new Exception("Retry after a failed save did not work");
        Console.WriteLine("PNG atomic save / locked output preservation / temporary cleanup / retry: pass");
    }
}
