using Microsoft.Data.SqlClient;

namespace M22;

/// <summary>Business logic: saving documents with their journal entries and stock movements.</summary>
public static class Svc
{
    public record Line(int ItemId, decimal Qty, decimal Price);

    static int AccId(SqlConnection c, SqlTransaction t, string code)
    {
        var id = Convert.ToInt32(Db.Scalar(c, t, "SELECT Id FROM Accounts WHERE Code=@c", ("@c", code)));
        if (id == 0) throw new Exception("الحساب الأساسي " + code + " غير موجود في شجرة الحسابات");
        return id;
    }

    static int TreasuryAcc(SqlConnection c, SqlTransaction t, int treasuryId)
    {
        var id = Convert.ToInt32(Db.Scalar(c, t, "SELECT ISNULL(AccountId,0) FROM Treasuries WHERE Id=@i", ("@i", treasuryId)));
        if (id == 0) throw new Exception("الخزينة غير مرتبطة بحساب");
        return id;
    }

    static void Journal(SqlConnection c, SqlTransaction t, DateTime date, string desc, string refKind, int refId,
        List<(int acc, decimal dr, decimal cr)> lines)
    {
        if (lines.Sum(l => l.dr) != lines.Sum(l => l.cr))
            throw new Exception("القيد غير متوازن");

        var eid = Convert.ToInt32(Db.Scalar(c, t,
            "INSERT INTO JournalEntries(EDate,Descr,RefKind,RefId) VALUES(@d,@s,@k,@r); SELECT CAST(SCOPE_IDENTITY() AS INT)",
            ("@d", date), ("@s", desc), ("@k", refKind), ("@r", refId)));

        foreach (var l in lines)
        {
            if (l.dr == 0 && l.cr == 0) continue;
            Db.Exec(c, t,
                "INSERT INTO JournalLines(EntryId,AccountId,Debit,Credit) VALUES(@e,@a,@dr,@cr)",
                ("@e", eid), ("@a", l.acc), ("@dr", l.dr), ("@cr", l.cr));
        }
    }

    public static decimal Stock(int itemId, int storeId) =>
        Convert.ToDecimal(Db.Scalar(
            "SELECT ISNULL(SUM(Qty),0) FROM StockMoves WHERE ItemId=@i AND StoreId=@s",
            ("@i", itemId), ("@s", storeId)));

    static readonly string[] InvNames = { "", "فاتورة مبيعات", "فاتورة مشتريات", "مرتجع مبيعات", "مرتجع مشتريات" };

    /// <summary>kind: 1 sale, 2 purchase, 3 sale return, 4 purchase return. Returns the invoice number.</summary>
    public static int SaveInvoice(int kind, DateTime date, int? partyId, int storeId, int? treasuryId,
        decimal discount, decimal paid, string notes, List<Line> lines)
    {
        decimal total = lines.Sum(l => l.Qty * l.Price);
        decimal net = total - discount;
        decimal credit = net - paid;

        using var c = Db.Open();
        using var t = c.BeginTransaction();
        try
        {
            int num = Convert.ToInt32(Db.Scalar(c, t,
                "SELECT ISNULL(MAX(Num),0)+1 FROM Invoices WHERE Kind=@k", ("@k", kind)));

            int id = Convert.ToInt32(Db.Scalar(c, t,
                @"INSERT INTO Invoices(Kind,Num,InvDate,PartyId,StoreId,TreasuryId,Total,Discount,Net,Paid,Notes)
                  VALUES(@k,@n,@d,@p,@s,@tr,@tot,@dis,@net,@paid,@notes);
                  SELECT CAST(SCOPE_IDENTITY() AS INT)",
                ("@k", kind), ("@n", num), ("@d", date), ("@p", partyId), ("@s", storeId), ("@tr", treasuryId),
                ("@tot", total), ("@dis", discount), ("@net", net), ("@paid", paid), ("@notes", notes)));

            decimal cost = 0;
            decimal sign = (kind == 1 || kind == 4) ? -1 : 1;

            foreach (var l in lines)
            {
                decimal cp = Convert.ToDecimal(Db.Scalar(c, t, "SELECT CostPrice FROM Items WHERE Id=@i", ("@i", l.ItemId)));
                cost += l.Qty * cp;

                Db.Exec(c, t,
                    "INSERT INTO InvoiceLines(InvoiceId,ItemId,Qty,Price,Total) VALUES(@inv,@i,@q,@p,@t)",
                    ("@inv", id), ("@i", l.ItemId), ("@q", l.Qty), ("@p", l.Price), ("@t", l.Qty * l.Price));

                Db.Exec(c, t,
                    "INSERT INTO StockMoves(MoveDate,ItemId,StoreId,Qty,RefKind,RefId) VALUES(@d,@i,@s,@q,'INV',@r)",
                    ("@d", date), ("@i", l.ItemId), ("@s", storeId), ("@q", sign * l.Qty), ("@r", id));

                if (kind == 2 && l.Price > 0)
                    Db.Exec(c, t, "UPDATE Items SET CostPrice=@p WHERE Id=@i", ("@p", l.Price), ("@i", l.ItemId));
            }

            int cash = treasuryId.HasValue ? TreasuryAcc(c, t, treasuryId.Value) : 0;
            if (paid > 0 && cash == 0) throw new Exception("اختر الخزينة");

            int ar = AccId(c, t, "1201"), ap = AccId(c, t, "2101"), inv = AccId(c, t, "1301");
            int sales = AccId(c, t, "4101"), salesRet = AccId(c, t, "4102"), cogs = AccId(c, t, "5101");

            var j = new List<(int acc, decimal dr, decimal cr)>();
            switch (kind)
            {
                case 1:
                    j.Add((cash, paid, 0)); j.Add((ar, credit, 0)); j.Add((sales, 0, net));
                    j.Add((cogs, cost, 0)); j.Add((inv, 0, cost));
                    break;
                case 2:
                    j.Add((inv, net, 0)); j.Add((cash, 0, paid)); j.Add((ap, 0, credit));
                    break;
                case 3:
                    j.Add((salesRet, net, 0)); j.Add((cash, 0, paid)); j.Add((ar, 0, credit));
                    j.Add((inv, cost, 0)); j.Add((cogs, 0, cost));
                    break;
                case 4:
                    j.Add((cash, paid, 0)); j.Add((ap, credit, 0)); j.Add((inv, 0, net));
                    break;
            }
            Journal(c, t, date, InvNames[kind] + " رقم " + num, "INV", id, j);

            t.Commit();
            return num;
        }
        catch
        {
            t.Rollback();
            throw;
        }
    }

