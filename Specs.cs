namespace M22;

/// <summary>Definitions of the simple list/edit screens.</summary>
public static class Specs
{
    public static TableSpec Items() => new()
    {
        Title = "الأصناف",
        Table = "Items",
        Fields =
        {
            new Field("Code", "الكود / الباركود"),
            new Field("Name", "اسم الصنف", required: true),
            new Field("Unit", "الوحدة"),
            new Field("CostPrice", "سعر التكلفة", FT.Number),
            new Field("SalePrice", "سعر البيع", FT.Number),
            new Field("ReorderLevel", "حد الطلب", FT.Number)
        },
        DeleteGuard = "SELECT COUNT(*) FROM InvoiceLines WHERE ItemId=@id",
        AfterInsert = (c, t, id, r) =>
            Db.Exec(c, t, "UPDATE Items SET Code=CAST(@i AS NVARCHAR(30)) WHERE Id=@i AND Code IS NULL", ("@i", id))
    };

    public static TableSpec Stores() => new()
    {
        Title = "المخازن",
        Table = "Stores",
        Fields = { new Field("Name", "اسم المخزن", required: true) },
        DeleteGuard = "SELECT COUNT(*) FROM StockMoves WHERE StoreId=@id"
    };

    static TableSpec Parties(int kind, string title) => new()
    {
        Title = title,
        Table = "Parties",
        Where = "Kind=" + kind,
        Fixed = { ["Kind"] = kind },
        Fields =
        {
            new Field("Name", "الاسم", required: true),
            new Field("Phone", "الهاتف"),
            new Field("Address", "العنوان"),
            new Field("OpeningBalance", "رصيد أول المدة", FT.Number)
        },
        DeleteGuard = "SELECT (SELECT COUNT(*) FROM Invoices WHERE PartyId=@id)+(SELECT COUNT(*) FROM Vouchers WHERE PartyId=@id)"
    };

    public static TableSpec Customers() => Parties(1, "العملاء");
    public static TableSpec Suppliers() => Parties(2, "الموردين");

    public static TableSpec Treasuries() => new()
    {
        Title = "الخزائن والبنوك",
        Table = "Treasuries",
        Fields =
        {
            new Field("Name", "الاسم", required: true),
            new Field("Kind", "النوع", FT.Combo, options: new[] { "خزينة", "بنك" })
        },
        DeleteGuard = "SELECT (SELECT COUNT(*) FROM Invoices WHERE TreasuryId=@id)+(SELECT COUNT(*) FROM Vouchers WHERE TreasuryId=@id)",
        AfterInsert = (c, t, id, r) =>
        {
            int parent = Convert.ToInt32(Db.Scalar(c, t, "SELECT Id FROM Accounts WHERE Code='11'"));
            int acc = Convert.ToInt32(Db.Scalar(c, t,
                "INSERT INTO Accounts(Code,Name,ParentId,Type) VALUES(@c,@n,@p,1); SELECT CAST(SCOPE_IDENTITY() AS INT)",
                ("@c", "11" + id.ToString("000")), ("@n", r["Name"]), ("@p", parent)));
            Db.Exec(c, t, "UPDATE Treasuries SET AccountId=@a WHERE Id=@i", ("@a", acc), ("@i", id));
        }
    };

    static TableSpec Categories(int kind, string title, string parentCode, int accType) => new()
    {
        Title = title,
        Table = "Categories",
        Where = "Kind=" + kind,
        Fixed = { ["Kind"] = kind },
        Fields = { new Field("Name", "اسم البند", required: true) },
        DeleteGuard = "SELECT COUNT(*) FROM Vouchers WHERE CategoryId=@id",
        AfterInsert = (c, t, id, r) =>
        {
            int parent = Convert.ToInt32(Db.Scalar(c, t, "SELECT Id FROM Accounts WHERE Code=@c", ("@c", parentCode)));
            int acc = Convert.ToInt32(Db.Scalar(c, t,
                "INSERT INTO Accounts(Code,Name,ParentId,Type) VALUES(@c,@n,@p,@ty); SELECT CAST(SCOPE_IDENTITY() AS INT)",
                ("@c", parentCode + id.ToString("000")), ("@n", r["Name"]), ("@p", parent), ("@ty", accType)));
            Db.Exec(c, t, "UPDATE Categories SET AccountId=@a WHERE Id=@i", ("@a", acc), ("@i", id));
        }
    };

    public static TableSpec ExpenseCategories() => Categories(1, "بنود المصروفات", "52", 5);
    public static TableSpec RevenueCategories() => Categories(2, "بنود الإيرادات", "42", 4);

    public static TableSpec Users() => new()
    {
        Title = "المستخدمين",
        Table = "Users",
        Fields =
        {
            new Field("Username", "اسم المستخدم", required: true),
            new Field("FullName", "الاسم الكامل"),
            new Field("PasswordHash", "كلمة المرور", FT.Password, required: true, inList: false),
            new Field("IsAdmin", "مدير", FT.Check)
        },
        DeleteGuard = "SELECT COUNT(*) FROM Users WHERE Id=@id AND Username='admin'"
    };
}
