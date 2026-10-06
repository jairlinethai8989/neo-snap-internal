namespace SnapCraft;

internal enum ProductFlavor
{
    NeoSnap,
    Snapzy
}

internal sealed record ProductProfile(
    string ProductName,
    string ApplicationFolder,
    string DataFolder,
    string StartupRegistryValue,
    string UninstallRegistryKey,
    string InstanceId,
    string AppUserModelId,
    string DefaultLanguage)
{
    public static ProductProfile Current { get; } = For(
#if SNAPZY_BUILD
        ProductFlavor.Snapzy
#else
        ProductFlavor.NeoSnap
#endif
    );

    public static ProductProfile For(ProductFlavor flavor) => flavor switch
    {
        ProductFlavor.NeoSnap => new("Neo Snap", "SnapCraft", "SnapCraft", "NeoSnap", "SnapCraft", "37B63537-46B0-4D51-A9C0-619BE83FE608", "jairlinethai.NeoSnap", "th"),
        ProductFlavor.Snapzy => new("Snapzy", "Snapzy", "Snapzy", "Snapzy", "Snapzy", "6A2D75A0-73EE-4B83-9D4F-C3E14C7D5C10", "jairlinethai.Snapzy", "en"),
        _ => throw new ArgumentOutOfRangeException(nameof(flavor))
    };
}
