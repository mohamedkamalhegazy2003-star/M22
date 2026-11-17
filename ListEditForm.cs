using System.Data;
using Microsoft.Data.SqlClient;

namespace M22;

public enum FT { Text, Number, Combo, Password, Check }

public class Field
{
    public string Col { get; }
    public string Label { get; }
    public FT Type { get; }
    public bool Required { get; }
    public string[]? Options { get; }
    public bool InList { get; }

    public Field(string col, string label, FT type = FT.Text, bool required = false,
        string[]? options = null, bool inList = true)
    {
        Col = col; Label = label; Type = type; Required = required; Options = options; InList = inList;
    }
}

public class TableSpec
{
    public string Title = "";
    public string Table = "";
    public string Where = "";
    public List<Field> Fields = new();
    public Dictionary<string, object?> Fixed = new();
    public string? DeleteGuard;
    public Action<SqlConnection, SqlTransaction, int, Dictionary<string, object?>>? AfterInsert;
}

/// <summary>Generic dialog that builds input controls from a field list.</summary>
public class FieldsDialog : BaseForm
{
    readonly List<Field> fields;
    readonly Dictionary<string, Control> ctrls = new();
    readonly bool editing;
    public Dictionary<string, object?> Result { get; } = new();

    public FieldsDialog(string title, List<Field> fields, Dictionary<string, object?>? init)
    {
        this.fields = fields;
        editing = init != null;
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(460, fields.Count * 38 + 24 + 56);

        var tl = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = fields.Count,
            Padding = new Padding(12),
            RightToLeft = RightToLeft.Yes
        };
        tl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;
        foreach (var f in fields)
        {
            tl.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            tl.Controls.Add(new Label { Text = f.Label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);

            object? iv = null;
            bool has = init != null && init.TryGetValue(f.Col, out iv) && iv != null;

            Control c;
            switch (f.Type)
            {
                case FT.Combo:
                    var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                    cb.Items.AddRange(f.Options ?? Array.Empty<string>());
                    if (has && cb.Items.Contains(Ui.S(iv))) cb.SelectedItem = Ui.S(iv);
                    else if (cb.Items.Count > 0) cb.SelectedIndex = 0;
                    c = cb;
                    break;
                case FT.Check:
                    c = new CheckBox { Checked = has ? Convert.ToBoolean(iv) : true };
                    break;
                case FT.Password:
                    c = new TextBox { UseSystemPasswordChar = true };
                    break;
                case FT.Number:
                    c = new TextBox { Text = has ? Convert.ToDecimal(iv).ToString("0.####") : "" };
                    break;
                default:
                    c = new TextBox { Text = has ? Ui.S(iv) : "" };
                    break;
            }
            c.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            tl.Controls.Add(c, 1, row);
            ctrls[f.Col] = c;
            row++;
        }

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 56, Padding = new Padding(8), RightToLeft = RightToLeft.Yes };
        var ok = Ui.Btn("حفظ", (s, e) => OnOk(), Color.FromArgb(39, 174, 96));
        var cancel = Ui.Btn("إلغاء", (s, e) => DialogResult = DialogResult.Cancel, Color.FromArgb(127, 140, 141));
        bottom.Controls.Add(ok);
        bottom.Controls.Add(cancel);

        Controls.Add(tl);
        Controls.Add(bottom);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    void OnOk()
    {
        Result.Clear();
        foreach (var f in fields)
        {
            var c = ctrls[f.Col];
            switch (f.Type)
            {
                case FT.Check:
                    Result[f.Col] = ((CheckBox)c).Checked;
                    break;
                case FT.Combo:
                    Result[f.Col] = ((ComboBox)c).SelectedItem?.ToString();
                    break;
                case FT.Number:
                    Result[f.Col] = Ui.Dec(c.Text);
                    break;
                case FT.Password:
                    if (f.Required && !editing && string.IsNullOrEmpty(c.Text)) { Missing(f); return; }
                    Result[f.Col] = c.Text;
                    break;
                default:
                    var s = c.Text.Trim();
                    if (f.Required && s == "") { Missing(f); return; }
                    Result[f.Col] = s == "" ? null : s;
                    break;
            }
        }
        DialogResult = DialogResult.OK;
    }

    void Missing(Field f) => Ui.Msg("الحقل مطلوب: " + f.Label, true);
}

/// <summary>Generic list + add/edit/delete screen for simple tables.</summary>
public class ListEditForm : BaseForm
{
    readonly TableSpec spec;
    readonly DataGridView grid = new();
    readonly TextBox txtSearch = new() { Width = 220, PlaceholderText = "بحث..." };
    DataTable dt = new();

    public ListEditForm(TableSpec spec)
    {
        this.spec = spec;
        Text = spec.Title;
        Size = new Size(900, 600);

        Ui.StyleGrid(grid);
        grid.Dock = DockStyle.Fill;
        grid.DoubleClick += (s, e) => EditRow();

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(8), RightToLeft = RightToLeft.Yes };
        top.Controls.Add(Ui.Btn("جديد", (s, e) => NewRow(), Color.FromArgb(39, 174, 96)));
        top.Controls.Add(Ui.Btn("تعديل", (s, e) => EditRow()));
        top.Controls.Add(Ui.Btn("حذف", (s, e) => DeleteRow(), Color.FromArgb(192, 57, 43)));
        txtSearch.Margin = new Padding(12, 8, 4, 4);
        txtSearch.TextChanged += (s, e) => ApplyFilter();
        top.Controls.Add(txtSearch);

