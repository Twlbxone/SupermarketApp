using System;
using System.Data;
using System.Data.SQLite;      // NuGet: System.Data.SQLite.Core
using System.IO;
using System.Windows.Forms;

namespace SupermarketSystem
{
    /// <summary>
    /// كلاس مساعد للتعامل مع قاعدة بيانات SQLite.
    /// يتولى إنشاء القاعدة والجداول عند أول تشغيل، وتنفيذ الاستعلامات بمعاملات آمنة،
    /// وتنفيذ العمليات المركبة (وارد/تالف/جرد) داخل معاملة (Transaction) واحدة.
    /// </summary>
    public static class DatabaseHelper
    {
        // مسار ملف القاعدة بجانب ملف التشغيل ليسهل نقل المشروع
        private static readonly string DbPath =
            Path.Combine(Application.StartupPath, "supermarket.db");

        private static readonly string ConnStr =
            "Data Source=" + DbPath + ";Version=3;Foreign Keys=True;";

        /// <summary>إنشاء اتصال جديد (يُغلق تلقائياً عند استخدام using).</summary>
        private static SQLiteConnection GetConnection()
        {
            return new SQLiteConnection(ConnStr);
        }

        // ------------------------------------------------------------
        // التهيئة: إنشاء ملف القاعدة والجداول إن لم تكن موجودة
        // ------------------------------------------------------------
        public static void Initialize()
        {
            try
            {
                if (!File.Exists(DbPath))
                    SQLiteConnection.CreateFile(DbPath);

                using (var conn = GetConnection())
                {
                    conn.Open();
                    // تنفيذ مخطط الجداول (يمكن أيضاً قراءته من ملف SupermarketDB.sql)
                    using (var cmd = new SQLiteCommand(SchemaSql, conn))
                        cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("تعذّر تهيئة قاعدة البيانات:\n" + ex.Message,
                    "خطأ في قاعدة البيانات", MessageBoxButtons.OK,
                    MessageBoxIcon.Error,
                    MessageBoxDefaultButton.Button1,
                    MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            }
        }

        // ------------------------------------------------------------
        // دوال عامة للاستعلام والتنفيذ
        // ------------------------------------------------------------

        /// <summary>تنفيذ استعلام SELECT وإرجاع النتيجة كجدول (لربطه بـ DataGridView).</summary>
        public static DataTable GetTable(string sql, params SQLiteParameter[] prms)
        {
            var dt = new DataTable();
            try
            {
                using (var conn = GetConnection())
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    if (prms != null) cmd.Parameters.AddRange(prms);
                    using (var da = new SQLiteDataAdapter(cmd))
                        da.Fill(dt);
                }
            }
            catch (Exception ex) { ShowError("خطأ أثناء قراءة البيانات", ex); }
            return dt;
        }

        /// <summary>تنفيذ INSERT/UPDATE/DELETE وإرجاع عدد الصفوف المتأثرة (-1 عند الفشل).</summary>
        public static int Execute(string sql, params SQLiteParameter[] prms)
        {
            try
            {
                using (var conn = GetConnection())
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    if (prms != null) cmd.Parameters.AddRange(prms);
                    conn.Open();
                    return cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex) { ShowError("خطأ أثناء تنفيذ العملية", ex); return -1; }
        }

        /// <summary>تنفيذ استعلام يعيد قيمة واحدة (مثل COUNT أو SUM).</summary>
        public static object Scalar(string sql, params SQLiteParameter[] prms)
        {
            try
            {
                using (var conn = GetConnection())
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    if (prms != null) cmd.Parameters.AddRange(prms);
                    conn.Open();
                    return cmd.ExecuteScalar();
                }
            }
            catch (Exception ex) { ShowError("خطأ أثناء قراءة القيمة", ex); return null; }
        }

