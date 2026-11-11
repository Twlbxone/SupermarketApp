using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace SupermarketSystem
{
    /// <summary>التبويب الرابع: المندوبون والشركات، وقائمة النواقص التلقائية لطلبات التوريد.</summary>
    public partial class Form1
    {
        private TextBox txtRepName, txtRepPhone, txtRepType;
        private ComboBox cboRepCompany;
        private Button btnRepAdd, btnRepUpdate, btnRepDelete, btnRepClear, btnExportShort;
        private DataGridView dgvReps, dgvShortages;
        private int _selRepId = 0;   // رقم المندوب المحدد في الجدول (0 = لا يوجد)

        #region بناء الواجهة

        private void BuildRepsTab()
        {
            TabPage page = MkPage("4. المندوبون وطلبات التوريد");

            // ---------- بيانات المندوب ----------
            GroupBox grp = MkGroup("بيانات المندوب والشركة");
            TableLayoutPanel t = MkTable(4);
            txtRepName = new TextBox { MaxLength = 80 };
            txtRepPhone = new TextBox { MaxLength = 20 };
            txtRepPhone.KeyPress += PhoneKeyPress;                    // أرقام فقط
            cboRepCompany = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };   // قابلة للكتابة: شركة جديدة أو موجودة
            txtRepType = new TextBox { MaxLength = 100 };
            Put(t, "اسم المندوب:", txtRepName);
            Put(t, "رقم الهاتف:", txtRepPhone);
            Put(t, "اسم الشركة:", cboRepCompany);
            Put(t, "نوع منتجات الشركة:", txtRepType);
            grp.Controls.Add(t);

            FlowLayoutPanel btns = MkButtons();
            btnRepAdd = MkBtn("➕ إضافة مندوب", ClrSuccess, BtnRepAdd_Click);
            btnRepUpdate = MkBtn("✏ تعديل", ClrWarning, BtnRepUpdate_Click);
            btnRepDelete = MkBtn("🗑 حذف", ClrDanger, BtnRepDelete_Click);
            btnRepClear = MkBtn("مسح الحقول", ClrGray, (s, e) => ClearRepForm());
            btns.Controls.AddRange(new Control[] { btnRepAdd, btnRepUpdate, btnRepDelete, btnRepClear });

            // ---------- جدول المندوبين ----------
            dgvReps = new DataGridView();
            StyleGrid(dgvReps);
            dgvReps.CellClick += DgvReps_CellClick;

            // ---------- قائمة النواقص التلقائية ----------
            dgvShortages = new DataGridView();
            StyleGrid(dgvShortages);
            btnExportShort = MkBtn("📤 تصدير قائمة النواقص", ClrAccent, (s, e) => ExportGrid(dgvShortages, "قائمة_النواقص.csv"));
            var shortTop = MkButtons();
            shortTop.Controls.Add(btnExportShort);
            shortTop.Controls.Add(new Label { Text = "تُولَّد تلقائياً: أي مادة نشطة وصلت للحد الأدنى للطلب تظهر هنا دون تدخل يدوي.", AutoSize = true, ForeColor = ClrGray, Margin = new Padding(8, 12, 0, 0) });

            var grpShort = new GroupBox { Text = "قائمة النواقص التلقائية (طلب التوريد)", Dock = DockStyle.Bottom, Height = 270, ForeColor = ClrDanger, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            grpShort.Controls.Add(dgvShortages);
            grpShort.Controls.Add(shortTop);

            page.Controls.Add(dgvReps);
            page.Controls.Add(grpShort);
            page.Controls.Add(btns);
            page.Controls.Add(grp);
            tabMain.TabPages.Add(page);
        }

        #endregion

        #region تحميل البيانات

        /// <summary>تحميل جدول المندوبين.</summary>
        private void LoadReps()
        {
            if (dgvReps == null) return;
            dgvReps.DataSource = DatabaseHelper.GetTable(
                @"SELECT r.RepID, r.RepName, r.Phone, c.CompanyName, IFNULL(c.ProductsType,'') AS ProductsType
                  FROM Representatives r JOIN Companies c ON c.CompanyID = r.CompanyID
                  ORDER BY r.RepName");
            SetHeaders(dgvReps, "RepID", "الرقم", "RepName", "اسم المندوب", "Phone", "الهاتف",
                       "CompanyName", "الشركة", "ProductsType", "نوع المنتجات");
            dgvReps.ClearSelection();
        }

        /// <summary>تحميل قائمة النواقص من العرض v_Shortages (تتحدث تلقائياً مع كل حركة).</summary>
        private void LoadShortages()
        {
            if (dgvShortages == null) return;
            dgvShortages.DataSource = DatabaseHelper.GetShortages();
            SetHeaders(dgvShortages, "ProductName", "المنتج", "CategoryName", "القسم", "Quantity", "الكمية الحالية",
                       "MinQuantity", "الحد الأدنى", "MissingQty", "الكمية الناقصة", "CompanyName", "الشركة الموردة",
                       "RepName", "المندوب", "Phone", "الهاتف");
        }

        #endregion

        #region العمليات

        private void DgvReps_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            DataGridViewRow r = dgvReps.Rows[e.RowIndex];
            _selRepId = Convert.ToInt32(r.Cells["RepID"].Value);
            txtRepName.Text = Convert.ToString(r.Cells["RepName"].Value);
            txtRepPhone.Text = Convert.ToString(r.Cells["Phone"].Value);
            cboRepCompany.Text = Convert.ToString(r.Cells["CompanyName"].Value);
            txtRepType.Text = Convert.ToString(r.Cells["ProductsType"].Value);
        }

        private void ClearRepForm()
        {
            _selRepId = 0;
            txtRepName.Clear(); txtRepPhone.Clear(); txtRepType.Clear();
            cboRepCompany.SelectedIndex = -1; cboRepCompany.Text = "";
            dgvReps.ClearSelection();
            txtRepName.Focus();
        }

        /// <summary>التحقق من حقول المندوب (if / else) قبل الإضافة أو التعديل.</summary>
        private bool ValidateRepInput()
        {
            if (txtRepName.Text.Trim().Length == 0) { Warn("الرجاء إدخال اسم المندوب."); txtRepName.Focus(); return false; }
            else if (txtRepPhone.Text.Trim().Length < 6) { Warn("الرجاء إدخال رقم هاتف صحيح (6 أرقام على الأقل)."); txtRepPhone.Focus(); return false; }
            else if (cboRepCompany.Text.Trim().Length == 0) { Warn("الرجاء إدخال اسم الشركة."); cboRepCompany.Focus(); return false; }
            else if (txtRepType.Text.Trim().Length == 0) { Warn("الرجاء إدخال نوع منتجات الشركة."); txtRepType.Focus(); return false; }
            return true;
        }

        /// <summary>
        /// إيجاد رقم الشركة بالاسم، وإنشاؤها إن لم تكن موجودة، مع تحديث نوع منتجاتها.
        /// </summary>
        private int GetOrCreateCompany(string name, string type)
        {
            object id = DatabaseHelper.Scalar("SELECT CompanyID FROM Companies WHERE CompanyName=@n", DatabaseHelper.P("@n", name));
            if (id != null)
            {
                DatabaseHelper.Execute("UPDATE Companies SET ProductsType=@t WHERE CompanyID=@i",
                    DatabaseHelper.P("@t", type), DatabaseHelper.P("@i", Convert.ToInt32(id)));
                return Convert.ToInt32(id);
            }
            return (int)DatabaseHelper.Insert("INSERT INTO Companies(CompanyName, ProductsType) VALUES(@n,@t)",
                DatabaseHelper.P("@n", name), DatabaseHelper.P("@t", type));
        }

        private void BtnRepAdd_Click(object sender, EventArgs e)
        {
            if (!ValidateRepInput()) return;
            try
            {
                int comp = GetOrCreateCompany(cboRepCompany.Text.Trim(), txtRepType.Text.Trim());
                if (comp <= 0) return;
                long id = DatabaseHelper.Insert("INSERT INTO Representatives(RepName,Phone,CompanyID) VALUES(@n,@p,@c)",
                    DatabaseHelper.P("@n", txtRepName.Text.Trim()), DatabaseHelper.P("@p", txtRepPhone.Text.Trim()), DatabaseHelper.P("@c", comp));
                if (id > 0) { Info("تمت إضافة المندوب."); ClearRepForm(); RefreshAll(); }
            }
            catch (Exception ex) { Err("فشلت إضافة المندوب", ex); }
        }

        private void BtnRepUpdate_Click(object sender, EventArgs e)
        {
            if (_selRepId == 0) { Warn("اختر مندوباً من الجدول أولاً."); return; }
            if (!ValidateRepInput()) return;
            if (!Confirm("هل تريد حفظ تعديلات المندوب؟")) return;
            try
            {
                int comp = GetOrCreateCompany(cboRepCompany.Text.Trim(), txtRepType.Text.Trim());
                int rows = DatabaseHelper.Execute("UPDATE Representatives SET RepName=@n, Phone=@p, CompanyID=@c WHERE RepID=@i",
                    DatabaseHelper.P("@n", txtRepName.Text.Trim()), DatabaseHelper.P("@p", txtRepPhone.Text.Trim()),
                    DatabaseHelper.P("@c", comp), DatabaseHelper.P("@i", _selRepId));
                if (rows > 0) { Info("تم تعديل بيانات المندوب."); ClearRepForm(); RefreshAll(); }
            }
            catch (Exception ex) { Err("فشل تعديل المندوب", ex); }
        }

        private void BtnRepDelete_Click(object sender, EventArgs e)
        {
            if (_selRepId == 0) { Warn("اختر مندوباً من الجدول أولاً."); return; }
            if (!Confirm("هل أنت متأكد من حذف المندوب «" + txtRepName.Text + "»؟")) return;
            try
            {
                int rows = DatabaseHelper.Execute("DELETE FROM Representatives WHERE RepID=@i", DatabaseHelper.P("@i", _selRepId));
                if (rows > 0) { Info("تم حذف المندوب."); ClearRepForm(); RefreshAll(); }
            }
            catch (Exception ex) { Err("فشل حذف المندوب", ex); }
        }

        #endregion
    }
}
