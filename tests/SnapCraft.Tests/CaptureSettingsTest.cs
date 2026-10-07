using SnapCraft;
using System.Text.Json;

internal static class CaptureSettingsTest
{
    public static void Run()
    {
        if (new AppSettings().DelayMs != 0) throw new Exception("Default capture delay must be immediate.");
        foreach (var delay in new[] { -1, 1, 999, 60000, int.MaxValue })
        {
            var settings = JsonSerializer.Deserialize<AppSettings>($"{{\"DelayMs\":{delay}}}")!;
            if (settings.DelayMs != 0) throw new Exception($"Unsupported capture delay {delay} must reset to immediate.");
        }
        foreach (var delay in new[] { 0, 3000, 5000, 10000 })
            if (new AppSettings { DelayMs = delay }.DelayMs != delay) throw new Exception("Supported capture delay changed.");
        Console.WriteLine("capture delay: supported choices retained / invalid persisted delays reset: pass");
    }
}