        /// <summary>تنفيذ INSERT وإرجاع المعرّف التلقائي الجديد (-1 عند الفشل).</summary>
        public static long Insert(string sql, params SQLiteParameter[] prms)
        {
            try
            {
                using (var conn = GetConnection())
                using (var cmd = new SQLiteCommand(sql + "; SELECT last_insert_rowid();", conn))
                {
                    if (prms != null) cmd.Parameters.AddRange(prms);
                    conn.Open();
                    return Convert.ToInt64(cmd.ExecuteScalar());
                }
            }
            catch (Exception ex) { ShowError("خطأ أثناء الإضافة", ex); return -1; }
        }

        /// <summary>اختصار لإنشاء معامل (Parameter) بدون تكرار الكود.</summary>
        public static SQLiteParameter P(string name, object value)
        {
            return new SQLiteParameter(name, value ?? DBNull.Value);
        }

        /// <summary>تحويل DateTime إلى نص بالصيغة المعتمدة في القاعدة.</summary>
        public static string D(DateTime d) { return d.ToString("yyyy-MM-dd"); }

        // ------------------------------------------------------------
        // العمليات المركبة (داخل معاملة واحدة لضمان سلامة البيانات)
        // ------------------------------------------------------------

        /// <summary>
        /// تسجيل بضاعة واردة: إضافة سجل في StockIn وزيادة رصيد المنتج وتحديث تاريخ الصلاحية.
        /// </summary>
        public static bool RegisterStockIn(int productId, int qty, DateTime supplyDate,
                                           DateTime newExpiry, int? companyId, string notes)
        {
            return RunInTransaction((conn, tx) =>
            {
                Exec(conn, tx,
                    @"INSERT INTO StockIn(ProductID,Quantity,SupplyDate,NewExpiryDate,CompanyID,Notes)
                      VALUES(@p,@q,@sd,@ne,@c,@n)",
                    P("@p", productId), P("@q", qty), P("@sd", D(supplyDate)),
                    P("@ne", D(newExpiry)), P("@c", companyId), P("@n", notes));

                // زيادة الرصيد وتحديث الصلاحية إن كانت الدفعة الجديدة أحدث
                Exec(conn, tx,
                    @"UPDATE Products
                      SET Quantity = Quantity + @q,
                          ExpiryDate = CASE WHEN @ne > ExpiryDate THEN @ne ELSE ExpiryDate END
                      WHERE ProductID = @p",
                    P("@q", qty), P("@ne", D(newExpiry)), P("@p", productId));
            });
        }

        /// <summary>
        /// تسجيل تالف: إنقاص الكمية وتسجيل السبب. يرفض العملية إن كانت الكمية أكبر من المتوفر.
        /// </summary>
        public static bool RegisterDamage(int productId, int qty, int reasonId,
                                          DateTime date, string notes)
        {
            return RunInTransaction((conn, tx) =>
            {
                int current = Convert.ToInt32(ExecScalar(conn, tx,
                    "SELECT Quantity FROM Products WHERE ProductID=@p", P("@p", productId)));
                if (qty > current)
                    throw new InvalidOperationException(
                        "كمية التالف (" + qty + ") أكبر من الكمية المتوفرة (" + current + ").");

                Exec(conn, tx,
                    @"INSERT INTO DamagedItems(ProductID,Quantity,ReasonID,DamageDate,Notes)
                      VALUES(@p,@q,@r,@d,@n)",
                    P("@p", productId), P("@q", qty), P("@r", reasonId),
                    P("@d", D(date)), P("@n", notes));

                Exec(conn, tx, "UPDATE Products SET Quantity = Quantity - @q WHERE ProductID=@p",
                    P("@q", qty), P("@p", productId));
            });
        }

        /// <summary>حفظ نتيجة جرد منتج (دون اعتماد). يُحسب الفرق = الفعلية - المسجلة.</summary>
        public static bool SaveAudit(int productId, int systemQty, int actualQty, DateTime date)
        {
            return Execute(
                @"INSERT INTO InventoryAudits(ProductID,SystemQty,ActualQty,Difference,AuditDate,IsApplied)
                  VALUES(@p,@s,@a,@d,@dt,0)",
                P("@p", productId), P("@s", systemQty), P("@a", actualQty),
                P("@d", actualQty - systemQty), P("@dt", D(date))) > 0;
        }

