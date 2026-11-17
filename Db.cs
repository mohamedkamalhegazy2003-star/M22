using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace M22;

public class AppConfig
{
    public string Server { get; set; } = ".\\SQLEXPRESS";
    public bool WindowsAuth { get; set; } = true;
    public string User { get; set; } = "sa";
    public string Password { get; set; } = "";
    public string Database { get; set; } = "M22DB";
    public string CompanyName { get; set; } = "اسم الشركة";

    static string FilePath => Path.Combine(AppContext.BaseDirectory, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath)) ?? new AppConfig();
        }
        catch { }
        return new AppConfig();
    }

    public void Save() =>
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));

    public string ConnStr(string? db = null)
    {
        var b = new SqlConnectionStringBuilder
        {
            DataSource = Server,
            InitialCatalog = db ?? Database,
            TrustServerCertificate = true,
            ConnectTimeout = 8
        };
        b.ConnectionString += ";Encrypt=False";
        if (WindowsAuth) b.IntegratedSecurity = true;
        else { b.UserID = User; b.Password = Password; }
        return b.ConnectionString;
    }
}

public static class Db
{
    public static AppConfig Cfg = AppConfig.Load();

    public static SqlConnection Open()
    {
        var c = new SqlConnection(Cfg.ConnStr());
        c.Open();
        return c;
    }

    static SqlCommand Cmd(SqlConnection c, SqlTransaction? t, string sql, (string, object?)[] p)
    {
        var cmd = c.CreateCommand();
        cmd.Transaction = t;
        cmd.CommandText = sql;
        foreach (var (n, v) in p) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        return cmd;
    }

    public static DataTable Query(string sql, params (string, object?)[] p)
    {
        using var c = Open();
        return Query(c, null, sql, p);
    }

    public static DataTable Query(SqlConnection c, SqlTransaction? t, string sql, params (string, object?)[] p)
    {
        using var cmd = Cmd(c, t, sql, p);
        using var da = new SqlDataAdapter(cmd);
        var dt = new DataTable();
        da.Fill(dt);
        return dt;
    }

    public static int Exec(string sql, params (string, object?)[] p)
    {
        using var c = Open();
        return Exec(c, null, sql, p);
    }

    public static int Exec(SqlConnection c, SqlTransaction? t, string sql, params (string, object?)[] p)
    {
        using var cmd = Cmd(c, t, sql, p);
        return cmd.ExecuteNonQuery();
    }

    public static object? Scalar(string sql, params (string, object?)[] p)
    {
        using var c = Open();
        return Scalar(c, null, sql, p);
    }

    public static object? Scalar(SqlConnection c, SqlTransaction? t, string sql, params (string, object?)[] p)
    {
        using var cmd = Cmd(c, t, sql, p);
        var o = cmd.ExecuteScalar();
        return o == DBNull.Value ? null : o;
    }

    // ---------- Database creation ----------

