using System.Data;

namespace M22;

/// <summary>Log of invoices and vouchers with the ability to delete a document.</summary>
public class DocumentsForm : BaseForm
{
    static readonly string[] Types =
    {
        "فواتير مبيعات", "فواتير مشتريات", "مرتجعات مبيعات", "مرتجعات مشتريات",
        "سندات استلام نقدي", "سندات دفع نقدي", "المصروفات", "الإيرادات"
    };

    readonly ComboBox cmb = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    readonly DateTimePicker dtpFrom = new() { Format = DateTimePickerFormat.Short, Width = 120 };
    readonly DateTimePicker dtpTo = new() { Format = DateTimePickerFormat.Short, Width = 120 };
    readonly DataGridView grid = new();
    readonly DataGridView lines = new();

    public DocumentsForm()
    {
        Text = "سجل المستندات";
        Size = new Size(1000, 680);

        cmb.Items.AddRange(Types);
        cmb.SelectedIndex = 0;
        var today = DateTime.Today;
        dtpFrom.Value = new DateTime(today.Year, today.Month, 1);
        dtpTo.Value = today;

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(8), RightToLeft = RightToLeft.Yes };
        top.Controls.Add(cmb);
        top.Controls.Add(new Label { Text = "من", AutoSize = true, Margin = new Padding(8, 8, 2, 0) });
        top.Controls.Add(dtpFrom);
        top.Controls.Add(new Label { Text = "إلى", AutoSize = true, Margin = new Padding(8, 8, 2, 0) });
        top.Controls.Add(dtpTo);
        top.Controls.Add(Ui.Btn("عرض", (s, e) => LoadDocs()));
        top.Controls.Add(Ui.Btn("حذف المستند", (s, e) => DeleteDoc(), Color.FromArgb(192, 57, 43)));

        Ui.StyleGrid(grid);
        grid.Dock = DockStyle.Fill;
        grid.SelectionChanged += (s, e) => ShowLines();

        Ui.StyleGrid(lines);
        lines.Dock = DockStyle.Bottom;
        lines.Height = 200;

        Controls.Add(grid);
        Controls.Add(lines);
        Controls.Add(top);

        cmb.SelectedIndexChanged += (s, e) => LoadDocs();
        LoadDocs();
    }

    void LoadDocs()
    {
        int i = cmb.SelectedIndex;
        DataTable dt;
        var f = dtpFrom.Value.Date;
        var t = dtpTo.Value.Date;
        if (i < 4)
        {
            dt = Db.Query(
                @"SELECT i.Id, i.Num AS [الرقم], i.InvDate AS [التاريخ], ISNULL(p.Name, N'نقدي') AS [الطرف],
                         s.Name AS [المخزن], i.Net AS [الصافي], i.Paid AS [المدفوع], i.Notes AS [ملاحظات]
                  FROM Invoices i
                  LEFT JOIN Parties p ON p.Id = i.PartyId
                  LEFT JOIN Stores s ON s.Id = i.StoreId
                  WHERE i.Kind = @k AND i.InvDate BETWEEN @f AND @t
                  ORDER BY i.InvDate DESC, i.Num DESC",
                ("@k", i + 1), ("@f", f), ("@t", t));
        }
        else
        {
            dt = Db.Query(
                @"SELECT v.Id, v.Num AS [الرقم], v.VDate AS [التاريخ], ISNULL(p.Name, ISNULL(c.Name, N'')) AS [الطرف / البند],
                         tr.Name AS [الخزينة], v.Amount AS [المبلغ], v.Notes AS [ملاحظات]
                  FROM Vouchers v
                  LEFT JOIN Parties p ON p.Id = v.PartyId
                  LEFT JOIN Categories c ON c.Id = v.CategoryId
                  LEFT JOIN Treasuries tr ON tr.Id = v.TreasuryId
                  WHERE v.Kind = @k AND v.VDate BETWEEN @f AND @t
                  ORDER BY v.VDate DESC, v.Num DESC",
                ("@k", i - 3), ("@f", f), ("@t", t));
        }
        grid.DataSource = dt;
        grid.Columns["Id"]!.Visible = false;
        Ui.FormatGrid(grid);
        lines.Visible = i < 4;
        ShowLines();
    }

    void ShowLines()
    {
        if (cmb.SelectedIndex >= 4 || grid.CurrentRow == null || !grid.Columns.Contains("Id"))
        {
            lines.DataSource = null;
            return;
        }
        int id = Convert.ToInt32(grid.CurrentRow.Cells["Id"].Value);
        lines.DataSource = Db.Query(
            @"SELECT i.Name AS [الصنف], l.Qty AS [الكمية], l.Price AS [السعر], l.Total AS [الإجمالي]
              FROM InvoiceLines l JOIN Items i ON i.Id = l.ItemId WHERE l.InvoiceId = @id ORDER BY l.Id",
            ("@id", id));
        Ui.FormatGrid(lines);
    }

    void DeleteDoc()
    {
        if (grid.CurrentRow == null) return;
        int id = Convert.ToInt32(grid.CurrentRow.Cells["Id"].Value);
        if (!Ui.Ask("حذف المستند المحدد مع قيوده وحركاته المخزنية؟")) return;
        try
        {
            if (cmb.SelectedIndex < 4) Svc.DeleteInvoice(id);
            else Svc.DeleteVoucher(id);
            LoadDocs();
        }
        catch (Exception ex) { Ui.Msg("تعذر الحذف: " + ex.Message, true); }
    }
}
