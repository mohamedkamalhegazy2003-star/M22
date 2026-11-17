using System.Data;

namespace M22;

/// <summary>Invoice screen. kind: 1 sale, 2 purchase, 3 sale return, 4 purchase return.</summary>
public class InvoiceForm : BaseForm
{
    static readonly string[] Titles = { "", "فاتورة مبيعات", "فاتورة مشتريات", "مرتجع مبيعات", "مرتجع مشتريات" };

    readonly int kind;
    readonly DataTable items;
    readonly DateTimePicker dtp = new() { Format = DateTimePickerFormat.Short };
    readonly ComboBox cmbParty = new(), cmbStore = new(), cmbTre = new();
    readonly TextBox txtCode = new(), txtNotes = new();
    readonly TextBox txtDisc = new() { Text = "0" };
    readonly TextBox txtPaid = new() { Text = "0", ReadOnly = true };
    readonly CheckBox chkFull = new() { Text = "مدفوع بالكامل", Checked = true };
    readonly Label lblTotal = Val(), lblNet = Val(), lblRem = Val();
    readonly DataGridView grid = new();
    bool busy;
    decimal curTotal;

    static Label Val() => new()
    {
        AutoSize = false,
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.FixedSingle,
        TextAlign = ContentAlignment.MiddleCenter,
        Font = new Font("Tahoma", 11f, FontStyle.Bold),
        BackColor = Color.White,
        Text = "0.00"
    };

