namespace M22;

public class ConnectionForm : BaseForm
{
    readonly TextBox txtServer = new();
    readonly CheckBox chkWin = new() { Text = "مصادقة Windows" };
    readonly TextBox txtUser = new();
    readonly TextBox txtPass = new() { UseSystemPasswordChar = true };
    readonly TextBox txtDb = new();
    readonly TextBox txtCompany = new();

    public ConnectionForm()
    {
        Text = "إعدادات الاتصال والشركة";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(480, 400);

        var c = Db.Cfg;
        txtServer.Text = c.Server;
        chkWin.Checked = c.WindowsAuth;
        txtUser.Text = c.User;
        txtPass.Text = c.Password;
        txtDb.Text = c.Database;
        txtCompany.Text = c.CompanyName;

        chkWin.CheckedChanged += (s, e) => { txtUser.Enabled = txtPass.Enabled = !chkWin.Checked; };
        txtUser.Enabled = txtPass.Enabled = !chkWin.Checked;

        var tl = Ui.Form2();
        Ui.AddRow(tl, "السيرفر", txtServer);
        Ui.AddRow(tl, "", chkWin);
        Ui.AddRow(tl, "مستخدم SQL", txtUser);
        Ui.AddRow(tl, "كلمة المرور", txtPass);
        Ui.AddRow(tl, "اسم قاعدة البيانات", txtDb);
        Ui.AddRow(tl, "اسم الشركة", txtCompany);

        var hint = new Label
        {
            Text = "مثال للسيرفر:  .\\SQLEXPRESS   أو   192.168.1.10   أو   localhost",
            Dock = DockStyle.Top,
            Height = 30,
            ForeColor = Color.Gray,
            Padding = new Padding(12, 0, 12, 0)
        };

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 56, Padding = new Padding(8), RightToLeft = RightToLeft.Yes };
        var ok = Ui.Btn("اختبار وحفظ", (s, e) => Save(), Color.FromArgb(39, 174, 96));
        var cancel = Ui.Btn("إلغاء", (s, e) => DialogResult = DialogResult.Cancel, Color.FromArgb(127, 140, 141));
        bottom.Controls.Add(ok);
        bottom.Controls.Add(cancel);

        Controls.Add(hint);
        Controls.Add(bottom);
        Controls.Add(tl);
    }

    void Save()
    {
        var old = Db.Cfg;
        var cfg = new AppConfig
        {
            Server = txtServer.Text.Trim(),
            WindowsAuth = chkWin.Checked,
            User = txtUser.Text.Trim(),
            Password = txtPass.Text,
            Database = txtDb.Text.Trim(),
            CompanyName = txtCompany.Text.Trim()
        };
        try
        {
            Db.Cfg = cfg;
            Db.EnsureDatabase();
            cfg.Save();
            Ui.Msg("تم الاتصال وحفظ الإعدادات. أعد تشغيل البرنامج إذا غيّرت قاعدة البيانات.");
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            Db.Cfg = old;
            Ui.Msg("فشل الاتصال:\n" + ex.Message, true);
        }
    }
}
