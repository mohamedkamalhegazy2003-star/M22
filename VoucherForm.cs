using System.Data;

namespace M22;

/// <summary>Cash vouchers. kind: 1 receipt from customer, 2 payment to supplier, 3 expense, 4 revenue.</summary>
public class VoucherForm : BaseForm
{
    static readonly string[] Titles = { "", "استلام نقدي من عميل", "دفع نقدي لمورد", "المصروفات", "الإيرادات" };
    static readonly string[] PartyLabels = { "", "العميل", "المورد", "بند المصروف", "بند الإيراد" };

    readonly int kind;
    readonly DateTimePicker dtp = new() { Format = DateTimePickerFormat.Short };
    readonly ComboBox cmbParty = new(), cmbTre = new();
    readonly TextBox txtAmt = new(), txtNotes = new();
    readonly DataGridView grid = new();

    public VoucherForm(int kind)
    {
        this.kind = kind;
        Text = Titles[kind];
        Size = new Size(760, 620);

        DataTable partyTable = kind switch
        {
            1 => Db.Query("SELECT Id, Name FROM Parties WHERE Kind=1 ORDER BY Name"),
            2 => Db.Query("SELECT Id, Name FROM Parties WHERE Kind=2 ORDER BY Name"),
            3 => Db.Query("SELECT Id, Name FROM Categories WHERE Kind=1 ORDER BY Name"),
            _ => Db.Query("SELECT Id, Name FROM Categories WHERE Kind=2 ORDER BY Name")
        };
        Ui.FillCombo(cmbParty, partyTable);
        Ui.FillCombo(cmbTre, Db.Query("SELECT Id, Name FROM Treasuries ORDER BY Id"));

        var tl = Ui.Form2();
        Ui.AddRow(tl, "التاريخ", dtp);
        Ui.AddRow(tl, PartyLabels[kind], cmbParty);
        Ui.AddRow(tl, "الخزينة / البنك", cmbTre);
        Ui.AddRow(tl, "المبلغ", txtAmt);
        Ui.AddRow(tl, "ملاحظات", txtNotes);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 54, Padding = new Padding(8), RightToLeft = RightToLeft.Yes };
        buttons.Controls.Add(Ui.Btn("حفظ", (s, e) => Save(), Color.FromArgb(39, 174, 96)));
        buttons.Controls.Add(Ui.Btn("إغلاق", (s, e) => Close(), Color.FromArgb(127, 140, 141)));

        Ui.StyleGrid(grid);
        grid.Dock = DockStyle.Fill;

        Controls.Add(grid);
        Controls.Add(buttons);
        Controls.Add(tl);

        LoadRecent();
    }

    void LoadRecent()
    {
        var dt = Db.Query(
            @"SELECT TOP 50 v.Num AS [الرقم], v.VDate AS [التاريخ], ISNULL(p.Name, ISNULL(c.Name, N'')) AS [الطرف / البند],
                     tr.Name AS [الخزينة], v.Amount AS [المبلغ], v.Notes AS [ملاحظات]
              FROM Vouchers v
              LEFT JOIN Parties p ON p.Id = v.PartyId
              LEFT JOIN Categories c ON c.Id = v.CategoryId
              LEFT JOIN Treasuries tr ON tr.Id = v.TreasuryId
              WHERE v.Kind = @k ORDER BY v.Id DESC", ("@k", kind));
        grid.DataSource = dt;
        Ui.FormatGrid(grid);
    }

    void Save()
    {
        var tre = Ui.Sel(cmbTre);
        if (tre == null) { Ui.Msg("اختر الخزينة.", true); return; }
        var sel = Ui.Sel(cmbParty);
        if (sel == null) { Ui.Msg("اختر " + PartyLabels[kind] + ".", true); return; }
        var amt = Ui.Dec(txtAmt.Text);
        if (amt <= 0) { Ui.Msg("أدخل مبلغاً صحيحاً.", true); return; }

        try
        {
            int? party = kind <= 2 ? sel : null;
            int? cat = kind > 2 ? sel : null;
            int num = Svc.SaveVoucher(kind, dtp.Value.Date, party, cat, tre.Value, amt, txtNotes.Text.Trim());
            Ui.Msg("تم الحفظ برقم " + num);
            txtAmt.Clear();
            txtNotes.Clear();
            LoadRecent();
        }
        catch (Exception ex) { Ui.Msg("تعذر الحفظ: " + ex.Message, true); }
    }
}