        /// <summary>
        /// تسجيل عملية جرد واعتمادها مباشرة: حفظ السجل (معتمد) وتحديث رصيد المنتج إلى الكمية الفعلية
        /// داخل معاملة واحدة. تُستخدم من شاشة الجرد الدوري.
        /// </summary>
        public static bool RecordAndApplyAudit(int productId, int systemQty, int actualQty, DateTime date)
        {
            return RunInTransaction((conn, tx) =>
            {
                Exec(conn, tx,
                    @"INSERT INTO InventoryAudits(ProductID,SystemQty,ActualQty,Difference,AuditDate,IsApplied)
                      VALUES(@p,@s,@a,@d,@dt,1)",
                    P("@p", productId), P("@s", systemQty), P("@a", actualQty),
                    P("@d", actualQty - systemQty), P("@dt", D(date)));
                Exec(conn, tx, "UPDATE Products SET Quantity=@a WHERE ProductID=@p",
                    P("@a", actualQty), P("@p", productId));
            });
        }

        /// <summary>اعتماد الجرد: تحديث رصيد المنتج إلى الكمية الفعلية ووسم السجل كمعتمد.</summary>
        public static bool ApplyAudit(int auditId)
        {
            return RunInTransaction((conn, tx) =>
            {
                Exec(conn, tx,
                    @"UPDATE Products
                      SET Quantity = (SELECT ActualQty FROM InventoryAudits WHERE AuditID=@a)
                      WHERE ProductID = (SELECT ProductID FROM InventoryAudits WHERE AuditID=@a)",
                    P("@a", auditId));
                Exec(conn, tx, "UPDATE InventoryAudits SET IsApplied=1 WHERE AuditID=@a",
                    P("@a", auditId));
            });
        }

        // ------------------------------------------------------------
        // استعلامات جاهزة للتنبيهات والإحصائيات
        // ------------------------------------------------------------

        /// <summary>المواد المنتهية أو التي ستنتهي خلال عدد أيام محدد (الافتراضي 15).</summary>
        public static DataTable GetExpiringProducts(int days)
        {
            return GetTable(
                @"SELECT ProductID, ProductName, Quantity, ExpiryDate,
                         CAST(julianday(ExpiryDate) - julianday(date('now','localtime')) AS INTEGER) AS DaysLeft
                  FROM Products
                  WHERE Quantity > 0 AND ExpiryDate <= date('now','localtime', '+' || @d || ' day')
                  ORDER BY ExpiryDate",
                P("@d", days));
        }

        /// <summary>قائمة النواقص التلقائية (من العرض v_Shortages).</summary>
        public static DataTable GetShortages()
        {
            return GetTable("SELECT * FROM v_Shortages ORDER BY MissingQty DESC");
        }

        /// <summary>إجمالي القيمة المالية للمخزون = مجموع (الكمية × سعر الشراء).</summary>
        public static decimal GetInventoryValue()
        {
            object o = Scalar("SELECT IFNULL(SUM(Quantity*PurchasePrice),0) FROM Products");
            return o == null ? 0 : Convert.ToDecimal(o);
        }

        /// <summary>نسبة امتلاء المستودع = مجموع الكميات ÷ السعة المحددة في الإعدادات (0-100).</summary>
        public static int GetWarehouseFillPercent()
        {
            object cap = Scalar("SELECT SettingValue FROM Settings WHERE SettingKey='WarehouseCapacity'");
            object tot = Scalar("SELECT IFNULL(SUM(Quantity),0) FROM Products");
            double capacity = cap == null ? 1 : Convert.ToDouble(cap);
            double total = tot == null ? 0 : Convert.ToDouble(tot);
            if (capacity <= 0) return 0;
            return (int)Math.Min(100, Math.Round(total * 100 / capacity));
        }

