using System.Text.Json;

namespace SnapCraft;

internal sealed class AppSettings
{
    public int DelayMs { get; set; }
    public string OpenMode { get; set; } = "window";

    public static AppSettings Load()
    {
        try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(WebAssets.SettingsPath)) ?? new(); }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(WebAssets.DataRoot);
        File.WriteAllText(WebAssets.SettingsPath, JsonSerializer.Serialize(this));
    }
}