    /// <summary>kind: 1 receipt from customer, 2 payment to supplier, 3 expense, 4 revenue.</summary>
    public static int SaveVoucher(int kind, DateTime date, int? partyId, int? categoryId, int treasuryId,
        decimal amount, string notes)
    {
        using var c = Db.Open();
        using var t = c.BeginTransaction();
        try
        {
            int num = Convert.ToInt32(Db.Scalar(c, t,
                "SELECT ISNULL(MAX(Num),0)+1 FROM Vouchers WHERE Kind=@k", ("@k", kind)));

            int id = Convert.ToInt32(Db.Scalar(c, t,
                @"INSERT INTO Vouchers(Kind,Num,VDate,PartyId,CategoryId,TreasuryId,Amount,Notes)
                  VALUES(@k,@n,@d,@p,@c,@t,@a,@notes); SELECT CAST(SCOPE_IDENTITY() AS INT)",
                ("@k", kind), ("@n", num), ("@d", date), ("@p", partyId), ("@c", categoryId),
                ("@t", treasuryId), ("@a", amount), ("@notes", notes)));

            int cash = TreasuryAcc(c, t, treasuryId);
            int catAcc = 0;
            if (categoryId.HasValue)
                catAcc = Convert.ToInt32(Db.Scalar(c, t, "SELECT ISNULL(AccountId,0) FROM Categories WHERE Id=@i", ("@i", categoryId.Value)));

            var j = new List<(int acc, decimal dr, decimal cr)>();
            string desc;
            switch (kind)
            {
                case 1: j.Add((cash, amount, 0)); j.Add((AccId(c, t, "1201"), 0, amount)); desc = "استلام نقدي رقم " + num; break;
                case 2: j.Add((AccId(c, t, "2101"), amount, 0)); j.Add((cash, 0, amount)); desc = "دفع نقدي رقم " + num; break;
                case 3:
                    if (catAcc == 0) throw new Exception("بند المصروف غير مرتبط بحساب");
                    j.Add((catAcc, amount, 0)); j.Add((cash, 0, amount)); desc = "مصروف رقم " + num; break;
                default:
                    if (catAcc == 0) throw new Exception("بند الإيراد غير مرتبط بحساب");
                    j.Add((cash, amount, 0)); j.Add((catAcc, 0, amount)); desc = "إيراد رقم " + num; break;
            }
            if (!string.IsNullOrWhiteSpace(notes)) desc += " - " + notes;
            Journal(c, t, date, desc, "VCH", id, j);

            t.Commit();
            return num;
        }
        catch
        {
            t.Rollback();
            throw;
        }
    }

    static void DeleteJournal(SqlConnection c, SqlTransaction t, string refKind, int refId)
    {
        Db.Exec(c, t,
            "DELETE FROM JournalLines WHERE EntryId IN (SELECT Id FROM JournalEntries WHERE RefKind=@k AND RefId=@r)",
            ("@k", refKind), ("@r", refId));
        Db.Exec(c, t, "DELETE FROM JournalEntries WHERE RefKind=@k AND RefId=@r", ("@k", refKind), ("@r", refId));
    }

    public static void DeleteInvoice(int id)
    {
        using var c = Db.Open();
        using var t = c.BeginTransaction();
        try
        {
            DeleteJournal(c, t, "INV", id);
            Db.Exec(c, t, "DELETE FROM StockMoves WHERE RefKind='INV' AND RefId=@r", ("@r", id));
            Db.Exec(c, t, "DELETE FROM InvoiceLines WHERE InvoiceId=@r", ("@r", id));
            Db.Exec(c, t, "DELETE FROM Invoices WHERE Id=@r", ("@r", id));
            t.Commit();
        }
        catch { t.Rollback(); throw; }
    }

    public static void DeleteVoucher(int id)
    {
        using var c = Db.Open();
        using var t = c.BeginTransaction();
        try
        {
            DeleteJournal(c, t, "VCH", id);
            Db.Exec(c, t, "DELETE FROM Vouchers WHERE Id=@r", ("@r", id));
            t.Commit();
        }
        catch { t.Rollback(); throw; }
    }
}