        // ------------------------------------------------------------
        // دوال داخلية مساعدة
        // ------------------------------------------------------------

        private static bool RunInTransaction(Action<SQLiteConnection, SQLiteTransaction> work)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();
                    using (var tx = conn.BeginTransaction())
                    {
                        try { work(conn, tx); tx.Commit(); return true; }
                        catch { tx.Rollback(); throw; }   // التراجع عن كل شيء عند أي خطأ
                    }
                }
            }
            catch (Exception ex) { ShowError("فشلت العملية وتم التراجع عنها", ex); return false; }
        }

        private static void Exec(SQLiteConnection c, SQLiteTransaction t, string sql, params SQLiteParameter[] prms)
        {
            using (var cmd = new SQLiteCommand(sql, c, t))
            {
                cmd.Parameters.AddRange(prms);
                cmd.ExecuteNonQuery();
            }
        }

        private static object ExecScalar(SQLiteConnection c, SQLiteTransaction t, string sql, params SQLiteParameter[] prms)
        {
            using (var cmd = new SQLiteCommand(sql, c, t))
            {
                cmd.Parameters.AddRange(prms);
                return cmd.ExecuteScalar();
            }
        }

        /// <summary>عرض رسالة خطأ عربية (من اليمين لليسار) للمستخدم.</summary>
        private static void ShowError(string title, Exception ex)
        {
            MessageBox.Show(ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error,
                MessageBoxDefaultButton.Button1,
                MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
        }

        // ------------------------------------------------------------
        // مخطط الجداول (نفس محتوى ملف SupermarketDB.sql)
        // ------------------------------------------------------------
        private const string SchemaSql = @"
PRAGMA foreign_keys = ON;
CREATE TABLE IF NOT EXISTS Categories (CategoryID INTEGER PRIMARY KEY AUTOINCREMENT, CategoryName TEXT NOT NULL UNIQUE);
CREATE TABLE IF NOT EXISTS StorageLocations (LocationID INTEGER PRIMARY KEY AUTOINCREMENT, LocationName TEXT NOT NULL UNIQUE);
CREATE TABLE IF NOT EXISTS DamageReasons (ReasonID INTEGER PRIMARY KEY AUTOINCREMENT, ReasonName TEXT NOT NULL UNIQUE);
CREATE TABLE IF NOT EXISTS Companies (CompanyID INTEGER PRIMARY KEY AUTOINCREMENT, CompanyName TEXT NOT NULL UNIQUE, ProductsType TEXT, Notes TEXT);
CREATE TABLE IF NOT EXISTS Representatives (RepID INTEGER PRIMARY KEY AUTOINCREMENT, RepName TEXT NOT NULL, Phone TEXT NOT NULL, CompanyID INTEGER NOT NULL,
  FOREIGN KEY (CompanyID) REFERENCES Companies(CompanyID) ON UPDATE CASCADE ON DELETE RESTRICT);
CREATE TABLE IF NOT EXISTS Products (
  ProductID INTEGER PRIMARY KEY AUTOINCREMENT, ProductName TEXT NOT NULL, CategoryID INTEGER NOT NULL, LocationID INTEGER NOT NULL,
  ShelfNumber TEXT NOT NULL, PurchasePrice REAL NOT NULL CHECK (PurchasePrice >= 0), SalePrice REAL NOT NULL CHECK (SalePrice >= 0),
  Quantity INTEGER NOT NULL DEFAULT 0 CHECK (Quantity >= 0), MinQuantity INTEGER NOT NULL DEFAULT 0 CHECK (MinQuantity >= 0),
  ExpiryDate TEXT NOT NULL, Status TEXT NOT NULL DEFAULT 'نشط' CHECK (Status IN ('نشط','معلق')), CompanyID INTEGER,
  CreatedAt TEXT NOT NULL DEFAULT (datetime('now','localtime')),
  FOREIGN KEY (CategoryID) REFERENCES Categories(CategoryID) ON UPDATE CASCADE ON DELETE RESTRICT,
  FOREIGN KEY (LocationID) REFERENCES StorageLocations(LocationID) ON UPDATE CASCADE ON DELETE RESTRICT,
  FOREIGN KEY (CompanyID) REFERENCES Companies(CompanyID) ON UPDATE CASCADE ON DELETE SET NULL);
CREATE TABLE IF NOT EXISTS StockIn (StockInID INTEGER PRIMARY KEY AUTOINCREMENT, ProductID INTEGER NOT NULL, Quantity INTEGER NOT NULL CHECK (Quantity > 0),
  SupplyDate TEXT NOT NULL, NewExpiryDate TEXT NOT NULL, CompanyID INTEGER, Notes TEXT,
  FOREIGN KEY (ProductID) REFERENCES Products(ProductID) ON DELETE CASCADE, FOREIGN KEY (CompanyID) REFERENCES Companies(CompanyID) ON DELETE SET NULL);
CREATE TABLE IF NOT EXISTS DamagedItems (DamageID INTEGER PRIMARY KEY AUTOINCREMENT, ProductID INTEGER NOT NULL, Quantity INTEGER NOT NULL CHECK (Quantity > 0),
  ReasonID INTEGER NOT NULL, DamageDate TEXT NOT NULL, Notes TEXT,
  FOREIGN KEY (ProductID) REFERENCES Products(ProductID) ON DELETE CASCADE, FOREIGN KEY (ReasonID) REFERENCES DamageReasons(ReasonID) ON DELETE RESTRICT);
CREATE TABLE IF NOT EXISTS InventoryAudits (AuditID INTEGER PRIMARY KEY AUTOINCREMENT, ProductID INTEGER NOT NULL, SystemQty INTEGER NOT NULL,
  ActualQty INTEGER NOT NULL, Difference INTEGER NOT NULL, AuditDate TEXT NOT NULL, IsApplied INTEGER NOT NULL DEFAULT 0 CHECK (IsApplied IN (0,1)),
  FOREIGN KEY (ProductID) REFERENCES Products(ProductID) ON DELETE CASCADE);
CREATE TABLE IF NOT EXISTS Settings (SettingKey TEXT PRIMARY KEY, SettingValue TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS idx_products_category ON Products(CategoryID);
CREATE INDEX IF NOT EXISTS idx_products_expiry ON Products(ExpiryDate);
CREATE INDEX IF NOT EXISTS idx_products_name ON Products(ProductName);
CREATE VIEW IF NOT EXISTS v_Shortages AS
SELECT p.ProductID, p.ProductName, c.CategoryName, p.Quantity, p.MinQuantity, (p.MinQuantity - p.Quantity) AS MissingQty,
       co.CompanyName, r.RepName, r.Phone
FROM Products p JOIN Categories c ON c.CategoryID = p.CategoryID
LEFT JOIN Companies co ON co.CompanyID = p.CompanyID
LEFT JOIN Representatives r ON r.CompanyID = co.CompanyID
WHERE p.Quantity <= p.MinQuantity AND p.Status = 'نشط';
INSERT OR IGNORE INTO Categories(CategoryName) VALUES ('منظفات'),('معلبات'),('غذائيات'),('ألبان'),('بسكويت'),('مشروبات'),('خضار وفواكه'),('لحوم ودواجن');
INSERT OR IGNORE INTO StorageLocations(LocationName) VALUES ('المستودع الرئيسي'),('البراد'),('صالة العرض'),('المجمّد');
INSERT OR IGNORE INTO DamageReasons(ReasonName) VALUES ('كسر'),('سوء تخزين'),('انتهاء صلاحية'),('تلف أثناء النقل'),('أخرى');
INSERT OR IGNORE INTO Settings(SettingKey, SettingValue) VALUES ('WarehouseCapacity','5000'),('ExpiryAlertDays','15');
";
    }
}
