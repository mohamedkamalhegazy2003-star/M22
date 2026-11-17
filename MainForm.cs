namespace M22;

public class MainForm : BaseForm
{
    public MainForm()
    {
        Text = "M22 - " + Db.Cfg.CompanyName;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(900, 600);

        // ---------- tiles ----------
        var tiles = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            RightToLeft = RightToLeft.Yes,
            WrapContents = true,
            AutoScroll = true,
            Padding = new Padding(60, 40, 60, 20),
            BackColor = Color.FromArgb(38, 50, 56)
        };

        tiles.Controls.Add(Tile("التقارير", Color.FromArgb(69, 76, 94), () => Open(new ReportsForm())));
        tiles.Controls.Add(Tile("فاتورة مشتريات", Color.FromArgb(30, 136, 229), () => Open(new InvoiceForm(2))));
        tiles.Controls.Add(Tile("فاتورة مبيعات", Color.FromArgb(230, 85, 50), () => Open(new InvoiceForm(1))));
        tiles.Controls.Add(Tile("إضافة صنف", Color.FromArgb(67, 160, 40), () => Open(new ListEditForm(Specs.Items()))));
        tiles.Controls.Add(Tile("مرتجع مشتريات", Color.FromArgb(110, 110, 110), () => Open(new InvoiceForm(4))));
        tiles.Controls.Add(Tile("دفع نقدي", Color.FromArgb(103, 58, 183), () => Open(new VoucherForm(2))));
        tiles.Controls.Add(Tile("مرتجع مبيعات", Color.FromArgb(96, 163, 164), () => Open(new InvoiceForm(3))));
        tiles.Controls.Add(Tile("استلام نقدي", Color.FromArgb(63, 81, 181), () => Open(new VoucherForm(1))));
        tiles.Controls.Add(Tile("المصروفات", Color.FromArgb(75, 75, 75), () => Open(new VoucherForm(3))));
        tiles.Controls.Add(Tile("الإيرادات", Color.FromArgb(33, 150, 243), () => Open(new VoucherForm(4))));
        tiles.Controls.Add(Tile("العملاء", Color.FromArgb(0, 150, 136), () => Open(new ListEditForm(Specs.Customers()))));
        tiles.Controls.Add(Tile("الموردين", Color.FromArgb(121, 85, 72), () => Open(new ListEditForm(Specs.Suppliers()))));
        tiles.Controls.Add(Tile("أرصدة المخزون", Color.FromArgb(255, 152, 0), () => Open(new ReportsForm(4))));
        tiles.Controls.Add(Tile("سجل المستندات", Color.FromArgb(96, 125, 139), () => Open(new DocumentsForm())));

        var company = new Label
        {
            Text = Db.Cfg.CompanyName,
            Dock = DockStyle.Bottom,
            Height = 50,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Tahoma", 16f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(38, 50, 56)
        };

        // ---------- status bar ----------
        var status = new StatusStrip { RightToLeft = RightToLeft.Yes };
        status.Items.Add(new ToolStripStatusLabel("المستخدم: " + (Session.FullName != "" ? Session.FullName : Session.User)));
        status.Items.Add(new ToolStripStatusLabel("|"));
        status.Items.Add(new ToolStripStatusLabel("السيرفر: " + Db.Cfg.Server));
        status.Items.Add(new ToolStripStatusLabel("|"));
        status.Items.Add(new ToolStripStatusLabel("قاعدة البيانات: " + Db.Cfg.Database));
        status.Items.Add(new ToolStripStatusLabel("|"));
        status.Items.Add(new ToolStripStatusLabel(DateTime.Today.ToString("yyyy/MM/dd")));

        // ---------- menu ----------
        var menu = new MenuStrip
        {
            RightToLeft = RightToLeft.Yes,
            Dock = DockStyle.Top,
            Font = new Font("Tahoma", 11f),
            BackColor = Color.FromArgb(200, 70, 20),
            ForeColor = Color.White
        };

