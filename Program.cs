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
                "Bu program y\u00f6netici olarak \u00e7al\u0131\u015fmal\u0131 (EC + WMI fan kontrol\u00fc).\n\n" +
                "exe'ye sa\u011f t\u0131k \u2192 Y\u00f6netici olarak \u00e7al\u0131\u015ft\u0131r.",
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
