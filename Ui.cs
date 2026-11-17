using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace M22;

public class BaseForm : Form
{
    public BaseForm()
    {
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = false;
        Font = Ui.Normal;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(245, 246, 248);
    }
}

public static class Session
{
    public static string User = "";
    public static string FullName = "";
}

public static class Ui
{
    public static readonly Font Normal = new("Tahoma", 10f);

    const MessageBoxOptions Rtl = MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading;

    public static void Msg(string text, bool error = false) =>
        MessageBox.Show(text, "M22", MessageBoxButtons.OK,
            error ? MessageBoxIcon.Error : MessageBoxIcon.Information,
            MessageBoxDefaultButton.Button1, Rtl);

    public static bool Ask(string text) =>
        MessageBox.Show(text, "M22", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2, Rtl) == DialogResult.Yes;

    public static string S(object? o) => Convert.ToString(o, CultureInfo.InvariantCulture) ?? "";

    /// <summary>Parses a number typed by the user (accepts Arabic-Indic digits and separators).</summary>
    public static decimal Dec(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        var sb = new StringBuilder();
        foreach (var ch in s.Trim())
        {
            if (ch >= '٠' && ch <= '٩') sb.Append((char)('0' + (ch - '٠')));
            else if (ch == '٫') sb.Append('.');
            else if (ch == ',' || ch == '٬') { }
            else sb.Append(ch);
        }
        return decimal.TryParse(sb.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0;
    }

    public static string Sha(string s) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

    public static void FillCombo(ComboBox cb, DataTable dt, string display = "Name", string value = "Id", string? blank = null)
    {
        if (blank != null)
        {
            var r = dt.NewRow();
            r[value] = DBNull.Value;
            r[display] = blank;
            dt.Rows.InsertAt(r, 0);
        }
        cb.DisplayMember = display;
        cb.ValueMember = value;
        cb.DataSource = dt;
        cb.DropDownStyle = ComboBoxStyle.DropDown;
        cb.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        cb.AutoCompleteSource = AutoCompleteSource.ListItems;
        cb.SelectedIndex = dt.Rows.Count > 0 ? 0 : -1;
    }

    public static int? Sel(ComboBox cb) => cb.SelectedValue is int i ? i : (int?)null;

    public static void StyleGrid(DataGridView g)
    {
        g.RightToLeft = RightToLeft.Yes;
        g.AllowUserToAddRows = false;
        g.AllowUserToDeleteRows = false;
        g.ReadOnly = true;
        g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        g.BackgroundColor = Color.White;
        g.RowHeadersVisible = false;
        g.EnableHeadersVisualStyles = false;
        g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(52, 73, 94);
        g.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        g.ColumnHeadersHeight = 34;
        g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(240, 244, 248);
        g.BorderStyle = BorderStyle.None;
    }

    public static void FormatGrid(DataGridView g)
    {
        foreach (DataGridViewColumn c in g.Columns)
        {
            if (c.ValueType == typeof(decimal) || c.ValueType == typeof(double))
                c.DefaultCellStyle.Format = "#,##0.00";
            else if (c.ValueType == typeof(DateTime))
                c.DefaultCellStyle.Format = "yyyy/MM/dd";
        }
    }

    public static Button Btn(string text, EventHandler click, Color? color = null)
    {
        var b = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(90, 32),
            Padding = new Padding(10, 3, 10, 3),
            FlatStyle = FlatStyle.Flat,
            BackColor = color ?? Color.FromArgb(52, 73, 94),
            ForeColor = Color.White,
            Margin = new Padding(4)
        };
        b.FlatAppearance.BorderSize = 0;
        b.Click += click;
        return b;
    }

    public static TableLayoutPanel Form2(int labelWidth = 130)
    {
        var tl = new TableLayoutPanel
        {
            ColumnCount = 2,
            RightToLeft = RightToLeft.Yes,
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12)
        };
        tl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelWidth));
        tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return tl;
    }

    public static void AddRow(TableLayoutPanel tl, string label, Control c)
    {
        int r = tl.RowCount++;
        tl.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        tl.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, r);
        c.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        tl.Controls.Add(c, 1, r);
    }
}
