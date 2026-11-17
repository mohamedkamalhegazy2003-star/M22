using System.Globalization;

namespace M22;

static class Program
{
    [STAThread]
    static void Main()
    {
        var ci = new CultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentCulture = ci;
        CultureInfo.DefaultThreadCurrentUICulture = ci;
        CultureInfo.CurrentCulture = ci;
        CultureInfo.CurrentUICulture = ci;

        ApplicationConfiguration.Initialize();
        Application.SetDefaultFont(new Font("Tahoma", 10f));

        while (true)
        {
            try
            {
                Db.EnsureDatabase();
                break;
            }
            catch (Exception ex)
            {
                Ui.Msg("تعذر الاتصال بقاعدة البيانات:\n" + ex.Message + "\n\nراجع إعدادات الاتصال.", true);
                using var f = new ConnectionForm();
                if (f.ShowDialog() != DialogResult.OK) return;
            }
        }

        using (var login = new LoginForm())
        {
            if (login.ShowDialog() != DialogResult.OK) return;
        }

        Application.Run(new MainForm());
    }
}