    public InvoiceForm(int kind)
    {
        this.kind = kind;
        Text = Titles[kind];
        Size = new Size(1000, 720);

        bool sales = kind == 1 || kind == 3;
        items = Db.Query("SELECT Id, Code, Name, CostPrice, SalePrice FROM Items ORDER BY Name");
        Ui.FillCombo(cmbParty, Db.Query("SELECT Id, Name FROM Parties WHERE Kind=@k ORDER BY Name", ("@k", sales ? 1 : 2)), blank: "نقدي / بدون طرف");
        Ui.FillCombo(cmbStore, Db.Query("SELECT Id, Name FROM Stores ORDER BY Id"));
        Ui.FillCombo(cmbTre, Db.Query("SELECT Id, Name FROM Treasuries ORDER BY Id"));

        // ----- header -----
        var hdr = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 3 * 38 + 16,
            ColumnCount = 4,
            RowCount = 3,
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(8)
        };
        hdr.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        hdr.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        hdr.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        hdr.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int r = 0; r < 3; r++) hdr.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        void P(int row, int col, string text, Control c)
        {
            hdr.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, col * 2, row);
            c.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            hdr.Controls.Add(c, col * 2 + 1, row);
        }
        P(0, 0, "التاريخ", dtp);
        P(0, 1, sales ? "العميل" : "المورد", cmbParty);
        P(1, 0, "المخزن", cmbStore);
        P(1, 1, "الخزينة", cmbTre);
        P(2, 0, "كود الصنف", txtCode);
        P(2, 1, "ملاحظات", txtNotes);
        txtCode.PlaceholderText = "اكتب الكود واضغط Enter";

        // ----- lines grid -----
        Ui.StyleGrid(grid);
        grid.ReadOnly = false;
        grid.AllowUserToAddRows = true;
        grid.AllowUserToDeleteRows = true;
        grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        grid.Dock = DockStyle.Fill;
        grid.Columns.Add(new DataGridViewComboBoxColumn
        {
            Name = "item", HeaderText = "الصنف", DataSource = items, DisplayMember = "Name", ValueMember = "Id",
            FlatStyle = FlatStyle.Flat, FillWeight = 50, DisplayStyleForCurrentCellOnly = true
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "qty", HeaderText = "الكمية", FillWeight = 15 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "price", HeaderText = "السعر", FillWeight = 17 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "total", HeaderText = "الإجمالي", FillWeight = 18, ReadOnly = true });
        grid.DataError += (s, e) => e.ThrowException = false;
        grid.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (grid.IsCurrentCellDirty && grid.CurrentCell is DataGridViewComboBoxCell)
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        grid.CellValueChanged += OnCellChanged;
        grid.UserDeletedRow += (s, e) => RecalcLines();

        // ----- totals -----
        var tot = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 3 * 38 + 16,
            ColumnCount = 4,
            RowCount = 3,
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(8)
        };
        tot.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        tot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        tot.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        tot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int r = 0; r < 3; r++) tot.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        void T(int row, int col, string text, Control c)
        {
            tot.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, col * 2, row);
            c.Anchor = c is Label ? AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom : AnchorStyles.Left | AnchorStyles.Right;
            tot.Controls.Add(c, col * 2 + 1, row);
        }
        T(0, 0, "الإجمالي", lblTotal);
        T(0, 1, "الخصم", txtDisc);
        T(1, 0, "الصافي", lblNet);
        T(1, 1, kind <= 2 ? "المدفوع" : "المسترد نقداً", txtPaid);
        T(2, 0, "", chkFull);
        T(2, 1, "المتبقي (آجل)", lblRem);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 56, Padding = new Padding(8), RightToLeft = RightToLeft.Yes };
        buttons.Controls.Add(Ui.Btn("حفظ الفاتورة", (s, e) => Save(), Color.FromArgb(39, 174, 96)));
        buttons.Controls.Add(Ui.Btn("جديد", (s, e) => Reset(), Color.FromArgb(41, 128, 185)));
        buttons.Controls.Add(Ui.Btn("إغلاق", (s, e) => Close(), Color.FromArgb(127, 140, 141)));

        Controls.Add(grid);
        Controls.Add(tot);
        Controls.Add(buttons);
        Controls.Add(hdr);

        txtDisc.TextChanged += (s, e) => RecalcTotals();
        txtPaid.TextChanged += (s, e) => RecalcTotals();
        chkFull.CheckedChanged += (s, e) => { txtPaid.ReadOnly = chkFull.Checked; RecalcTotals(); };
        txtCode.KeyDown += OnCodeKey;
        RecalcTotals();
    }

    void OnCodeKey(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter) return;
        e.SuppressKeyPress = true;
        var code = txtCode.Text.Trim();
        if (code == "") return;
        var m = items.Select("Code='" + code.Replace("'", "''") + "'");
        if (m.Length == 0) { Ui.Msg("الصنف غير موجود."); return; }
        AddItem(Convert.ToInt32(m[0]["Id"]));
        txtCode.Clear();
    }

    object PriceFor(DataRow item) => (kind == 1 || kind == 3) ? item["SalePrice"] : item["CostPrice"];

    void AddItem(int id)
    {
        var m = items.Select("Id=" + id);
        if (m.Length == 0) return;
        foreach (DataGridViewRow r in grid.Rows)
        {
            if (!r.IsNewRow && r.Cells[0].Value is int x && x == id)
            {
                r.Cells[1].Value = Ui.Dec(Ui.S(r.Cells[1].Value)) + 1;
                return;
            }
        }
        busy = true;
        grid.Rows.Add(id, 1m, PriceFor(m[0]), 0m);
        busy = false;
        RecalcLines();
    }

    void OnCellChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (busy || e.RowIndex < 0 || e.ColumnIndex > 2) return;
        if (e.ColumnIndex == 0)
        {
            var row = grid.Rows[e.RowIndex];
            if (row.Cells[0].Value is int id)
            {
                var m = items.Select("Id=" + id);
                if (m.Length > 0)
                {
                    if (string.IsNullOrWhiteSpace(Ui.S(row.Cells[1].Value))) row.Cells[1].Value = 1m;
                    if (string.IsNullOrWhiteSpace(Ui.S(row.Cells[2].Value))) row.Cells[2].Value = PriceFor(m[0]);
                }
            }
        }
        RecalcLines();
    }

    void RecalcLines()
    {
        busy = true;
        curTotal = 0;
        foreach (DataGridViewRow r in grid.Rows)
        {
            if (r.IsNewRow) continue;
            var line = Ui.Dec(Ui.S(r.Cells[1].Value)) * Ui.Dec(Ui.S(r.Cells[2].Value));
            r.Cells[3].Value = line;
            curTotal += line;
        }
        busy = false;
        RecalcTotals();
    }

    void RecalcTotals()
    {
        if (busy) return;
        var net = curTotal - Ui.Dec(txtDisc.Text);
        lblTotal.Text = curTotal.ToString("N2");
        lblNet.Text = net.ToString("N2");
        if (chkFull.Checked)
        {
            busy = true;
            txtPaid.Text = net.ToString("0.##");
            busy = false;
        }
        var paid = chkFull.Checked ? net : Ui.Dec(txtPaid.Text);
        lblRem.Text = (net - paid).ToString("N2");
    }

    void Reset()
    {
        grid.Rows.Clear();
        txtDisc.Text = "0";
        txtPaid.Text = "0";
        txtNotes.Clear();
        txtCode.Clear();
        chkFull.Checked = true;
        curTotal = 0;
        RecalcTotals();
    }

    void Save()
    {
        grid.EndEdit();
        var lines = new List<Svc.Line>();
        foreach (DataGridViewRow r in grid.Rows)
        {
            if (r.IsNewRow) continue;
            if (r.Cells[0].Value is not int itemId) continue;
            var qty = Ui.Dec(Ui.S(r.Cells[1].Value));
            var price = Ui.Dec(Ui.S(r.Cells[2].Value));
            if (qty <= 0) { Ui.Msg("الكمية يجب أن تكون أكبر من صفر.", true); return; }
            if (price < 0) { Ui.Msg("السعر غير صحيح.", true); return; }
            lines.Add(new Svc.Line(itemId, qty, price));
        }
        if (lines.Count == 0) { Ui.Msg("أضف صنفاً واحداً على الأقل.", true); return; }

        var store = Ui.Sel(cmbStore);
        if (store == null) { Ui.Msg("اختر المخزن.", true); return; }
        var party = Ui.Sel(cmbParty);
        var tre = Ui.Sel(cmbTre);

        decimal total = lines.Sum(l => l.Qty * l.Price);
        decimal disc = Ui.Dec(txtDisc.Text);
        if (disc < 0 || disc > total) { Ui.Msg("قيمة الخصم غير صحيحة.", true); return; }
        decimal net = total - disc;
        decimal paid = chkFull.Checked ? net : Ui.Dec(txtPaid.Text);
        if (paid < 0 || paid > net) { Ui.Msg("المبلغ المدفوع غير صحيح.", true); return; }
        if (net - paid > 0 && party == null) { Ui.Msg("حدد العميل/المورد لأن هناك مبلغاً آجلاً.", true); return; }
        if (paid > 0 && tre == null) { Ui.Msg("اختر الخزينة.", true); return; }

        if (kind == 1 || kind == 4)
        {
            var low = new List<string>();
            foreach (var g in lines.GroupBy(l => l.ItemId))
            {
                var need = g.Sum(l => l.Qty);
                var have = Svc.Stock(g.Key, store.Value);
                if (have < need)
                    low.Add(Ui.S(items.Select("Id=" + g.Key)[0]["Name"]) + " (المتاح " + have.ToString("0.###") + ")");
            }
            if (low.Count > 0 && !Ui.Ask("الكمية أكبر من رصيد المخزن للأصناف:\n" + string.Join("\n", low) + "\n\nهل تريد المتابعة؟"))
                return;
        }

        try
        {
            int num = Svc.SaveInvoice(kind, dtp.Value.Date, party, store.Value, tre, disc, paid, txtNotes.Text.Trim(), lines);
            Ui.Msg("تم حفظ " + Titles[kind] + " رقم " + num);
            Reset();
        }
        catch (Exception ex) { Ui.Msg("تعذر الحفظ: " + ex.Message, true); }
    }
}
