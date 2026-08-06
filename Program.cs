using System.Security.Principal;

namespace LegionFanCurve;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        if (!IsAdmin())
        {
            MessageBox.Show(
                "Bu program yönetici olarak çalışmalı (EC + WMI fan kontrolü).\n\n" +
                "exe'ye sağ tık → Yönetici olarak çalıştır.",
                "Legion Fan Curve",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        Application.Run(new MainForm());
    }

    private static bool IsAdmin()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
