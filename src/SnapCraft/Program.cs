using System.Windows.Forms;

namespace SnapCraft;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try
        {
            WebAssets.Prepare();
            Application.Run(new MainForm());
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "SnapCraft", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
