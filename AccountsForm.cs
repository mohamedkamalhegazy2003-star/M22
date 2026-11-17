using System.Data;

namespace M22;

/// <summary>Chart of accounts (tree).</summary>
public class AccountsForm : BaseForm
{
    record Acc(int Id, string Code, string Name, int Type);

    static readonly string[] TypeNames = { "أصول", "خصوم", "حقوق ملكية", "إيرادات", "مصروفات" };
    static readonly HashSet<string> Protected = new()
    {
        "1", "11", "1101", "1201", "1301", "2", "2101", "3", "3101", "4", "4101", "4102", "42", "4201",
        "5", "5101", "52", "5201"
    };

    readonly TreeView tree = new() { Dock = DockStyle.Fill, Font = new Font("Tahoma", 11f), RightToLeft = RightToLeft.Yes, RightToLeftLayout = true };

    public AccountsForm()
    {
        Text = "شجرة الحسابات";
        Size = new Size(760, 640);

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(8), RightToLeft = RightToLeft.Yes };
        top.Controls.Add(Ui.Btn("حساب فرعي", (s, e) => AddAccount(true), Color.FromArgb(39, 174, 96)));
        top.Controls.Add(Ui.Btn("حساب رئيسي", (s, e) => AddAccount(false)));
        top.Controls.Add(Ui.Btn("تعديل", (s, e) => EditAccount()));
        top.Controls.Add(Ui.Btn("حذف", (s, e) => DeleteAccount(), Color.FromArgb(192, 57, 43)));

        Controls.Add(tree);
        Controls.Add(top);
        LoadTree();
    }

    void LoadTree()
    {
        tree.BeginUpdate();
        tree.Nodes.Clear();
        var dt = Db.Query("SELECT Id, Code, Name, ParentId, Type FROM Accounts ORDER BY Code");
        var nodes = new Dictionary<int, TreeNode>();
        foreach (DataRow r in dt.Rows)
        {
            var a = new Acc(Convert.ToInt32(r["Id"]), Ui.S(r["Code"]), Ui.S(r["Name"]), Convert.ToInt32(r["Type"]));
            nodes[a.Id] = new TreeNode(a.Code + " - " + a.Name) { Tag = a };
        }
        foreach (DataRow r in dt.Rows)
        {
            var node = nodes[Convert.ToInt32(r["Id"])];
            if (r["ParentId"] != DBNull.Value && nodes.TryGetValue(Convert.ToInt32(r["ParentId"]), out var parent))
                parent.Nodes.Add(node);
            else
                tree.Nodes.Add(node);
        }
        tree.ExpandAll();
        tree.EndUpdate();
    }

    Acc? Selected => tree.SelectedNode?.Tag as Acc;

    void AddAccount(bool child)
    {
        var parent = Selected;
        if (child && parent == null) { Ui.Msg("اختر الحساب الأب أولاً."); return; }

        var fields = new List<Field>
        {
            new Field("Code", "كود الحساب", required: true),
            new Field("Name", "اسم الحساب", required: true)
        };
        if (!child) fields.Add(new Field("Type", "النوع", FT.Combo, options: TypeNames));

        using var dlg = new FieldsDialog(child ? "حساب فرعي" : "حساب رئيسي", fields, null);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        int type = child ? parent!.Type : Array.IndexOf(TypeNames, Ui.S(dlg.Result["Type"])) + 1;
        try
        {
            Db.Exec("INSERT INTO Accounts(Code,Name,ParentId,Type) VALUES(@c,@n,@p,@t)",
                ("@c", dlg.Result["Code"]), ("@n", dlg.Result["Name"]),
                ("@p", child ? parent!.Id : (int?)null), ("@t", type));
            LoadTree();
        }
        catch (Exception ex) { Ui.Msg("تعذر الحفظ: " + ex.Message, true); }
    }

    void EditAccount()
    {
        var a = Selected;
        if (a == null) return;
        var fields = new List<Field>
        {
            new Field("Code", "كود الحساب", required: true),
            new Field("Name", "اسم الحساب", required: true)
        };
        var init = new Dictionary<string, object?> { ["Code"] = a.Code, ["Name"] = a.Name };
        using var dlg = new FieldsDialog("تعديل حساب", fields, init);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            Db.Exec("UPDATE Accounts SET Code=@c, Name=@n WHERE Id=@i",
                ("@c", dlg.Result["Code"]), ("@n", dlg.Result["Name"]), ("@i", a.Id));
            LoadTree();
        }
        catch (Exception ex) { Ui.Msg("تعذر الحفظ: " + ex.Message, true); }
    }

    void DeleteAccount()
    {
        var a = Selected;
        if (a == null) return;
        if (Protected.Contains(a.Code)) { Ui.Msg("هذا حساب أساسي في النظام ولا يمكن حذفه.", true); return; }
        try
        {
            int children = Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM Accounts WHERE ParentId=@i", ("@i", a.Id)));
            int used = Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM JournalLines WHERE AccountId=@i", ("@i", a.Id)));
            int linked = Convert.ToInt32(Db.Scalar(
                "SELECT (SELECT COUNT(*) FROM Treasuries WHERE AccountId=@i)+(SELECT COUNT(*) FROM Categories WHERE AccountId=@i)",
                ("@i", a.Id)));
            if (children > 0 || used > 0 || linked > 0)
            {
                Ui.Msg("لا يمكن الحذف: للحساب حسابات فرعية أو حركات أو ارتباط بخزينة/بند.", true);
                return;
            }
            if (!Ui.Ask("هل تريد حذف الحساب؟")) return;
            Db.Exec("DELETE FROM Accounts WHERE Id=@i", ("@i", a.Id));
            LoadTree();
        }
        catch (Exception ex) { Ui.Msg("تعذر الحذف: " + ex.Message, true); }
    }
}
