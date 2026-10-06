using System.Reflection;
using System.IO;
using SnapCraft;

internal static class VideoClipTest
{
    public static async Task RunAsync(string root)
    {
        var type = typeof(VideoSession).Assembly.GetType("SnapCraft.VideoClipStore");
        if (type is null) throw new Exception("Completed recordings need a retained clip store before preview/copy");
        var directory = Path.Combine(root, "clip-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var original = Path.Combine(directory, "recording.mp4");
        byte[] bytes = [0, 0, 0, 20, 102, 116, 121, 112, 105, 115, 111, 109];
        File.WriteAllBytes(original, bytes);
        var retain = type.GetMethod("Retain", BindingFlags.Static | BindingFlags.NonPublic)!;
        var clip = (string)retain.Invoke(null, [original])!;
        if (!File.Exists(clip) || !File.ReadAllBytes(clip).SequenceEqual(bytes)) throw new Exception("Retaining damaged the completed clip");
        var save = type.GetMethod("SaveAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
        var destination = Path.Combine(directory, "saved.mp4");
        async Task Write() => await (Task)save.Invoke(null, [clip, destination])!;
        await Write();
        if (!File.ReadAllBytes(destination).SequenceEqual(bytes)) throw new Exception("MP4 save did not copy the full clip");
        File.WriteAllText(destination, "previous video");
        using (var locked = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            try { await Write(); throw new Exception("Locked MP4 output must reject replacement"); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
        if (File.ReadAllText(destination) != "previous video" || !File.Exists(clip)) throw new Exception("Failed save must keep both the previous output and source");
        if (Directory.EnumerateFiles(directory, "*.tmp").Any()) throw new Exception("Failed video save left a staging file");
        var cleanup = type.GetMethod("Cleanup", BindingFlags.Static | BindingFlags.NonPublic)!;
        File.SetLastWriteTimeUtc(clip, DateTime.UtcNow.AddDays(-8));
        cleanup.Invoke(null, [new[] { clip }]);
        if (!File.Exists(clip)) throw new Exception("Cleanup removed an open or clipboard-pinned clip");
        cleanup.Invoke(null, [Array.Empty<string>()]);
        if (File.Exists(clip)) throw new Exception("Expired unpinned clip was not cleaned up");
        if (!File.Exists(destination)) throw new Exception("Cleanup must never touch user-saved videos");
        Console.WriteLine("video retained source / atomic save / locked destination / pinned cleanup: pass");
    }
}
