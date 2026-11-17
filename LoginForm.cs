namespace M22;

public class LoginForm : BaseForm
{
    readonly TextBox txtUser = new() { Text = "admin" };
    readonly TextBox txtPass = new() { UseSystemPasswordChar = true };

    public LoginForm()
    {
        Text = "تسجيل الدخول - M22";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(400, 250);

        var title = new Label
        {
            Text = "M22 - نظام إدارة الأعمال",
            Dock = DockStyle.Top,
            Height = 60,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Tahoma", 14f, FontStyle.Bold),
            BackColor = Color.FromArgb(52, 73, 94),
            ForeColor = Color.White
        };

        var tl = Ui.Form2();
        Ui.AddRow(tl, "اسم المستخدم", txtUser);
        Ui.AddRow(tl, "كلمة المرور", txtPass);

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 56, Padding = new Padding(8), RightToLeft = RightToLeft.Yes };
        var ok = Ui.Btn("دخول", (s, e) => DoLogin(), Color.FromArgb(39, 174, 96));
        var exit = Ui.Btn("خروج", (s, e) => DialogResult = DialogResult.Cancel, Color.FromArgb(127, 140, 141));
        var conn = Ui.Btn("إعدادات الاتصال", (s, e) => { using var f = new ConnectionForm(); f.ShowDialog(this); }, Color.FromArgb(41, 128, 185));
        bottom.Controls.Add(ok);
        bottom.Controls.Add(exit);
        bottom.Controls.Add(conn);

        Controls.Add(tl);
        Controls.Add(bottom);
        Controls.Add(title);
        AcceptButton = ok;
        ActiveControl = txtPass;
    }

    void DoLogin()
    {
        try
        {
            var o = Db.Scalar("SELECT PasswordHash FROM Users WHERE Username=@u", ("@u", txtUser.Text.Trim()));
            if (o == null || Ui.S(o) != Ui.Sha(txtPass.Text))
            {
                Ui.Msg("اسم المستخدم أو كلمة المرور غير صحيحة.", true);
                return;
            }
            Session.User = txtUser.Text.Trim();
            Session.FullName = Ui.S(Db.Scalar("SELECT FullName FROM Users WHERE Username=@u", ("@u", Session.User)));
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex) { Ui.Msg(ex.Message, true); }
    }
}
