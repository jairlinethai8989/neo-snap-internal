using System.IO;
using SnapCraft;

internal static class PerformanceTraceTest
{
    public static async Task RunAsync()
    {
        var stage = "test.timing-" + Guid.NewGuid().ToString("N");
        PerformanceTrace.Record(stage, 12.5);
        var path = Path.Combine(WebAssets.DataRoot, "Logs", "performance.log");
        for (var attempt = 0; attempt < 250; attempt++)
        {
            try
            {
                if (!File.Exists(path)) { await Task.Delay(20); continue; }
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                if ((await reader.ReadToEndAsync()).Contains(stage + " 12.5ms"))
                {
                    Console.WriteLine("performance trace: asynchronous timing log: pass");
                    return;
                }
            }
            catch (IOException) { }
            await Task.Delay(20);
        }
        throw new Exception("Performance timing was not written.");
    }
}
