using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace SupermarketSystem
{
    /// <summary>
    /// النافذة الرئيسية لنظام إدارة وتشغيل سوبرماركت.
    /// الصنف مقسّم (partial) إلى ملفات حسب الوحدة الوظيفية:
    ///   Form1.cs            : الهيكل العام (القوائم، شريط الحالة، الدوال المساعدة، التحديث الشامل)
    ///   Form1.Products.cs   : التبويب 1 - إدارة المنتجات والمخزون
    ///   Form1.Alerts.cs     : التبويب 2 - الصلاحيات والتنبيهات
    ///   Form1.Movements.cs  : التبويب 3 - حركات المستودع (وارد / تالف / جرد)
    ///   Form1.Reps.cs       : التبويب 4 - المندوبون وقائمة النواقص
    ///   Form1.Stats.cs      : التبويب 5 - لوحة الإحصائيات
    /// واجهة المستخدم تُبنى بالكود (دون Designer) لسهولة الشرح والتعديل.
    /// </summary>
    public partial class Form1 : Form
    {
        #region لوحة الألوان (Flat UI)
        private static readonly Color ClrBack = ColorTranslator.FromHtml("#F4F6F8");     // خلفية عامة
        private static readonly Color ClrPrimary = ColorTranslator.FromHtml("#1F3A5F");  // أزرق داكن (العناوين)
        private static readonly Color ClrAccent = ColorTranslator.FromHtml("#2E86AB");   // أزرق فاتح (التحديد)
        private static readonly Color ClrSuccess = ColorTranslator.FromHtml("#27AE60");  // أخضر (إضافة/حفظ)
        private static readonly Color ClrWarning = ColorTranslator.FromHtml("#E67E22");  // برتقالي (تعديل)
        private static readonly Color ClrDanger = ColorTranslator.FromHtml("#C0392B");   // أحمر (حذف)
        private static readonly Color ClrGray = ColorTranslator.FromHtml("#7F8C8D");     // رمادي (مسح)
        private static readonly Color RowExpired = ColorTranslator.FromHtml("#F5B7B1");  // صف منتهي الصلاحية
        private static readonly Color RowNear = ColorTranslator.FromHtml("#FAD7A0");     // صف قريب الانتهاء
        private static readonly Color RowLow = ColorTranslator.FromHtml("#FCF3CF");      // صف تحت الحد الأدنى
        #endregion

        // العناصر المشتركة في النافذة
        private TabControl tabMain;
        private MenuStrip menu;
        private StatusStrip status;
        private ToolStripStatusLabel lblStatusCount, lblStatusDate, lblStatusTime;
        private ToolStripProgressBar tsPb;
        private Timer clock;

        // علَم يمنع تنفيذ أحداث التغيير أثناء تعبئة القوائم برمجياً
        private bool _loading = false;

        #region البناء والتهيئة

        /// <summary>باني النافذة: يضبط الخصائص العامة ثم يبني كل التبويبات.</summary>
        public Form1()
        {
            InitializeUI();
        }

        /// <summary>ضبط خصائص النافذة (عربي، من اليمين لليسار) وإنشاء المكونات.</summary>
        private void InitializeUI()
        {
            Text = "نظام إدارة وتشغيل سوبرماركت";
            Size = new Size(1280, 800);
            MinimumSize = new Size(1100, 700);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10F);
            BackColor = ClrBack;

            // الاتجاه من اليمين إلى اليسار لكل النافذة (يورَّث تلقائياً لكل العناصر)
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;

            // التبويبات الخمسة
            tabMain = new TabControl
            {
                Dock = DockStyle.Fill,
                RightToLeft = RightToLeft.Yes,
                RightToLeftLayout = true,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                Padding = new Point(14, 6)
            };
            BuildProductsTab();
            BuildAlertsTab();
            BuildMovementsTab();
            BuildRepsTab();
            BuildStatsTab();
            tabMain.SelectedIndexChanged += (s, e) => SetDefaultButtons();

            BuildMenu();
            BuildStatusBar();

            // ترتيب الإضافة مهم لعمل الـ Dock: المحتوى أولاً ثم الشريطان
            Controls.Add(tabMain);
            Controls.Add(status);
            Controls.Add(menu);
            MainMenuStrip = menu;

            Load += Form1_Load;
        }

        /// <summary>عند فتح النافذة: تحميل كل البيانات وإظهار تنبيه الصلاحية إن وُجد.</summary>
        private void Form1_Load(object sender, EventArgs e)
        {
            RefreshAll();
            SetDefaultButtons();
            ShowStartupAlerts();
        }

        /// <summary>
        /// إعادة تحميل كل الجداول والقوائم والإحصائيات.
        /// تُستدعى بعد أي عملية (إضافة/تعديل/حذف/وارد/تالف/جرد) لتبقى كل التبويبات متزامنة.
        /// </summary>
        private void RefreshAll()
        {
            try
            {
                LoadLookups();
                LoadProducts();
                LoadAlerts();
                LoadShortages();
                LoadReps();
                LoadMovementLogs();
                LoadStats();
                UpdateStatusBar();
            }
            catch (Exception ex)
            {
                Err("حدث خطأ أثناء تحديث البيانات", ex);
            }
        }

        /// <summary>
        /// يضبط AcceptButton (زر Enter) و CancelButton (زر Esc) حسب التبويب الظاهر حالياً.
        /// </summary>
        private void SetDefaultButtons()
        {
            switch (tabMain.SelectedIndex)
            {
                case 0: AcceptButton = btnAdd; CancelButton = btnClear; break;
                case 2:
                    if (tabMov.SelectedIndex == 0) { AcceptButton = btnStockIn; CancelButton = btnInClear; }
                    else if (tabMov.SelectedIndex == 1) { AcceptButton = btnDamage; CancelButton = btnDmgClear; }
                    else { AcceptButton = null; CancelButton = null; }
                    break;
                case 3: AcceptButton = btnRepAdd; CancelButton = btnRepClear; break;
                default: AcceptButton = null; CancelButton = null; break;
            }
        }

        /// <summary>رسالة تنبيه عند التشغيل إن كانت هناك مواد منتهية/قريبة الانتهاء أو نواقص.</summary>
        private void ShowStartupAlerts()
        {
            try
            {
                int exp = DatabaseHelper.GetExpiringProducts((int)numAlertDays.Value).Rows.Count;
                int low = DatabaseHelper.GetShortages().Rows.Count;
                if (exp > 0 || low > 0)
                {
                    Msg("تنبيه:\n• مواد منتهية أو قريبة الانتهاء: " + exp +
                        "\n• مواد وصلت للحد الأدنى للطلب: " + low +
                        "\n\nراجع تبويب «الصلاحيات والتنبيهات».",
                        "تنبيهات المخزون", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex) { Err("تعذّر فحص التنبيهات", ex); }
        }

        #endregion

        #region القائمة العلوية وشريط الحالة

        /// <summary>بناء MenuStrip: ملف / تنقل / مساعدة.</summary>
        private void BuildMenu()
        {
            menu = new MenuStrip { RightToLeft = RightToLeft.Yes, BackColor = ClrPrimary, ForeColor = Color.White };

            var mFile = new ToolStripMenuItem("ملف");
            mFile.DropDownItems.Add("تصدير جدول المنتجات إلى Excel (CSV)", null,
                (s, e) => ExportGrid(dgvProducts, "المنتجات.csv"));
            mFile.DropDownItems.Add("تصدير قائمة النواقص (CSV)", null,
                (s, e) => ExportGrid(dgvShortages, "قائمة_النواقص.csv"));
            mFile.DropDownItems.Add("نسخة احتياطية من قاعدة البيانات", null, (s, e) => BackupDatabase());
            mFile.DropDownItems.Add(new ToolStripSeparator());
            mFile.DropDownItems.Add("خروج", null, (s, e) => Close());

            var mNav = new ToolStripMenuItem("تنقل");
            string[] names = { "إدارة المنتجات والمخزون", "الصلاحيات والتنبيهات", "حركات المستودع",
                               "المندوبون وطلبات التوريد", "لوحة الإحصائيات" };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i; // نسخة محلية للاستخدام داخل التعبير
                mNav.DropDownItems.Add(names[i], null, (s, e) => tabMain.SelectedIndex = idx);
            }
            mNav.DropDownItems.Add(new ToolStripSeparator());
            var refresh = new ToolStripMenuItem("تحديث كل البيانات", null, (s, e) => RefreshAll())
            { ShortcutKeys = Keys.F5 };
            mNav.DropDownItems.Add(refresh);

            var mHelp = new ToolStripMenuItem("مساعدة");
            mHelp.DropDownItems.Add("حول البرنامج", null, (s, e) =>
                Msg("نظام إدارة وتشغيل سوبرماركت\nالمقرر: IPG201 - الجامعة الافتراضية السورية\n" +
                    "تقنية التنفيذ: C# / Windows Forms (.NET Framework) مع قاعدة بيانات SQLite",
                    "حول البرنامج", MessageBoxButtons.OK, MessageBoxIcon.Information));

            foreach (ToolStripMenuItem m in new[] { mFile, mNav, mHelp }) { m.ForeColor = Color.White; menu.Items.Add(m); }
            foreach (ToolStripMenuItem m in new[] { mFile, mNav, mHelp })
                foreach (ToolStripItem it in m.DropDownItems) it.ForeColor = Color.Black;
        }

        /// <summary>بناء StatusStrip: عدد الأصناف + نسبة الامتلاء + التاريخ + الوقت (ساعة تتحدث كل ثانية).</summary>
        private void BuildStatusBar()
        {
            status = new StatusStrip { RightToLeft = RightToLeft.Yes, BackColor = ClrPrimary };
            lblStatusCount = new ToolStripStatusLabel { ForeColor = Color.White, Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            tsPb = new ToolStripProgressBar { Width = 140, Style = ProgressBarStyle.Continuous };
            lblStatusDate = new ToolStripStatusLabel { ForeColor = Color.White };
            lblStatusTime = new ToolStripStatusLabel { ForeColor = Color.White };
            status.Items.AddRange(new ToolStripItem[] { lblStatusCount, tsPb, lblStatusDate, lblStatusTime });

            clock = new Timer { Interval = 1000 };
            clock.Tick += (s, e) =>
            {
                lblStatusDate.Text = "التاريخ: " + DateTime.Now.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
                lblStatusTime.Text = "الوقت: " + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            };
            clock.Start();
        }

        /// <summary>تحديث عدد الأصناف ونسبة امتلاء المستودع في شريط الحالة.</summary>
        private void UpdateStatusBar()
        {
            object c = DatabaseHelper.Scalar("SELECT COUNT(*) FROM Products");
            lblStatusCount.Text = "إجمالي الأصناف المسجلة: " + (c ?? 0) + "      |      امتلاء المستودع:";
            tsPb.Value = DatabaseHelper.GetWarehouseFillPercent();
        }

        #endregion

        #region تحميل القوائم المنسدلة (Lookups)

        /// <summary>تعبئة كل ComboBox من قاعدة البيانات مع الحفاظ على الاختيار الحالي قدر الإمكان.</summary>
        private void LoadLookups()
        {
            _loading = true;
            try
            {
                DataTable cats = DatabaseHelper.GetTable("SELECT CategoryID, CategoryName FROM Categories ORDER BY CategoryName");
                BindCombo(cboCategory, cats.Copy(), "CategoryID", "CategoryName", null);
                BindCombo(cboFilterCat, cats.Copy(), "CategoryID", "CategoryName", "— كل الأقسام —");

                DataTable locs = DatabaseHelper.GetTable("SELECT LocationID, LocationName FROM StorageLocations ORDER BY LocationName");
                BindCombo(cboLocation, locs, "LocationID", "LocationName", null);

                DataTable comps = DatabaseHelper.GetTable("SELECT CompanyID, CompanyName FROM Companies ORDER BY CompanyName");
                BindCombo(cboProdSupplier, comps.Copy(), "CompanyID", "CompanyName", "— بدون مورد —");
                BindCombo(cboInSupplier, comps.Copy(), "CompanyID", "CompanyName", "— بدون مورد —");
                BindCombo(cboRepCompany, comps.Copy(), "CompanyID", "CompanyName", null);

                DataTable reasons = DatabaseHelper.GetTable("SELECT ReasonID, ReasonName FROM DamageReasons ORDER BY ReasonID");
                BindCombo(cboReason, reasons, "ReasonID", "ReasonName", null);

                string prodSql = "SELECT ProductID, ProductID || ' - ' || ProductName AS Display FROM Products ORDER BY ProductName";
                BindCombo(cboInProduct, DatabaseHelper.GetTable(prodSql), "ProductID", "Display", null);
                BindCombo(cboDmgProduct, DatabaseHelper.GetTable(prodSql), "ProductID", "Display", null);
            }
            finally { _loading = false; }

            ShowCurrentQty(cboInProduct, lblInCurrent);
            ShowCurrentQty(cboDmgProduct, lblDmgCurrent);
        }

        /// <summary>
        /// ربط ComboBox بجدول. إن مُرِّر allText يُضاف صف أول (قيمته 0) مثل «الكل».
        /// للقوائم القابلة للكتابة (DropDown) يُحفظ النص المكتوب ويُعاد بعد الربط.
        /// </summary>
        private void BindCombo(ComboBox c, DataTable dt, string value, string display, string allText)
        {
            string oldText = c.Text;
            object oldValue = c.SelectedValue;

            if (allText != null)
            {
                DataRow r = dt.NewRow();
                r[0] = (long)0;
                r[1] = allText;
                dt.Rows.InsertAt(r, 0);
            }
            c.DataSource = null;
            c.DisplayMember = display;
            c.ValueMember = value;
            c.DataSource = dt;

            if (c.DropDownStyle == ComboBoxStyle.DropDown) { c.SelectedIndex = -1; c.Text = oldText; return; }

            if (oldValue != null && !(oldValue is DataRowView))
            {
                try { c.SelectedValue = oldValue; } catch { /* القيمة لم تعد موجودة */ }
            }
            if (c.SelectedIndex < 0 && c.Items.Count > 0) c.SelectedIndex = 0;
        }

        /// <summary>إرجاع رقم العنصر المختار في ComboBox (0 إن لم يوجد اختيار).</summary>
        private int ComboId(ComboBox c)
        {
            if (c.SelectedValue == null || c.SelectedValue is DataRowView) return 0;
            return Convert.ToInt32(c.SelectedValue);
        }

        #endregion

        #region دوال بناء الواجهة المساعدة

        /// <summary>إنشاء صفحة تبويب بنمط موحّد.</summary>
        private TabPage MkPage(string title)
        {
            return new TabPage(title)
            {
                BackColor = ClrBack,
                UseVisualStyleBackColor = false,
                Padding = new Padding(8),
                RightToLeft = RightToLeft.Yes,
                Font = new Font("Segoe UI", 10F)
            };
        }

        /// <summary>إنشاء GroupBox يتمدد حسب محتواه ويلتصق بأعلى الصفحة.</summary>
        private GroupBox MkGroup(string title)
        {
            return new GroupBox
            {
                Text = title,
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(8, 4, 8, 8),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = ClrPrimary,
                RightToLeft = RightToLeft.Yes
            };
        }

        /// <summary>
        /// جدول تنظيم (TableLayoutPanel) بعدد أعمدة زوجي: (عنوان | حقل) × n.
        /// يُملأ تلقائياً من اليمين إلى اليسار بسبب RightToLeft.
        /// </summary>
        private TableLayoutPanel MkTable(int columns)
        {
            var t = new TableLayoutPanel
            {
                ColumnCount = columns,
                Dock = DockStyle.Top,
                AutoSize = true,
                GrowStyle = TableLayoutPanelGrowStyle.AddRows,
                RightToLeft = RightToLeft.Yes,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                ForeColor = Color.Black
            };
            for (int i = 0; i < columns; i++)
                t.ColumnStyles.Add(i % 2 == 0 ? new ColumnStyle(SizeType.AutoSize)
                                              : new ColumnStyle(SizeType.Percent, 100f / (columns / 2)));
            return t;
        }

        /// <summary>إضافة زوج (عنوان + حقل إدخال) إلى جدول التنظيم.</summary>
        private void Put(TableLayoutPanel t, string label, Control c)
        {
            var lbl = new Label
            {
                Text = label,
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft, // في الاتجاه RTL تظهر على اليمين
                Margin = new Padding(8, 6, 4, 6)
            };
            c.Dock = DockStyle.Fill;
            c.Margin = new Padding(4, 4, 14, 4);
            t.Controls.Add(lbl);
            t.Controls.Add(c);
        }

        /// <summary>حاوية أزرار أفقية.</summary>
        private FlowLayoutPanel MkButtons()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(4),
                RightToLeft = RightToLeft.Yes,
                WrapContents = true
            };
        }

        /// <summary>زر بتصميم مسطح (Flat) بلون محدد.</summary>
        private Button MkBtn(string text, Color back, EventHandler click)
        {
            var b = new Button
            {
                Text = text,
                BackColor = back,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                AutoSize = true,
                MinimumSize = new Size(120, 36),
                Margin = new Padding(4),
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderSize = 0;
            if (click != null) b.Click += click;
            return b;
        }

        /// <summary>
        /// تنسيق DataGridView: للقراءة فقط (إلا إذا طُلب غير ذلك)، تحديد الصف كاملاً،
        /// بدون إضافة صفوف يدوية، وألوان Flat.
        /// </summary>
        private void StyleGrid(DataGridView g, bool readOnly = true)
        {
            g.Dock = DockStyle.Fill;
            g.ReadOnly = readOnly;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.MultiSelect = false;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.RowHeadersVisible = false;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            g.BackgroundColor = Color.White;
            g.BorderStyle = BorderStyle.None;
            g.RightToLeft = RightToLeft.Yes;
            g.EnableHeadersVisualStyles = false;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.ColumnHeadersHeight = 36;
            g.ColumnHeadersDefaultCellStyle.BackColor = ClrPrimary;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            g.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            g.DefaultCellStyle.SelectionBackColor = ClrAccent;
            g.DefaultCellStyle.SelectionForeColor = Color.White;
            g.DefaultCellStyle.Font = new Font("Segoe UI", 10F);
            g.RowTemplate.Height = 30;
            g.AlternatingRowsDefaultCellStyle.BackColor = ColorTranslator.FromHtml("#FAFBFC");
        }

        /// <summary>
        /// تخصيص عناوين الأعمدة بالعربية. تُمرَّر أزواج (اسم العمود، العنوان العربي)
        /// وأي عمود غير مذكور يُخفى (مثل الأعمدة التقنية ID).
        /// </summary>
        private void SetHeaders(DataGridView g, params string[] pairs)
        {
            var map = new Dictionary<string, string>();
            for (int i = 0; i + 1 < pairs.Length; i += 2) map[pairs[i]] = pairs[i + 1];
            foreach (DataGridViewColumn col in g.Columns)
            {
                if (map.ContainsKey(col.Name)) { col.HeaderText = map[col.Name]; col.Visible = true; }
                else col.Visible = false;
            }
        }

        #endregion

        #region رسائل المستخدم (MessageBox بالعربية RTL)

        /// <summary>عرض رسالة بالاتجاه من اليمين لليسار.</summary>
        private DialogResult Msg(string text, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return MessageBox.Show(this, text, title, buttons, icon, MessageBoxDefaultButton.Button1,
                MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
        }

        private void Info(string text) { Msg(text, "تم", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        private void Warn(string text) { Msg(text, "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        private void Err(string text, Exception ex) { Msg(text + ":\n" + ex.Message, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error); }

        /// <summary>رسالة تأكيد (نعم/لا) قبل التعديل أو الحذف.</summary>
        private bool Confirm(string text)
        {
            return Msg(text, "تأكيد العملية", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        #endregion

        #region التحقق من الإدخال

        /// <summary>
        /// تحويل نص إلى رقم عشري (يقبل الفاصلة أو النقطة). يرجع false عند الفشل أو القيمة السالبة.
        /// التحويل داخل try-catch لمنع أي استثناء.
        /// </summary>
        private bool ParseDecimal(string s, out decimal value)
        {
            value = 0;
            try
            {
                s = (s ?? "").Trim().Replace(',', '.');
                return decimal.TryParse(s, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value) && value >= 0;
            }
            catch { return false; }
        }

        /// <summary>
        /// حدث KeyPress لحقول الأسعار: يسمح بالأرقام وفاصلة عشرية واحدة (. أو ,) ومفتاح الحذف فقط.
        /// </summary>
        private void PriceKeyPress(object sender, KeyPressEventArgs e)
        {
            var tb = (TextBox)sender;
            char c = e.KeyChar;
            if (char.IsControl(c)) return;                 // Backspace وغيره
            if (c >= '0' && c <= '9') return;              // أرقام إنكليزية
            if ((c == '.' || c == ',') && tb.Text.IndexOfAny(new[] { '.', ',' }) < 0) return; // فاصلة واحدة
            e.Handled = true;                              // منع أي حرف آخر
        }

        /// <summary>حدث KeyPress لحقل الهاتف: أرقام وعلامة + فقط.</summary>
        private void PhoneKeyPress(object sender, KeyPressEventArgs e)
        {
            char c = e.KeyChar;
            if (char.IsControl(c) || (c >= '0' && c <= '9') || c == '+') return;
            e.Handled = true;
        }

        #endregion

        #region التصدير والنسخ الاحتياطي

        /// <summary>تصدير الأعمدة الظاهرة في الجدول إلى ملف CSV بترميز UTF-8 (يُفتح في Excel بالعربية).</summary>
        private void ExportGrid(DataGridView g, string defaultName)
        {
            using (var sfd = new SaveFileDialog { Filter = "ملف CSV (*.csv)|*.csv", FileName = defaultName })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var cols = new List<DataGridViewColumn>();
                    foreach (DataGridViewColumn c in g.Columns) if (c.Visible) cols.Add(c);
                    cols.Sort((a, b) => a.DisplayIndex.CompareTo(b.DisplayIndex));

                    var sb = new StringBuilder();
                    sb.AppendLine(string.Join(",", cols.ConvertAll(c => Csv(c.HeaderText))));
                    foreach (DataGridViewRow r in g.Rows)
                        sb.AppendLine(string.Join(",", cols.ConvertAll(c => Csv(Convert.ToString(r.Cells[c.Index].Value)))));

                    File.WriteAllText(sfd.FileName, sb.ToString(), new UTF8Encoding(true)); // UTF-8 مع BOM
                    Info("تم تصدير البيانات بنجاح إلى:\n" + sfd.FileName);
                }
                catch (Exception ex) { Err("فشل التصدير", ex); }
            }
        }

        /// <summary>تهريب قيمة نصية لملف CSV.</summary>
        private static string Csv(string s)
        {
            return "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
        }

        /// <summary>نسخة احتياطية: نسخ ملف قاعدة البيانات إلى مكان يختاره المستخدم.</summary>
        private void BackupDatabase()
        {
            using (var sfd = new SaveFileDialog { Filter = "قاعدة بيانات (*.db)|*.db", FileName = "supermarket_backup_" + DateTime.Now.ToString("yyyyMMdd") + ".db" })
            {
                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    File.Copy(Path.Combine(Application.StartupPath, "supermarket.db"), sfd.FileName, true);
                    Info("تم حفظ النسخة الاحتياطية بنجاح.");
                }
                catch (Exception ex) { Err("فشل النسخ الاحتياطي", ex); }
            }
        }

        #endregion
    }
}
