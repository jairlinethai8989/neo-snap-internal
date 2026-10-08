namespace SnapCraft;

internal static class AppInfo
{
    public static string Version => typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "0.1.21";
    public const string Developer = "jairlinethai";
    public static string ProductName => ProductProfile.Current.ProductName;
    public static string AppUserModelId => ProductProfile.Current.AppUserModelId;
#if SNAPZY_BUILD
    public const string InstanceId = "6A2D75A0-73EE-4B83-9D4F-C3E14C7D5C10";
#else
    public const string InstanceId = "37B63537-46B0-4D51-A9C0-619BE83FE608";
#endif
}