        var index = Mn("الفهرس",
            M("الأصناف", () => Open(new ListEditForm(Specs.Items())), Keys.Control | Keys.I),
            M("العملاء", () => Open(new ListEditForm(Specs.Customers())), Keys.Control | Keys.H),
            M("الموردين", () => Open(new ListEditForm(Specs.Suppliers())), Keys.Control | Keys.J),
            M("شجرة الحسابات", () => Open(new AccountsForm()), Keys.Control | Keys.T),
            M("بنود المصروفات", () => Open(new ListEditForm(Specs.ExpenseCategories()))),
            M("بنود الإيرادات", () => Open(new ListEditForm(Specs.RevenueCategories()))),
            M("الخزائن والبنوك", () => Open(new ListEditForm(Specs.Treasuries()))),
            M("المستخدمين", () => Open(new ListEditForm(Specs.Users()))),
            new ToolStripSeparator(),
            M("إعدادات الاتصال والشركة", () => { using var f = new ConnectionForm(); f.ShowDialog(this); }),
            M("خروج", Close));

        var stores = Mn("المخازن",
            M("المخازن", () => Open(new ListEditForm(Specs.Stores()))),
            M("الأصناف", () => Open(new ListEditForm(Specs.Items()))),
            M("أرصدة المخزون", () => Open(new ReportsForm(4))));

        var sales = Mn("المبيعات",
            M("فاتورة مبيعات", () => Open(new InvoiceForm(1))),
            M("مرتجع مبيعات", () => Open(new InvoiceForm(3))),
            M("استلام نقدي من عميل", () => Open(new VoucherForm(1))),
            M("سجل المستندات", () => Open(new DocumentsForm())));

        var purchases = Mn("المشتريات",
            M("فاتورة مشتريات", () => Open(new InvoiceForm(2))),
            M("مرتجع مشتريات", () => Open(new InvoiceForm(4))),
            M("دفع نقدي لمورد", () => Open(new VoucherForm(2))));

        var accounts = Mn("الحسابات",
            M("شجرة الحسابات", () => Open(new AccountsForm())),
            M("المصروفات", () => Open(new VoucherForm(3))),
            M("الإيرادات", () => Open(new VoucherForm(4))),
            M("دفتر أستاذ حساب", () => Open(new ReportsForm(5))),
            M("ميزان المراجعة", () => Open(new ReportsForm(6))),
            M("قائمة الدخل", () => Open(new ReportsForm(7))));

        var parties = Mn("العملاء والموردين",
            M("كشف حساب عميل", () => Open(new ReportsForm(0))),
            M("كشف حساب مورد", () => Open(new ReportsForm(1))),
            M("أرصدة العملاء", () => Open(new ReportsForm(2))),
            M("أرصدة الموردين", () => Open(new ReportsForm(3))));

        var reports = Mn("التقارير", M("كل التقارير", () => Open(new ReportsForm())));

        menu.Items.AddRange(new ToolStripItem[] { index, reports, parties, accounts, stores, purchases, sales });
        foreach (ToolStripItem it in menu.Items) it.ForeColor = Color.White;

        Controls.Add(tiles);
        Controls.Add(company);
        Controls.Add(status);
        Controls.Add(menu);
        MainMenuStrip = menu;
    }

    static void Open(Form f) => f.Show();

    static ToolStripMenuItem M(string text, Action? act = null, Keys keys = Keys.None)
    {
        var m = new ToolStripMenuItem(text);
        if (act != null) m.Click += (s, e) => act();
        if (keys != Keys.None) m.ShortcutKeys = keys;
        m.ForeColor = Color.Black;
        return m;
    }

    static ToolStripMenuItem Mn(string text, params ToolStripItem[] children)
    {
        var m = new ToolStripMenuItem(text);
        m.DropDownItems.AddRange(children);
        return m;
    }

    static Button Tile(string text, Color color, Action act)
    {
        var b = new Button
        {
            Text = text,
            Size = new Size(190, 130),
            Margin = new Padding(8),
            FlatStyle = FlatStyle.Flat,
            BackColor = color,
            ForeColor = Color.White,
            Font = new Font("Tahoma", 13f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(color, 0.2f);
        b.Click += (s, e) => act();
        return b;
    }
}
