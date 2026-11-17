using System.Data;
using System.Globalization;
using System.Text;

namespace M22;

public class ReportsForm : BaseForm
{
    // param: 0 none, 1 customer, 2 supplier, 3 account
    static readonly (string name, int param)[] Reps =
    {
        ("كشف حساب عميل", 1),
        ("كشف حساب مورد", 2),
        ("أرصدة العملاء", 0),
        ("أرصدة الموردين", 0),
        ("أرصدة المخزون", 0),
        ("دفتر أستاذ حساب", 3),
        ("ميزان المراجعة", 0),
        ("قائمة الدخل", 0),
        ("تقرير المبيعات", 0),
        ("تقرير المشتريات", 0),
        ("المصروفات والإيرادات حسب البند", 0)
    };

    readonly ComboBox cmbRep = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
    readonly ComboBox cmbParam = new() { Width = 230 };
    readonly DateTimePicker dtpFrom = new() { Format = DateTimePickerFormat.Short, Width = 120 };
    readonly DateTimePicker dtpTo = new() { Format = DateTimePickerFormat.Short, Width = 120 };
    readonly DataGridView grid = new();

    public ReportsForm(int index = 0)
    {
        Text = "التقارير";
        Size = new Size(1050, 680);

        foreach (var r in Reps) cmbRep.Items.Add(r.name);

        var today = DateTime.Today;
        dtpFrom.Value = new DateTime(today.Year, 1, 1);
        dtpTo.Value = today;

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 56, Padding = new Padding(8), RightToLeft = RightToLeft.Yes };
        top.Controls.Add(cmbRep);
        top.Controls.Add(cmbParam);
        top.Controls.Add(new Label { Text = "من", AutoSize = true, Margin = new Padding(8, 8, 2, 0) });
        top.Controls.Add(dtpFrom);
        top.Controls.Add(new Label { Text = "إلى", AutoSize = true, Margin = new Padding(8, 8, 2, 0) });
        top.Controls.Add(dtpTo);
        top.Controls.Add(Ui.Btn("عرض", (s, e) => ShowReport(), Color.FromArgb(39, 174, 96)));
        top.Controls.Add(Ui.Btn("تصدير CSV", (s, e) => Export()));

        Ui.StyleGrid(grid);
        grid.Dock = DockStyle.Fill;

        Controls.Add(grid);
        Controls.Add(top);