    static readonly string[] Tables =
    {
        @"IF OBJECT_ID('Users') IS NULL CREATE TABLE Users(
            Id INT IDENTITY PRIMARY KEY, Username NVARCHAR(50) NOT NULL UNIQUE, FullName NVARCHAR(100) NULL,
            PasswordHash NVARCHAR(100) NOT NULL, IsAdmin BIT NOT NULL DEFAULT 1)",
        @"IF OBJECT_ID('Accounts') IS NULL CREATE TABLE Accounts(
            Id INT IDENTITY PRIMARY KEY, Code NVARCHAR(20) NOT NULL UNIQUE, Name NVARCHAR(150) NOT NULL,
            ParentId INT NULL, Type TINYINT NOT NULL)",
        @"IF OBJECT_ID('Stores') IS NULL CREATE TABLE Stores(
            Id INT IDENTITY PRIMARY KEY, Name NVARCHAR(100) NOT NULL)",
        @"IF OBJECT_ID('Items') IS NULL CREATE TABLE Items(
            Id INT IDENTITY PRIMARY KEY, Code NVARCHAR(30) NULL, Name NVARCHAR(150) NOT NULL, Unit NVARCHAR(30) NULL,
            CostPrice DECIMAL(18,4) NOT NULL DEFAULT 0, SalePrice DECIMAL(18,4) NOT NULL DEFAULT 0,
            ReorderLevel DECIMAL(18,3) NOT NULL DEFAULT 0)",
        @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Items_Code')
            CREATE UNIQUE INDEX UX_Items_Code ON Items(Code) WHERE Code IS NOT NULL",
        @"IF OBJECT_ID('Parties') IS NULL CREATE TABLE Parties(
            Id INT IDENTITY PRIMARY KEY, Name NVARCHAR(150) NOT NULL, Phone NVARCHAR(50) NULL, Address NVARCHAR(250) NULL,
            Kind TINYINT NOT NULL, OpeningBalance DECIMAL(18,2) NOT NULL DEFAULT 0)",
        @"IF OBJECT_ID('Treasuries') IS NULL CREATE TABLE Treasuries(
            Id INT IDENTITY PRIMARY KEY, Name NVARCHAR(100) NOT NULL, Kind NVARCHAR(20) NULL, AccountId INT NULL)",
        @"IF OBJECT_ID('Categories') IS NULL CREATE TABLE Categories(
            Id INT IDENTITY PRIMARY KEY, Name NVARCHAR(100) NOT NULL, Kind TINYINT NOT NULL, AccountId INT NULL)",
        @"IF OBJECT_ID('Invoices') IS NULL CREATE TABLE Invoices(
            Id INT IDENTITY PRIMARY KEY, Kind TINYINT NOT NULL, Num INT NOT NULL, InvDate DATE NOT NULL,
            PartyId INT NULL, StoreId INT NOT NULL, TreasuryId INT NULL,
            Total DECIMAL(18,2) NOT NULL, Discount DECIMAL(18,2) NOT NULL, Net DECIMAL(18,2) NOT NULL,
            Paid DECIMAL(18,2) NOT NULL, Notes NVARCHAR(300) NULL)",
        @"IF OBJECT_ID('InvoiceLines') IS NULL CREATE TABLE InvoiceLines(
            Id INT IDENTITY PRIMARY KEY, InvoiceId INT NOT NULL, ItemId INT NOT NULL,
            Qty DECIMAL(18,3) NOT NULL, Price DECIMAL(18,4) NOT NULL, Total DECIMAL(18,2) NOT NULL)",
        @"IF OBJECT_ID('StockMoves') IS NULL CREATE TABLE StockMoves(
            Id INT IDENTITY PRIMARY KEY, MoveDate DATE NOT NULL, ItemId INT NOT NULL, StoreId INT NOT NULL,
            Qty DECIMAL(18,3) NOT NULL, RefKind NVARCHAR(10) NOT NULL, RefId INT NOT NULL)",
        @"IF OBJECT_ID('Vouchers') IS NULL CREATE TABLE Vouchers(
            Id INT IDENTITY PRIMARY KEY, Kind TINYINT NOT NULL, Num INT NOT NULL, VDate DATE NOT NULL,
            PartyId INT NULL, CategoryId INT NULL, TreasuryId INT NOT NULL,
            Amount DECIMAL(18,2) NOT NULL, Notes NVARCHAR(300) NULL)",
        @"IF OBJECT_ID('JournalEntries') IS NULL CREATE TABLE JournalEntries(
            Id INT IDENTITY PRIMARY KEY, EDate DATE NOT NULL, Descr NVARCHAR(300) NULL,
            RefKind NVARCHAR(10) NOT NULL, RefId INT NOT NULL)",
        @"IF OBJECT_ID('JournalLines') IS NULL CREATE TABLE JournalLines(
            Id INT IDENTITY PRIMARY KEY, EntryId INT NOT NULL, AccountId INT NOT NULL,
            Debit DECIMAL(18,2) NOT NULL DEFAULT 0, Credit DECIMAL(18,2) NOT NULL DEFAULT 0)"
    };

    public static void EnsureDatabase()
    {
        using (var m = new SqlConnection(Cfg.ConnStr("master")))
        {
            m.Open();
            using var cmd = m.CreateCommand();
            var name = Cfg.Database;
            cmd.CommandText = $"IF DB_ID(N'{name.Replace("'", "''")}') IS NULL CREATE DATABASE [{name.Replace("]", "]]")}]";
            cmd.ExecuteNonQuery();
        }

        using var c = Open();
        foreach (var s in Tables)
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = s;
            cmd.ExecuteNonQuery();
        }
        Seed(c);
    }

    static void Seed(SqlConnection c)
    {
        if (Convert.ToInt32(Scalar(c, null, "SELECT COUNT(*) FROM Accounts")) == 0)
        {
            void A(string code, string name, string? parent, int type) =>
                Exec(c, null,
                    "INSERT INTO Accounts(Code,Name,ParentId,Type) VALUES(@c,@n,(SELECT Id FROM Accounts WHERE Code=@p),@t)",
                    ("@c", code), ("@n", name), ("@p", parent), ("@t", type));

            A("1", "الأصول", null, 1);
            A("11", "النقدية والبنوك", "1", 1);
            A("1101", "الخزينة الرئيسية", "11", 1);
            A("1201", "العملاء", "1", 1);
            A("1301", "المخزون", "1", 1);
            A("2", "الخصوم", null, 2);
            A("2101", "الموردون", "2", 2);
            A("3", "حقوق الملكية", null, 3);
            A("3101", "رأس المال", "3", 3);
            A("4", "الإيرادات", null, 4);
            A("4101", "المبيعات", "4", 4);
            A("4102", "مرتجعات المبيعات", "4", 4);
            A("42", "إيرادات أخرى", "4", 4);
            A("4201", "إيرادات متنوعة", "42", 4);
            A("5", "المصروفات", null, 5);
            A("5101", "تكلفة البضاعة المباعة", "5", 5);
            A("52", "مصروفات عامة", "5", 5);
            A("5201", "مصروفات متنوعة", "52", 5);
        }

        if (Convert.ToInt32(Scalar(c, null, "SELECT COUNT(*) FROM Stores")) == 0)
            Exec(c, null, "INSERT INTO Stores(Name) VALUES(N'المخزن الرئيسي')");

        if (Convert.ToInt32(Scalar(c, null, "SELECT COUNT(*) FROM Treasuries")) == 0)
            Exec(c, null,
                "INSERT INTO Treasuries(Name,Kind,AccountId) VALUES(N'الخزينة الرئيسية',N'خزينة',(SELECT Id FROM Accounts WHERE Code='1101'))");

        if (Convert.ToInt32(Scalar(c, null, "SELECT COUNT(*) FROM Users")) == 0)
            Exec(c, null,
                "INSERT INTO Users(Username,FullName,PasswordHash,IsAdmin) VALUES(N'admin',N'مدير النظام',@h,1)",
                ("@h", Ui.Sha("admin")));
    }
}
