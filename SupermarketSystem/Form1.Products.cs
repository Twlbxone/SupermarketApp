using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace SupermarketSystem
{
    /// <summary>التبويب الأول: إدارة المنتجات والمخزون (إضافة / تعديل / حذف / بحث وتصفية).</summary>
    public partial class Form1
    {
        // حقول الإدخال
        private TextBox txtId, txtName, txtBuy, txtSell;
        private ComboBox cboCategory, cboLocation, cboShelf, cboProdSupplier;
        private NumericUpDown numQty, numMin;
        private DateTimePicker dtpExpiry;
        private RadioButton rdoActive, rdoSuspended;
        private Button btnAdd, btnUpdate, btnDelete, btnClear;

        // البحث والتصفية
        private TextBox txtSearch;
        private ComboBox cboFilterCat;
        private CheckBox chkExpiredOnly, chkLowOnly;
        private RadioButton rdoStAll, rdoStActive, rdoStSusp;

        private DataGridView dgvProducts;

        #region بناء الواجهة

        /// <summary>بناء تبويب المنتجات: مجموعة الإدخال، الأزرار، مجموعة التصفية، ثم الجدول.</summary>
        private void BuildProductsTab()
        {
            TabPage page = MkPage("1. إدارة المنتجات والمخزون");

            // ---------- GroupBox: بيانات المنتج ----------
            GroupBox grpInput = MkGroup("بيانات المنتج");
            TableLayoutPanel t = MkTable(4);

            txtId = new TextBox { ReadOnly = true, Text = "تلقائي", BackColor = Color.WhiteSmoke };
            txtName = new TextBox { MaxLength = 100 };
            cboCategory = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cboLocation = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cboShelf = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, MaxLength = 20 };
            for (int i = 1; i <= 30; i++) cboShelf.Items.Add("رف " + i); // أرقام الرفوف المقترحة
            cboProdSupplier = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };

            txtBuy = new TextBox { MaxLength = 12 };
            txtSell = new TextBox { MaxLength = 12 };
            txtBuy.KeyPress += PriceKeyPress;   // منع الحروف في حقول الأسعار
            txtSell.KeyPress += PriceKeyPress;

            numQty = new NumericUpDown { Minimum = 0, Maximum = 1000000 };
            numMin = new NumericUpDown { Minimum = 0, Maximum = 1000000 };
            dtpExpiry = new DateTimePicker { Format = DateTimePickerFormat.Short, RightToLeftLayout = true, Value = DateTime.Today.AddMonths(6) };

            rdoActive = new RadioButton { Text = "نشط", Checked = true, AutoSize = true };
            rdoSuspended = new RadioButton { Text = "معلق", AutoSize = true };
            var pnlStatus = new FlowLayoutPanel { AutoSize = true, RightToLeft = RightToLeft.Yes };
            pnlStatus.Controls.Add(rdoActive);
            pnlStatus.Controls.Add(rdoSuspended);

            Put(t, "رقم المنتج:", txtId);
            Put(t, "اسم المنتج:", txtName);
            Put(t, "القسم:", cboCategory);
            Put(t, "مكان التخزين:", cboLocation);
            Put(t, "رقم الرف:", cboShelf);
            Put(t, "المورد (الشركة):", cboProdSupplier);
            Put(t, "سعر الشراء:", txtBuy);
            Put(t, "سعر المبيع المقترح:", txtSell);
            Put(t, "الكمية الحالية:", numQty);
            Put(t, "الحد الأدنى للطلب:", numMin);
            Put(t, "تاريخ انتهاء الصلاحية:", dtpExpiry);
            Put(t, "حالة الصنف:", pnlStatus);
            grpInput.Controls.Add(t);

            // ---------- Panel: أزرار العمليات (معزولة عن حقول الإدخال) ----------
            FlowLayoutPanel pnlButtons = MkButtons();
            btnAdd = MkBtn("➕ إضافة", ClrSuccess, BtnAdd_Click);
            btnUpdate = MkBtn("✏ تعديل", ClrWarning, BtnUpdate_Click);
            btnDelete = MkBtn("🗑 حذف", ClrDanger, BtnDelete_Click);
            btnClear = MkBtn("مسح الحقول", ClrGray, (s, e) => ClearProductForm());
            pnlButtons.Controls.AddRange(new Control[] { btnAdd, btnUpdate, btnDelete, btnClear });

            // ---------- GroupBox: بحث وتصفية ----------
            GroupBox grpFilter = MkGroup("بحث وتصفية المخزون");
            TableLayoutPanel f = MkTable(4);
            txtSearch = new TextBox();
            txtSearch.TextChanged += FilterChanged;
            cboFilterCat = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cboFilterCat.SelectedIndexChanged += FilterChanged;

            chkExpiredOnly = new CheckBox { Text = "عرض المواد المنتهية فقط", AutoSize = true };
            chkLowOnly = new CheckBox { Text = "عرض النواقص فقط", AutoSize = true };
            chkExpiredOnly.CheckedChanged += FilterChanged;
            chkLowOnly.CheckedChanged += FilterChanged;
            var pnlChk = new FlowLayoutPanel { AutoSize = true, RightToLeft = RightToLeft.Yes };
            pnlChk.Controls.Add(chkExpiredOnly);
            pnlChk.Controls.Add(chkLowOnly);

            rdoStAll = new RadioButton { Text = "الكل", Checked = true, AutoSize = true };
            rdoStActive = new RadioButton { Text = "نشط", AutoSize = true };
            rdoStSusp = new RadioButton { Text = "معلق", AutoSize = true };
            rdoStAll.CheckedChanged += FilterChanged;
            rdoStActive.CheckedChanged += FilterChanged;
            rdoStSusp.CheckedChanged += FilterChanged;
            var pnlRdo = new FlowLayoutPanel { AutoSize = true, RightToLeft = RightToLeft.Yes };
            pnlRdo.Controls.AddRange(new Control[] { rdoStAll, rdoStActive, rdoStSusp });

            Put(f, "بحث بالاسم:", txtSearch);
            Put(f, "تصفية حسب القسم:", cboFilterCat);
            Put(f, "خيارات:", pnlChk);
            Put(f, "الحالة:", pnlRdo);
            grpFilter.Controls.Add(f);

            // ---------- مفتاح الألوان ----------
            var lblLegend = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 26,
                Text = "دليل الألوان:  أحمر = منتهي الصلاحية   |   برتقالي = قريب الانتهاء   |   أصفر = وصل للحد الأدنى للطلب",
                ForeColor = ClrGray,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // ---------- DataGridView ----------
            dgvProducts = new DataGridView();
            StyleGrid(dgvProducts);
            dgvProducts.CellClick += DgvProducts_CellClick;
            dgvProducts.DataBindingComplete += (s, e) => ColorProductRows();

            // ترتيب الإضافة: الملء أولاً ثم العناصر المثبتة من الأسفل ثم من الأعلى
            page.Controls.Add(dgvProducts);
            page.Controls.Add(lblLegend);
            page.Controls.Add(grpFilter);
            page.Controls.Add(pnlButtons);
            page.Controls.Add(grpInput);
            tabMain.TabPages.Add(page);
        }

        #endregion

        #region تحميل وتلوين الجدول

        /// <summary>
        /// قراءة المنتجات مع تطبيق البحث والتصفية الحالية (الاسم، القسم، الحالة، المنتهية، النواقص).
        /// </summary>
        private void LoadProducts()
        {
            if (dgvProducts == null) return;

            string sql = @"SELECT p.ProductID, p.ProductName, c.CategoryName, l.LocationName, p.ShelfNumber,
                                  p.PurchasePrice, p.SalePrice, p.Quantity, p.MinQuantity, p.ExpiryDate, p.Status,
                                  IFNULL(co.CompanyName,'') AS CompanyName,
                                  p.CategoryID, p.LocationID, IFNULL(p.CompanyID,0) AS CompanyID
                           FROM Products p
                           JOIN Categories c ON c.CategoryID = p.CategoryID
                           JOIN StorageLocations l ON l.LocationID = p.LocationID
                           LEFT JOIN Companies co ON co.CompanyID = p.CompanyID
                           WHERE 1=1";
            var prms = new List<SQLiteParameter>();

            if (txtSearch.Text.Trim().Length > 0)
            {
                sql += " AND p.ProductName LIKE @s";
                prms.Add(DatabaseHelper.P("@s", "%" + txtSearch.Text.Trim() + "%"));
            }
            int cat = ComboId(cboFilterCat);
            if (cat > 0)
            {
                sql += " AND p.CategoryID = @c";
                prms.Add(DatabaseHelper.P("@c", cat));
            }
            if (rdoStActive.Checked) sql += " AND p.Status = 'نشط'";
            if (rdoStSusp.Checked) sql += " AND p.Status = 'معلق'";
            if (chkExpiredOnly.Checked) sql += " AND p.Quantity > 0 AND p.ExpiryDate < date('now','localtime')";
            if (chkLowOnly.Checked) sql += " AND p.Status = 'نشط' AND p.Quantity <= p.MinQuantity";
            sql += " ORDER BY p.ProductName";

            dgvProducts.DataSource = DatabaseHelper.GetTable(sql, prms.ToArray());
            SetHeaders(dgvProducts,
                "ProductID", "الرقم", "ProductName", "اسم المنتج", "CategoryName", "القسم",
                "LocationName", "مكان التخزين", "ShelfNumber", "الرف", "PurchasePrice", "سعر الشراء",
                "SalePrice", "سعر المبيع", "Quantity", "الكمية", "MinQuantity", "الحد الأدنى",
                "ExpiryDate", "تاريخ الصلاحية", "Status", "الحالة", "CompanyName", "المورد");
            dgvProducts.ClearSelection();
        }

        /// <summary>أي تغيير في البحث أو التصفية يعيد تحميل الجدول فوراً.</summary>
        private void FilterChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            LoadProducts();
        }

        /// <summary>
        /// تلوين الصفوف: أحمر للمنتهي، برتقالي للقريب من الانتهاء (خلال عدد الأيام المحدد في تبويب التنبيهات)،
        /// أصفر لما وصل للحد الأدنى للطلب.
        /// </summary>
        private void ColorProductRows()
        {
            int alertDays = (int)numAlertDays.Value;
            foreach (DataGridViewRow r in dgvProducts.Rows)
            {
                try
                {
                    DateTime exp;
                    if (!DateTime.TryParseExact(Convert.ToString(r.Cells["ExpiryDate"].Value), "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out exp)) continue;

                    int qty = Convert.ToInt32(r.Cells["Quantity"].Value);
                    int min = Convert.ToInt32(r.Cells["MinQuantity"].Value);
                    bool active = Convert.ToString(r.Cells["Status"].Value) == "نشط";
                    int days = (exp.Date - DateTime.Today).Days;

                    if (qty > 0 && days < 0) r.DefaultCellStyle.BackColor = RowExpired;
                    else if (qty > 0 && days <= alertDays) r.DefaultCellStyle.BackColor = RowNear;
                    else if (active && qty <= min) r.DefaultCellStyle.BackColor = RowLow;
                }
                catch { /* تجاهل أي صف غير صالح */ }
            }
        }

        #endregion

        #region اختيار صف ومسح الحقول

        /// <summary>عند النقر على صف: تعبئة حقول الإدخال ببياناته للتعديل أو الحذف.</summary>
        private void DgvProducts_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            try
            {
                DataGridViewRow r = dgvProducts.Rows[e.RowIndex];
                txtId.Text = Convert.ToString(r.Cells["ProductID"].Value);
                txtName.Text = Convert.ToString(r.Cells["ProductName"].Value);
                cboCategory.SelectedValue = Convert.ToInt64(r.Cells["CategoryID"].Value);
                cboLocation.SelectedValue = Convert.ToInt64(r.Cells["LocationID"].Value);
                cboProdSupplier.SelectedValue = Convert.ToInt64(r.Cells["CompanyID"].Value);
                cboShelf.Text = Convert.ToString(r.Cells["ShelfNumber"].Value);
                txtBuy.Text = Convert.ToDecimal(r.Cells["PurchasePrice"].Value).ToString(CultureInfo.InvariantCulture);
                txtSell.Text = Convert.ToDecimal(r.Cells["SalePrice"].Value).ToString(CultureInfo.InvariantCulture);
                numQty.Value = Convert.ToInt32(r.Cells["Quantity"].Value);
                numMin.Value = Convert.ToInt32(r.Cells["MinQuantity"].Value);
                dtpExpiry.Value = DateTime.ParseExact(Convert.ToString(r.Cells["ExpiryDate"].Value), "yyyy-MM-dd", CultureInfo.InvariantCulture);
                bool active = Convert.ToString(r.Cells["Status"].Value) == "نشط";
                rdoActive.Checked = active;
                rdoSuspended.Checked = !active;
            }
            catch (Exception ex) { Err("تعذّر عرض بيانات المنتج المحدد", ex); }
        }

        /// <summary>إعادة حقول الإدخال إلى وضعها الافتراضي (زر الإلغاء/المسح).</summary>
        private void ClearProductForm()
        {
            txtId.Text = "تلقائي";
            txtName.Clear();
            txtBuy.Clear();
            txtSell.Clear();
            cboShelf.Text = "";
            if (cboCategory.Items.Count > 0) cboCategory.SelectedIndex = 0;
            if (cboLocation.Items.Count > 0) cboLocation.SelectedIndex = 0;
            if (cboProdSupplier.Items.Count > 0) cboProdSupplier.SelectedIndex = 0;
            numQty.Value = 0;
            numMin.Value = 0;
            dtpExpiry.Value = DateTime.Today.AddMonths(6);
            rdoActive.Checked = true;
            dgvProducts.ClearSelection();
            txtName.Focus();
        }

        #endregion

        #region التحقق (if / else) والعمليات

        /// <summary>
        /// التحقق من اكتمال الحقول وصحة الأرقام قبل الإضافة أو التعديل.
        /// يعرض رسالة عربية ويضع المؤشر على أول حقل خاطئ. يرجع false عند وجود خطأ.
        /// </summary>
        private bool ValidateProductInput(out decimal buy, out decimal sell)
        {
            buy = 0; sell = 0;

            if (txtName.Text.Trim().Length == 0) { Warn("الرجاء إدخال اسم المنتج."); txtName.Focus(); return false; }
            else if (ComboId(cboCategory) == 0) { Warn("الرجاء اختيار القسم."); cboCategory.Focus(); return false; }
            else if (ComboId(cboLocation) == 0) { Warn("الرجاء اختيار مكان التخزين."); cboLocation.Focus(); return false; }
            else if (cboShelf.Text.Trim().Length == 0) { Warn("الرجاء إدخال رقم الرف."); cboShelf.Focus(); return false; }
            else if (txtBuy.Text.Trim().Length == 0) { Warn("الرجاء إدخال سعر الشراء."); txtBuy.Focus(); return false; }
            else if (!ParseDecimal(txtBuy.Text, out buy)) { Warn("سعر الشراء غير صالح."); txtBuy.Focus(); return false; }
            else if (txtSell.Text.Trim().Length == 0) { Warn("الرجاء إدخال سعر المبيع."); txtSell.Focus(); return false; }
            else if (!ParseDecimal(txtSell.Text, out sell)) { Warn("سعر المبيع غير صالح."); txtSell.Focus(); return false; }
            else if (sell < buy && !Confirm("سعر المبيع أقل من سعر الشراء (خسارة). هل تريد المتابعة؟")) { txtSell.Focus(); return false; }

            return true;
        }

        /// <summary>تجهيز معاملات الإدخال/التعديل المشتركة.</summary>
        private List<SQLiteParameter> ProductParams(decimal buy, decimal sell)
        {
            int comp = ComboId(cboProdSupplier);
            return new List<SQLiteParameter>
            {
                DatabaseHelper.P("@n", txtName.Text.Trim()),
                DatabaseHelper.P("@c", ComboId(cboCategory)),
                DatabaseHelper.P("@l", ComboId(cboLocation)),
                DatabaseHelper.P("@sh", cboShelf.Text.Trim()),
                DatabaseHelper.P("@b", (double)buy),
                DatabaseHelper.P("@sp", (double)sell),
                DatabaseHelper.P("@q", (int)numQty.Value),
                DatabaseHelper.P("@m", (int)numMin.Value),
                DatabaseHelper.P("@e", DatabaseHelper.D(dtpExpiry.Value)),
                DatabaseHelper.P("@st", rdoActive.Checked ? "نشط" : "معلق"),
                DatabaseHelper.P("@co", comp == 0 ? (object)null : comp)
            };
        }

        /// <summary>إضافة منتج جديد (الرقم يُولَّد تلقائياً من قاعدة البيانات).</summary>
        private void BtnAdd_Click(object sender, EventArgs e)
        {
            decimal buy, sell;
            if (!ValidateProductInput(out buy, out sell)) return;
            try
            {
                long id = DatabaseHelper.Insert(
                    @"INSERT INTO Products(ProductName,CategoryID,LocationID,ShelfNumber,PurchasePrice,SalePrice,
                                           Quantity,MinQuantity,ExpiryDate,Status,CompanyID)
                      VALUES(@n,@c,@l,@sh,@b,@sp,@q,@m,@e,@st,@co)",
                    ProductParams(buy, sell).ToArray());
                if (id > 0)
                {
                    Info("تمت إضافة المنتج بنجاح. رقمه: " + id);
                    ClearProductForm();
                    RefreshAll();
                }
            }
            catch (Exception ex) { Err("فشلت إضافة المنتج", ex); }
        }

        /// <summary>تعديل المنتج المحدد بعد رسالة تأكيد.</summary>
        private void BtnUpdate_Click(object sender, EventArgs e)
        {
            int id;
            if (!int.TryParse(txtId.Text, out id)) { Warn("اختر منتجاً من الجدول أولاً لتعديله."); return; }
            decimal buy, sell;
            if (!ValidateProductInput(out buy, out sell)) return;
            if (!Confirm("هل تريد حفظ التعديلات على المنتج رقم " + id + "؟")) return;
            try
            {
                List<SQLiteParameter> p = ProductParams(buy, sell);
                p.Add(DatabaseHelper.P("@id", id));
                int rows = DatabaseHelper.Execute(
                    @"UPDATE Products SET ProductName=@n, CategoryID=@c, LocationID=@l, ShelfNumber=@sh,
                                          PurchasePrice=@b, SalePrice=@sp, Quantity=@q, MinQuantity=@m,
                                          ExpiryDate=@e, Status=@st, CompanyID=@co
                      WHERE ProductID=@id", p.ToArray());
                if (rows > 0) { Info("تم تعديل بيانات المنتج."); ClearProductForm(); RefreshAll(); }
            }
            catch (Exception ex) { Err("فشل تعديل المنتج", ex); }
        }

        /// <summary>حذف المنتج المحدد بعد رسالة تأكيد (تُحذف حركاته المرتبطة تلقائياً).</summary>
        private void BtnDelete_Click(object sender, EventArgs e)
        {
            int id;
            if (!int.TryParse(txtId.Text, out id)) { Warn("اختر منتجاً من الجدول أولاً لحذفه."); return; }
            if (!Confirm("هل أنت متأكد من حذف المنتج «" + txtName.Text + "» نهائياً؟\nسيتم حذف سجل حركاته (وارد/تالف/جرد) أيضاً.")) return;
            try
            {
                int rows = DatabaseHelper.Execute("DELETE FROM Products WHERE ProductID=@id", DatabaseHelper.P("@id", id));
                if (rows > 0) { Info("تم حذف المنتج."); ClearProductForm(); RefreshAll(); }
            }
            catch (Exception ex) { Err("فشل حذف المنتج", ex); }
        }

        #endregion
    }
}
