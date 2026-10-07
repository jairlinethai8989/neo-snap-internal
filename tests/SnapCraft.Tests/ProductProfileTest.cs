using SnapCraft;
using System.IO;

internal static class ProductProfileTest
{
    public static void Run(bool verifyDefaultDataPath = false)
    {
        var neo = ProductProfile.For(ProductFlavor.NeoSnap);
        var snapzy = ProductProfile.For(ProductFlavor.Snapzy);
        if (neo.IconFile != "app.ico" || snapzy.IconFile != "snapzy.ico")
            throw new Exception("Snapzy must use its own approved icon without changing Neo Snap.");
        using (var icon = new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "icons", ProductProfile.Current.IconFile)))
            if (icon.Width < 16) throw new Exception("The product icon must be a valid Windows ICO.");

        if (neo.ProductName != "Neo Snap" || neo.ApplicationFolder != "SnapCraft" || neo.DataFolder != "SnapCraft" || neo.StartupRegistryValue != "NeoSnap" || neo.UninstallRegistryKey != "SnapCraft" || neo.DefaultLanguage != "th")
            throw new Exception("Neo Snap must preserve its existing Windows identity and Thai default.");
        if (snapzy.ProductName != "SnapZy" || snapzy.ApplicationFolder != "Snapzy" || snapzy.DataFolder != "Snapzy" || snapzy.StartupRegistryValue != "Snapzy" || snapzy.UninstallRegistryKey != "Snapzy" || snapzy.DefaultLanguage != "en")
            throw new Exception("Snapzy must use isolated Windows identity and English default.");
        if (neo.InstanceId == snapzy.InstanceId || !Guid.TryParse(neo.InstanceId, out _) || !Guid.TryParse(snapzy.InstanceId, out _) ||
            string.IsNullOrWhiteSpace(neo.AppUserModelId) || string.IsNullOrWhiteSpace(snapzy.AppUserModelId) || neo.AppUserModelId == snapzy.AppUserModelId)
            throw new Exception("Each product needs distinct valid application and taskbar identities.");
        if (AppInfo.ProductName != ProductProfile.Current.ProductName || AppInfo.InstanceId != ProductProfile.Current.InstanceId || AppInfo.AppUserModelId != ProductProfile.Current.AppUserModelId)
            throw new Exception("The executable identity must match the selected product profile.");
        if (ProductProfile.Current == snapzy && AppInfo.Version != "1.0.0")
            throw new Exception("The public product must start at version 1.0.0.");
        if (new AppSettings().Language != ProductProfile.Current.DefaultLanguage)
            throw new Exception("New settings must use the active product's default language.");
        if (System.Text.Json.JsonSerializer.Deserialize<AppSettings>("{\"Language\":\"fr\"}")!.Language != ProductProfile.Current.DefaultLanguage)
            throw new Exception("Unsupported saved languages must fall back to the product default.");
        foreach (var language in new[] { "en", "th" })
            if (System.Text.Json.JsonSerializer.Deserialize<AppSettings>($"{{\"Language\":\"{language}\"}}")!.Language != language)
                throw new Exception($"The {language} language preference must persist.");
        if (verifyDefaultDataPath)
        {
            var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductProfile.Current.DataFolder);
            if (WebAssets.DataRoot != expected) throw new Exception($"The default data directory must be isolated at {expected}.");
        }

        Console.WriteLine("product profiles: distinct install/data identities and preserved defaults: pass");
    }
}