        cmbRep.SelectedIndexChanged += (s, e) => OnReportChanged();
        cmbRep.SelectedIndex = Math.Clamp(index, 0, Reps.Length - 1);
    }

    void OnReportChanged()
    {
        int p = Reps[cmbRep.SelectedIndex].param;
        cmbParam.Visible = p > 0;
        grid.DataSource = null;
        switch (p)
        {
            case 1: Ui.FillCombo(cmbParam, Db.Query("SELECT Id, Name FROM Parties WHERE Kind=1 ORDER BY Name")); break;
            case 2: Ui.FillCombo(cmbParam, Db.Query("SELECT Id, Name FROM Parties WHERE Kind=2 ORDER BY Name")); break;
            case 3: Ui.FillCombo(cmbParam, Db.Query("SELECT Id, Code + N' - ' + Name AS Name FROM Accounts ORDER BY Code")); break;
        }
    }

    void ShowReport()
    {
        try
        {
            DataTable? dt = cmbRep.SelectedIndex switch
            {
                0 => PartyStatement(1),
                1 => PartyStatement(2),
                2 => Balances(1),
                3 => Balances(2),
                4 => StockReport(),
                5 => Ledger(),
                6 => TrialBalance(),
                7 => IncomeStatement(),
                8 => InvoicesReport(1),
                9 => InvoicesReport(2),
                _ => CategoriesReport()
            };
            if (dt == null) return;
            grid.DataSource = dt;
            Ui.FormatGrid(grid);
        }
        catch (Exception ex) { Ui.Msg("تعذر عرض التقرير: " + ex.Message, true); }
    }

    // ---------- helpers ----------

    static void AddTotals(DataTable dt, params string[] cols)
    {
        if (dt.Rows.Count == 0) return;
        var row = dt.NewRow();
        var first = dt.Columns.Cast<DataColumn>().FirstOrDefault(c => c.DataType == typeof(string));
        if (first != null) row[first] = "الإجمالي";
        foreach (var c in cols)
        {
            var v = dt.Compute($"SUM([{c}])", "");
            if (v != DBNull.Value) row[c] = v;
        }
        dt.Rows.Add(row);
    }

    // ---------- reports ----------

    DataTable? PartyStatement(int kind)
    {
        int? pid = Ui.Sel(cmbParam);
        if (pid == null) { Ui.Msg("اختر الطرف أولاً."); return null; }

        decimal opening = Convert.ToDecimal(Db.Scalar("SELECT OpeningBalance FROM Parties WHERE Id=@p", ("@p", pid)) ?? 0m);

        string sql = kind == 1
            ? @"SELECT InvDate AS D, N'فاتورة مبيعات' AS Doc, Num, Net AS Dr, Paid AS Cr FROM Invoices WHERE Kind=1 AND PartyId=@p
                UNION ALL SELECT InvDate, N'مرتجع مبيعات', Num, Paid, Net FROM Invoices WHERE Kind=3 AND PartyId=@p
                UNION ALL SELECT VDate, N'استلام نقدي', Num, 0, Amount FROM Vouchers WHERE Kind=1 AND PartyId=@p
                ORDER BY D, Num"
            : @"SELECT InvDate AS D, N'فاتورة مشتريات' AS Doc, Num, Paid AS Dr, Net AS Cr FROM Invoices WHERE Kind=2 AND PartyId=@p
                UNION ALL SELECT InvDate, N'مرتجع مشتريات', Num, Net, Paid FROM Invoices WHERE Kind=4 AND PartyId=@p
                UNION ALL SELECT VDate, N'دفع نقدي', Num, Amount, 0 FROM Vouchers WHERE Kind=2 AND PartyId=@p
                ORDER BY D, Num";

        var src = Db.Query(sql, ("@p", pid));
        var res = new DataTable();
        res.Columns.Add("التاريخ", typeof(DateTime));
        res.Columns.Add("المستند", typeof(string));
        res.Columns.Add("الرقم", typeof(int));
        res.Columns.Add("مدين", typeof(decimal));
        res.Columns.Add("دائن", typeof(decimal));
        res.Columns.Add("الرصيد", typeof(decimal));

        decimal bal = opening;
        res.Rows.Add(DBNull.Value, "رصيد أول المدة", DBNull.Value, 0m, 0m, bal);
        foreach (DataRow r in src.Rows)
        {
            decimal dr = Convert.ToDecimal(r["Dr"]);
            decimal cr = Convert.ToDecimal(r["Cr"]);
            bal += kind == 1 ? dr - cr : cr - dr;
            res.Rows.Add(r["D"], r["Doc"], r["Num"], dr, cr, bal);
        }
        return res;
    }

    DataTable Balances(int kind)
    {
        string sql = kind == 1
            ? @"SELECT p.Name AS [العميل], p.Phone AS [الهاتف],
                  p.OpeningBalance
                  + ISNULL((SELECT SUM(CASE Kind WHEN 1 THEN Net-Paid ELSE -(Net-Paid) END) FROM Invoices WHERE PartyId=p.Id AND Kind IN (1,3)),0)
                  - ISNULL((SELECT SUM(Amount) FROM Vouchers WHERE PartyId=p.Id AND Kind=1),0) AS [الرصيد]
                FROM Parties p WHERE p.Kind=1 ORDER BY p.Name"
            : @"SELECT p.Name AS [المورد], p.Phone AS [الهاتف],
                  p.OpeningBalance
                  + ISNULL((SELECT SUM(CASE Kind WHEN 2 THEN Net-Paid ELSE -(Net-Paid) END) FROM Invoices WHERE PartyId=p.Id AND Kind IN (2,4)),0)
                  - ISNULL((SELECT SUM(Amount) FROM Vouchers WHERE PartyId=p.Id AND Kind=2),0) AS [الرصيد]
                FROM Parties p WHERE p.Kind=2 ORDER BY p.Name";
        var dt = Db.Query(sql);
        AddTotals(dt, "الرصيد");
        return dt;
    }

    DataTable StockReport()
    {
        var dt = Db.Query(
            @"SELECT i.Name AS [الصنف], i.Code AS [الكود], s.Name AS [المخزن], SUM(m.Qty) AS [الكمية],
                     i.ReorderLevel AS [حد الطلب], i.CostPrice AS [التكلفة], SUM(m.Qty) * i.CostPrice AS [القيمة]
              FROM StockMoves m
              JOIN Items i ON i.Id = m.ItemId
              JOIN Stores s ON s.Id = m.StoreId
              GROUP BY i.Name, i.Code, s.Name, i.ReorderLevel, i.CostPrice
              ORDER BY i.Name");
        AddTotals(dt, "القيمة");
        return dt;
    }

    DataTable? Ledger()
    {
        int? aid = Ui.Sel(cmbParam);
        if (aid == null) { Ui.Msg("اختر الحساب أولاً."); return null; }

        int type = Convert.ToInt32(Db.Scalar("SELECT Type FROM Accounts WHERE Id=@a", ("@a", aid)));
        bool debitNatural = type == 1 || type == 5;
        var f = dtpFrom.Value.Date;
        var t = dtpTo.Value.Date;

        const string cte = "WITH t AS (SELECT Id FROM Accounts WHERE Id=@a UNION ALL SELECT a.Id FROM Accounts a JOIN t ON a.ParentId=t.Id) ";
        decimal opening = Convert.ToDecimal(Db.Scalar(cte +
            @"SELECT ISNULL(SUM(l.Debit-l.Credit),0) FROM JournalLines l JOIN JournalEntries e ON e.Id=l.EntryId
              WHERE l.AccountId IN (SELECT Id FROM t) AND e.EDate < @f", ("@a", aid), ("@f", f)));

        var src = Db.Query(cte +
            @"SELECT e.EDate AS D, e.Descr AS Descr, l.Debit AS Dr, l.Credit AS Cr
              FROM JournalLines l JOIN JournalEntries e ON e.Id=l.EntryId
              WHERE l.AccountId IN (SELECT Id FROM t) AND e.EDate BETWEEN @f AND @t
              ORDER BY e.EDate, e.Id, l.Id", ("@a", aid), ("@f", f), ("@t", t));

        var res = new DataTable();
        res.Columns.Add("التاريخ", typeof(DateTime));
        res.Columns.Add("البيان", typeof(string));
        res.Columns.Add("مدين", typeof(decimal));
        res.Columns.Add("دائن", typeof(decimal));
        res.Columns.Add("الرصيد", typeof(decimal));

        decimal signed = opening;
        res.Rows.Add(DBNull.Value, "رصيد ما قبل الفترة", 0m, 0m, debitNatural ? signed : -signed);
        foreach (DataRow r in src.Rows)
        {
            decimal dr = Convert.ToDecimal(r["Dr"]);
            decimal cr = Convert.ToDecimal(r["Cr"]);
            signed += dr - cr;
            res.Rows.Add(r["D"], r["Descr"], dr, cr, debitNatural ? signed : -signed);
        }
        return res;
    }

    DataTable TrialBalance()
    {
        var dt = Db.Query(
            @"SELECT a.Code AS [الكود], a.Name AS [الحساب],
                     SUM(l.Debit) AS [مدين], SUM(l.Credit) AS [دائن],
                     CASE WHEN SUM(l.Debit) > SUM(l.Credit) THEN SUM(l.Debit) - SUM(l.Credit) ELSE 0 END AS [رصيد مدين],
                     CASE WHEN SUM(l.Credit) > SUM(l.Debit) THEN SUM(l.Credit) - SUM(l.Debit) ELSE 0 END AS [رصيد دائن]
              FROM Accounts a
              JOIN JournalLines l ON l.AccountId = a.Id
              JOIN JournalEntries e ON e.Id = l.EntryId
              WHERE e.EDate <= @t
              GROUP BY a.Code, a.Name
              ORDER BY a.Code", ("@t", dtpTo.Value.Date));
        AddTotals(dt, "مدين", "دائن", "رصيد مدين", "رصيد دائن");
        return dt;
    }

    DataTable IncomeStatement()
    {
        var dt = Db.Query(
            @"SELECT a.Code AS [الكود], a.Name AS [الحساب],
                     CASE a.Type WHEN 4 THEN N'إيراد' ELSE N'مصروف' END AS [النوع],
                     CASE a.Type WHEN 4 THEN SUM(l.Credit - l.Debit) ELSE SUM(l.Debit - l.Credit) END AS [المبلغ]
              FROM Accounts a
              JOIN JournalLines l ON l.AccountId = a.Id
              JOIN JournalEntries e ON e.Id = l.EntryId
              WHERE a.Type IN (4,5) AND e.EDate BETWEEN @f AND @t
              GROUP BY a.Code, a.Name, a.Type
              ORDER BY a.Type, a.Code", ("@f", dtpFrom.Value.Date), ("@t", dtpTo.Value.Date));

        decimal rev = 0, exp = 0;
        foreach (DataRow r in dt.Rows)
        {
            if (Ui.S(r["النوع"]) == "إيراد") rev += Convert.ToDecimal(r["المبلغ"]);
            else exp += Convert.ToDecimal(r["المبلغ"]);
        }
        dt.Rows.Add("", "إجمالي الإيرادات", "", rev);
        dt.Rows.Add("", "إجمالي المصروفات", "", exp);
        dt.Rows.Add("", "صافي الربح / (الخسارة)", "", rev - exp);
        return dt;
    }

    DataTable InvoicesReport(int kind)
    {
        var dt = Db.Query(
            @"SELECT ISNULL(p.Name, N'نقدي') AS [الطرف], i.Num AS [الرقم], i.InvDate AS [التاريخ],
                     i.Total AS [الإجمالي], i.Discount AS [الخصم], i.Net AS [الصافي], i.Paid AS [المدفوع],
                     i.Net - i.Paid AS [المتبقي]
              FROM Invoices i LEFT JOIN Parties p ON p.Id = i.PartyId
              WHERE i.Kind = @k AND i.InvDate BETWEEN @f AND @t
              ORDER BY i.InvDate, i.Num",
            ("@k", kind), ("@f", dtpFrom.Value.Date), ("@t", dtpTo.Value.Date));
        AddTotals(dt, "الإجمالي", "الخصم", "الصافي", "المدفوع", "المتبقي");
        return dt;
    }

    DataTable CategoriesReport() =>
        Db.Query(
            @"SELECT c.Name AS [البند], CASE v.Kind WHEN 3 THEN N'مصروف' ELSE N'إيراد' END AS [النوع],
                     SUM(v.Amount) AS [الإجمالي]
              FROM Vouchers v JOIN Categories c ON c.Id = v.CategoryId
              WHERE v.Kind IN (3,4) AND v.VDate BETWEEN @f AND @t
              GROUP BY c.Name, v.Kind
              ORDER BY v.Kind, c.Name",
            ("@f", dtpFrom.Value.Date), ("@t", dtpTo.Value.Date));

    // ---------- export ----------

    static string Q(string? s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";

    void Export()
    {
        if (grid.DataSource is not DataTable dt || dt.Rows.Count == 0) { Ui.Msg("لا توجد بيانات للتصدير."); return; }
        using var sfd = new SaveFileDialog { Filter = "CSV (Excel)|*.csv", FileName = "report.csv" };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", dt.Columns.Cast<DataColumn>().Select(c => Q(c.ColumnName))));
        foreach (DataRow r in dt.Rows)
        {
            sb.AppendLine(string.Join(",", r.ItemArray.Select(v =>
                v is DateTime d ? Q(d.ToString("yyyy-MM-dd"))
                : Q(Convert.ToString(v, CultureInfo.InvariantCulture)))));
        }
        File.WriteAllText(sfd.FileName, sb.ToString(), new UTF8Encoding(true));
        Ui.Msg("تم التصدير.");
    }
}