        Controls.Add(grid);
        Controls.Add(top);

        LoadData();
    }

    IEnumerable<Field> Selectable => spec.Fields.Where(f => f.Type != FT.Password);

    void LoadData()
    {
        var cols = string.Join(",", Selectable.Select(f => "[" + f.Col + "]"));
        var sql = $"SELECT Id,{cols} FROM {spec.Table}" + (spec.Where != "" ? " WHERE " + spec.Where : "") + " ORDER BY Id";
        dt = Db.Query(sql);
        grid.DataSource = dt;
        grid.Columns["Id"]!.Visible = false;
        foreach (var f in Selectable)
        {
            var c = grid.Columns[f.Col]!;
            c.HeaderText = f.Label;
            c.Visible = f.InList;
        }
        Ui.FormatGrid(grid);
        ApplyFilter();
    }

    void ApplyFilter()
    {
        var s = txtSearch.Text.Trim().Replace("'", "''").Replace("[", "").Replace("]", "").Replace("%", "").Replace("*", "");
        if (s == "") { dt.DefaultView.RowFilter = ""; return; }
        var parts = Selectable
            .Where(f => f.Type == FT.Text || f.Type == FT.Combo || f.Type == FT.Number)
            .Select(f => $"Convert([{f.Col}], 'System.String') LIKE '%{s}%'");
        dt.DefaultView.RowFilter = string.Join(" OR ", parts);
    }

    int? CurrentId => grid.CurrentRow == null ? null : Convert.ToInt32(grid.CurrentRow.Cells["Id"].Value);

    object? Val(Field f, Dictionary<string, object?> r)
    {
        var v = r[f.Col];
        if (f.Type == FT.Password) return Ui.Sha(Ui.S(v));
        return v;
    }

    void NewRow()
    {
        using var dlg = new FieldsDialog(spec.Title + " - جديد", spec.Fields, null);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var cols = new List<string>();
            var ps = new List<(string, object?)>();
            foreach (var f in spec.Fields) { cols.Add(f.Col); ps.Add(("@" + f.Col, Val(f, dlg.Result))); }
            foreach (var kv in spec.Fixed) { cols.Add(kv.Key); ps.Add(("@" + kv.Key, kv.Value)); }

            var sql = $"INSERT INTO {spec.Table}({string.Join(",", cols.Select(x => "[" + x + "]"))}) " +
                      $"VALUES({string.Join(",", cols.Select(x => "@" + x))}); SELECT CAST(SCOPE_IDENTITY() AS INT)";
            using var c = Db.Open();
            using var t = c.BeginTransaction();
            try
            {
                int id = Convert.ToInt32(Db.Scalar(c, t, sql, ps.ToArray()));
                spec.AfterInsert?.Invoke(c, t, id, dlg.Result);
                t.Commit();
            }
            catch { t.Rollback(); throw; }
            LoadData();
        }
        catch (Exception ex) { Ui.Msg("تعذر الحفظ: " + ex.Message, true); }
    }

    void EditRow()
    {
        if (CurrentId is not int id) return;
        var row = ((DataRowView)grid.CurrentRow!.DataBoundItem).Row;
        var init = new Dictionary<string, object?>();
        foreach (var f in Selectable) init[f.Col] = row[f.Col] == DBNull.Value ? null : row[f.Col];
        foreach (var pf in spec.Fields.Where(x => x.Type == FT.Password)) init[pf.Col] = null;

        using var dlg = new FieldsDialog(spec.Title + " - تعديل", spec.Fields, init);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var sets = new List<string>();
            var ps = new List<(string, object?)> { ("@id", id) };
            foreach (var f in spec.Fields)
            {
                if (f.Type == FT.Password && string.IsNullOrEmpty(Ui.S(dlg.Result[f.Col]))) continue;
                sets.Add($"[{f.Col}]=@{f.Col}");
                ps.Add(("@" + f.Col, Val(f, dlg.Result)));
            }
            Db.Exec($"UPDATE {spec.Table} SET {string.Join(",", sets)} WHERE Id=@id", ps.ToArray());
            LoadData();
        }
        catch (Exception ex) { Ui.Msg("تعذر الحفظ: " + ex.Message, true); }
    }

    void DeleteRow()
    {
        if (CurrentId is not int id) return;
        try
        {
            if (spec.DeleteGuard != null && Convert.ToInt32(Db.Scalar(spec.DeleteGuard, ("@id", id))) > 0)
            {
                Ui.Msg("لا يمكن الحذف: السجل مستخدم في حركات أو محمي من الحذف.", true);
                return;
            }
            if (!Ui.Ask("هل تريد حذف السجل المحدد؟")) return;
            Db.Exec($"DELETE FROM {spec.Table} WHERE Id=@id", ("@id", id));
            LoadData();
        }
        catch (Exception ex) { Ui.Msg("تعذر الحذف: " + ex.Message, true); }
    }
}
