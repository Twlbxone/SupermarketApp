using System;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace SupermarketSystem
{
    /// <summary>
    /// التبويب الثالث: حركات المستودع، وفيه ثلاث صفحات داخلية:
    /// (1) بضاعة واردة  (2) تسجيل التالف  (3) الجرد الدوري.
    /// </summary>
    public partial class Form1
    {
        private TabControl tabMov;

        // بضاعة واردة
        private ComboBox cboInProduct, cboInSupplier;
        private NumericUpDown numInQty;
        private DateTimePicker dtpSupply, dtpInExpiry;
        private TextBox txtInNotes;
        private Label lblInCurrent;
        private Button btnStockIn, btnInClear;
        private DataGridView dgvInLog;

        // التالف
        private ComboBox cboDmgProduct, cboReason;
        private NumericUpDown numDmgQty;
        private DateTimePicker dtpDmg;
        private TextBox txtDmgNotes;
        private Label lblDmgCurrent;
        private Button btnDamage, btnDmgClear;
        private DataGridView dgvDmgLog;

        // الجرد
        private DateTimePicker dtpAudit;
        private Button btnAuditStart, btnAuditApply;
        private ProgressBar pbAudit;
        private Label lblAuditSummary;
        private DataGridView dgvAudit, dgvAuditLog;

        /// <summary>بناء التبويب الثالث وصفحاته الداخلية.</summary>
        private void BuildMovementsTab()
        {
            TabPage page = MkPage("3. حركات المستودع والتالف والجرد");
            tabMov = new TabControl { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes, RightToLeftLayout = true, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            tabMov.TabPages.Add(BuildStockInPage());
            tabMov.TabPages.Add(BuildDamagePage());
            tabMov.TabPages.Add(BuildAuditPage());
            tabMov.SelectedIndexChanged += (s, e) => SetDefaultButtons();
            page.Controls.Add(tabMov);
            tabMain.TabPages.Add(page);
        }

        #region (1) بضاعة واردة

        private TabPage BuildStockInPage()
        {
            TabPage pg = MkPage("إدخال بضاعة واردة");

            GroupBox grp = MkGroup("بيانات الوارد");
            TableLayoutPanel t = MkTable(4);
            cboInProduct = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cboInProduct.SelectedIndexChanged += (s, e) => ShowCurrentQty(cboInProduct, lblInCurrent);
            lblInCurrent = new Label { AutoSize = true, ForeColor = ClrAccent, Text = "الكمية الحالية: -" };
            numInQty = new NumericUpDown { Minimum = 1, Maximum = 1000000, Value = 1 };
            dtpSupply = new DateTimePicker { Format = DateTimePickerFormat.Short, RightToLeftLayout = true };
            dtpInExpiry = new DateTimePicker { Format = DateTimePickerFormat.Short, RightToLeftLayout = true, Value = DateTime.Today.AddMonths(6) };
            cboInSupplier = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            txtInNotes = new TextBox { MaxLength = 200 };

            Put(t, "المنتج:", cboInProduct);
            Put(t, "الرصيد:", lblInCurrent);
            Put(t, "الكمية الواردة:", numInQty);
            Put(t, "المورد:", cboInSupplier);
            Put(t, "تاريخ التوريد:", dtpSupply);
            Put(t, "تاريخ الصلاحية الجديد:", dtpInExpiry);
            Put(t, "ملاحظات:", txtInNotes);
            grp.Controls.Add(t);

            FlowLayoutPanel btns = MkButtons();
            btnStockIn = MkBtn("📥 تسجيل الوارد", ClrSuccess, BtnStockIn_Click);
            btnInClear = MkBtn("مسح الحقول", ClrGray, (s, e) => { numInQty.Value = 1; txtInNotes.Clear(); dtpSupply.Value = DateTime.Today; });
            btns.Controls.AddRange(new Control[] { btnStockIn, btnInClear });

            dgvInLog = new DataGridView();
            StyleGrid(dgvInLog);

            pg.Controls.Add(dgvInLog);
            pg.Controls.Add(btns);
            pg.Controls.Add(grp);
            return pg;
        }

        /// <summary>تسجيل بضاعة واردة: زيادة الرصيد وتحديث تاريخ الصلاحية داخل معاملة واحدة.</summary>
        private void BtnStockIn_Click(object sender, EventArgs e)
        {
            int pid = ComboId(cboInProduct);
            if (pid == 0) { Warn("الرجاء اختيار المنتج."); cboInProduct.Focus(); return; }
            else if (numInQty.Value < 1) { Warn("الكمية الواردة يجب أن تكون أكبر من صفر."); numInQty.Focus(); return; }
            else if (dtpInExpiry.Value.Date <= dtpSupply.Value.Date) { Warn("تاريخ الصلاحية يجب أن يكون بعد تاريخ التوريد."); dtpInExpiry.Focus(); return; }

            try
            {
                int comp = ComboId(cboInSupplier);
                bool ok = DatabaseHelper.RegisterStockIn(pid, (int)numInQty.Value, dtpSupply.Value, dtpInExpiry.Value,
                                                         comp == 0 ? (int?)null : comp, txtInNotes.Text.Trim());
                if (ok)
                {
                    Info("تم تسجيل البضاعة الواردة وزيادة الرصيد.");
                    numInQty.Value = 1; txtInNotes.Clear();
                    RefreshAll();
                }
            }
            catch (Exception ex) { Err("فشل تسجيل الوارد", ex); }
        }

        #endregion

        #region (2) التالف

        private TabPage BuildDamagePage()
        {
            TabPage pg = MkPage("تسجيل التالف");

            GroupBox grp = MkGroup("بيانات التالف");
            TableLayoutPanel t = MkTable(4);
            cboDmgProduct = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cboDmgProduct.SelectedIndexChanged += (s, e) => ShowCurrentQty(cboDmgProduct, lblDmgCurrent);
            lblDmgCurrent = new Label { AutoSize = true, ForeColor = ClrAccent, Text = "الكمية الحالية: -" };
            numDmgQty = new NumericUpDown { Minimum = 1, Maximum = 1000000, Value = 1 };
            cboReason = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            dtpDmg = new DateTimePicker { Format = DateTimePickerFormat.Short, RightToLeftLayout = true };
            txtDmgNotes = new TextBox { MaxLength = 200 };

            Put(t, "المنتج:", cboDmgProduct);
            Put(t, "الرصيد:", lblDmgCurrent);
            Put(t, "كمية التالف:", numDmgQty);
            Put(t, "سبب الإتلاف:", cboReason);
            Put(t, "تاريخ الإتلاف:", dtpDmg);
            Put(t, "ملاحظات:", txtDmgNotes);
            grp.Controls.Add(t);

            FlowLayoutPanel btns = MkButtons();
            btnDamage = MkBtn("⚠ تسجيل التالف", ClrDanger, BtnDamage_Click);
            btnDmgClear = MkBtn("مسح الحقول", ClrGray, (s, e) => { numDmgQty.Value = 1; txtDmgNotes.Clear(); dtpDmg.Value = DateTime.Today; });
            btns.Controls.AddRange(new Control[] { btnDamage, btnDmgClear });

            dgvDmgLog = new DataGridView();
            StyleGrid(dgvDmgLog);

            pg.Controls.Add(dgvDmgLog);
            pg.Controls.Add(btns);
            pg.Controls.Add(grp);
            return pg;
        }

        /// <summary>تسجيل تالف: إنقاص الكمية مع حفظ السبب. يُمنع إتلاف كمية أكبر من المتوفرة.</summary>
        private void BtnDamage_Click(object sender, EventArgs e)
        {
            int pid = ComboId(cboDmgProduct);
            if (pid == 0) { Warn("الرجاء اختيار المنتج."); cboDmgProduct.Focus(); return; }
            else if (ComboId(cboReason) == 0) { Warn("الرجاء اختيار سبب الإتلاف."); cboReason.Focus(); return; }
            else if (numDmgQty.Value < 1) { Warn("كمية التالف يجب أن تكون أكبر من صفر."); numDmgQty.Focus(); return; }

            try
            {
                object cur = DatabaseHelper.Scalar("SELECT Quantity FROM Products WHERE ProductID=@p", DatabaseHelper.P("@p", pid));
                if (cur != null && numDmgQty.Value > Convert.ToInt32(cur))
                {
                    Warn("كمية التالف أكبر من الكمية المتوفرة (" + cur + ").");
                    numDmgQty.Focus();
                    return;
                }
                if (!Confirm("سيتم إنقاص " + numDmgQty.Value + " من رصيد المنتج. هل تريد المتابعة؟")) return;

                bool ok = DatabaseHelper.RegisterDamage(pid, (int)numDmgQty.Value, ComboId(cboReason), dtpDmg.Value, txtDmgNotes.Text.Trim());
                if (ok)
                {
                    Info("تم تسجيل التالف وإنقاص الرصيد.");
                    numDmgQty.Value = 1; txtDmgNotes.Clear();
                    RefreshAll();
                }
            }
            catch (Exception ex) { Err("فشل تسجيل التالف", ex); }
        }

        #endregion

        #region (3) الجرد الدوري

        private TabPage BuildAuditPage()
        {
            TabPage pg = MkPage("الجرد الدوري");

            // شريط الأزرار والتقدم
            FlowLayoutPanel top = MkButtons();
            dtpAudit = new DateTimePicker { Format = DateTimePickerFormat.Short, RightToLeftLayout = true, Width = 130, Margin = new Padding(4, 8, 4, 0) };
            btnAuditStart = MkBtn("📋 بدء جرد جديد", ClrAccent, (s, e) => StartAudit());
            btnAuditApply = MkBtn("✔ اعتماد الجرد وتحديث الرصيد", ClrSuccess, (s, e) => ApplyAuditAll());
            pbAudit = new ProgressBar { Width = 220, Height = 22, Margin = new Padding(8, 10, 4, 0), RightToLeftLayout = true };
            lblAuditSummary = new Label { AutoSize = true, ForeColor = ClrPrimary, Margin = new Padding(8, 10, 4, 0), Text = "اضغط «بدء جرد جديد» ثم عدّل عمود الكمية الفعلية." };
            top.Controls.AddRange(new Control[] { new Label { Text = "تاريخ الجرد:", AutoSize = true, Margin = new Padding(4, 12, 0, 0) }, dtpAudit, btnAuditStart, btnAuditApply, pbAudit, lblAuditSummary });

            // جدول الجرد (الكمية الفعلية فقط قابلة للتعديل)
            dgvAudit = new DataGridView();
            StyleGrid(dgvAudit, false);
            dgvAudit.CellValidating += DgvAudit_CellValidating;
            dgvAudit.CellEndEdit += (s, e) => { ColorAuditRows(); UpdateAuditSummary(); };
            dgvAudit.DataError += (s, e) => { e.ThrowException = false; };

            // سجل عمليات الجرد المعتمدة
            dgvAuditLog = new DataGridView();
            StyleGrid(dgvAuditLog);
            var grpLog = new GroupBox { Text = "سجل عمليات الجرد المعتمدة", Dock = DockStyle.Bottom, Height = 190, ForeColor = ClrPrimary, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            grpLog.Controls.Add(dgvAuditLog);

            pg.Controls.Add(dgvAudit);
            pg.Controls.Add(grpLog);
            pg.Controls.Add(top);
            return pg;
        }

        /// <summary>بدء جرد جديد: تحميل كل المنتجات مع الكمية المسجلة، والكمية الفعلية مساوية لها مبدئياً.</summary>
        private void StartAudit()
        {
            try
            {
                DataTable src = DatabaseHelper.GetTable("SELECT ProductID, ProductName, Quantity FROM Products ORDER BY ProductName");
                var dt = new DataTable();
                dt.Columns.Add("ProductID", typeof(int));
                dt.Columns.Add("ProductName", typeof(string));
                dt.Columns.Add("SystemQty", typeof(int));
                dt.Columns.Add("ActualQty", typeof(int));
                dt.Columns.Add(new DataColumn("Diff", typeof(int), "ActualQty - SystemQty")); // عمود محسوب تلقائياً
                foreach (DataRow r in src.Rows)
                {
                    int q = Convert.ToInt32(r["Quantity"]);
                    dt.Rows.Add(Convert.ToInt32(r["ProductID"]), r["ProductName"], q, q);
                }
                dgvAudit.DataSource = dt;
                SetHeaders(dgvAudit, "ProductID", "الرقم", "ProductName", "اسم المنتج", "SystemQty", "الكمية المسجلة",
                           "ActualQty", "الكمية الفعلية (عدّلها)", "Diff", "الفرق");
                foreach (DataGridViewColumn c in dgvAudit.Columns) c.ReadOnly = c.Name != "ActualQty";
                pbAudit.Value = 0;
                ColorAuditRows();
                UpdateAuditSummary();
            }
            catch (Exception ex) { Err("تعذّر بدء الجرد", ex); }
        }

        /// <summary>التحقق من أن الكمية الفعلية عدد صحيح غير سالب (منع إدخال النصوص).</summary>
        private void DgvAudit_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            if (e.RowIndex < 0 || dgvAudit.Columns[e.ColumnIndex].Name != "ActualQty") return;
            int v;
            if (!int.TryParse(Convert.ToString(e.FormattedValue), NumberStyles.None, CultureInfo.InvariantCulture, out v))
            {
                e.Cancel = true;
                Warn("أدخل عدداً صحيحاً غير سالب للكمية الفعلية.");
            }
        }

        /// <summary>تلوين الصفوف التي يوجد فيها فرق بين المسجل والفعلي (أحمر للنقص، أصفر للزيادة).</summary>
        private void ColorAuditRows()
        {
            foreach (DataGridViewRow r in dgvAudit.Rows)
            {
                try
                {
                    int diff = Convert.ToInt32(r.Cells["Diff"].Value);
                    r.DefaultCellStyle.BackColor = diff < 0 ? RowExpired : diff > 0 ? RowLow : Color.White;
                }
                catch { }
            }
        }

        private void UpdateAuditSummary()
        {
            int n = 0;
            foreach (DataGridViewRow r in dgvAudit.Rows)
                if (Convert.ToInt32(r.Cells["Diff"].Value) != 0) n++;
            lblAuditSummary.Text = "عدد المنتجات التي بها فروقات: " + n;
        }

        /// <summary>
        /// اعتماد الجرد: لكل منتج به فرق يُسجَّل الجرد ويُحدَّث رصيده إلى الكمية الفعلية.
        /// يتقدم ProgressBar مع كل منتج.
        /// </summary>
        private void ApplyAuditAll()
        {
            if (dgvAudit.DataSource == null) { Warn("ابدأ جرداً جديداً أولاً."); return; }
            dgvAudit.EndEdit();

            var diffs = new System.Collections.Generic.List<DataGridViewRow>();
            foreach (DataGridViewRow r in dgvAudit.Rows)
                if (Convert.ToInt32(r.Cells["Diff"].Value) != 0) diffs.Add(r);

            if (diffs.Count == 0) { Info("لا توجد فروقات بين الكميات المسجلة والفعلية."); return; }
            if (!Confirm("سيتم تحديث رصيد " + diffs.Count + " منتج إلى الكمية الفعلية. هل تريد الاعتماد؟")) return;

            try
            {
                pbAudit.Minimum = 0; pbAudit.Maximum = diffs.Count; pbAudit.Value = 0;
                int done = 0;
                foreach (DataGridViewRow r in diffs)
                {
                    bool ok = DatabaseHelper.RecordAndApplyAudit(
                        Convert.ToInt32(r.Cells["ProductID"].Value),
                        Convert.ToInt32(r.Cells["SystemQty"].Value),
                        Convert.ToInt32(r.Cells["ActualQty"].Value),
                        dtpAudit.Value);
                    if (!ok) break;     // الرسالة ظهرت من DatabaseHelper
                    done++;
                    pbAudit.Value = done;
                }
                Info("تم اعتماد الجرد وتحديث رصيد " + done + " منتج.");
                RefreshAll();
                StartAudit();
            }
            catch (Exception ex) { Err("فشل اعتماد الجرد", ex); }
        }

        #endregion

        #region سجلات الحركات والكمية الحالية

        /// <summary>تحميل جداول السجل الثلاثة (وارد / تالف / جرد).</summary>
        private void LoadMovementLogs()
        {
            if (dgvInLog == null) return;

            dgvInLog.DataSource = DatabaseHelper.GetTable(
                @"SELECT s.StockInID, p.ProductName, s.Quantity, s.SupplyDate, s.NewExpiryDate,
                         IFNULL(c.CompanyName,'') AS CompanyName, IFNULL(s.Notes,'') AS Notes
                  FROM StockIn s JOIN Products p ON p.ProductID = s.ProductID
                  LEFT JOIN Companies c ON c.CompanyID = s.CompanyID
                  ORDER BY s.StockInID DESC");
            SetHeaders(dgvInLog, "StockInID", "رقم الحركة", "ProductName", "المنتج", "Quantity", "الكمية",
                       "SupplyDate", "تاريخ التوريد", "NewExpiryDate", "الصلاحية الجديدة", "CompanyName", "المورد", "Notes", "ملاحظات");

            dgvDmgLog.DataSource = DatabaseHelper.GetTable(
                @"SELECT d.DamageID, p.ProductName, d.Quantity, r.ReasonName, d.DamageDate, IFNULL(d.Notes,'') AS Notes
                  FROM DamagedItems d JOIN Products p ON p.ProductID = d.ProductID
                  JOIN DamageReasons r ON r.ReasonID = d.ReasonID
                  ORDER BY d.DamageID DESC");
            SetHeaders(dgvDmgLog, "DamageID", "رقم الحركة", "ProductName", "المنتج", "Quantity", "الكمية التالفة",
                       "ReasonName", "السبب", "DamageDate", "التاريخ", "Notes", "ملاحظات");

            dgvAuditLog.DataSource = DatabaseHelper.GetTable(
                @"SELECT a.AuditID, p.ProductName, a.SystemQty, a.ActualQty, a.Difference, a.AuditDate
                  FROM InventoryAudits a JOIN Products p ON p.ProductID = a.ProductID
                  WHERE a.IsApplied = 1 ORDER BY a.AuditID DESC");
            SetHeaders(dgvAuditLog, "AuditID", "رقم", "ProductName", "المنتج", "SystemQty", "المسجلة",
                       "ActualQty", "الفعلية", "Difference", "الفرق", "AuditDate", "التاريخ");
        }

        /// <summary>عرض الرصيد الحالي للمنتج المختار بجانب القائمة.</summary>
        private void ShowCurrentQty(ComboBox cbo, Label lbl)
        {
            if (cbo == null || lbl == null) return;
            int id = ComboId(cbo);
            if (id == 0) { lbl.Text = "الكمية الحالية: -"; return; }
            object q = DatabaseHelper.Scalar("SELECT Quantity FROM Products WHERE ProductID=@p", DatabaseHelper.P("@p", id));
            lbl.Text = "الكمية الحالية: " + (q ?? "-");
        }

        #endregion
    }
}
