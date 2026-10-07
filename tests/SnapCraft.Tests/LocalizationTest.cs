using SnapCraft;

internal static class LocalizationTest
{
    public static void Run()
    {
        Localization.SetLanguage("en");
        if (Localization.Translate("เลือกภาพตามลำดับแท็บ") != "Select images in tab order")
            throw new Exception("Native dialogs must use the selected English language.");
        if (Localization.Translate("ภาพใหญ่เกินไป กรุณาแบ่งเป็นหลายภาพ") != "Image is too large. Please split it into multiple images.")
            throw new Exception("Native error text must remain readable when dynamic content is added.");
        if (Localization.Translate("เกี่ยวกับ Neo Snap") != "About SnapZy" && ProductProfile.Current == ProductProfile.For(ProductFlavor.Snapzy))
            throw new Exception("Native UI must use the selected product name in English.");
        if (ProductProfile.Current == ProductProfile.For(ProductFlavor.Snapzy) && Localization.Translate("ออกจาก Neo Snap") != "Exit SnapZy")
            throw new Exception("Native tray labels must use the selected language and product identity.");
        Localization.SetLanguage("th");
        if (Localization.Translate("เลือกภาพตามลำดับแท็บ") != "เลือกภาพตามลำดับแท็บ")
            throw new Exception("Thai selection must preserve Thai text.");
        Console.WriteLine("native localization: selected language and product name: pass");
    }
}
