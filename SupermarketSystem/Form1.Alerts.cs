using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace SupermarketSystem
{
    /// <summary>التبويب الثاني: الصلاحيات والتنبيهات (قريب الانتهاء + النواقص).</summary>
    public partial class Form1
    {
        private NumericUpDown numAlertDays;
        private Button btnRefreshAlerts;
        private Label lblAlertSummary;
        private DataGridView dgvExpiring;
        private ListBox lstExpiry, lstShortage;

        #region بناء الواجهة

        /// <summary>بناء التبويب: شريط علوي (عدد الأيام)، جدول المواد القريبة، وقائمتا إشعارات.</summary>
        private void BuildAlertsTab()
        {
            TabPage page = MkPage("2. الصلاحيات والتنبيهات");

            // ---------- شريط التحكم العلوي ----------
            FlowLayoutPanel top = MkButtons();
            top.Controls.Add(new Label { Text = "عرض المواد التي تنتهي صلاحيتها خلال (يوم):", AutoSize = true, Margin = new Padding(6, 10, 4, 0) });
            numAlertDays = new NumericUpDown { Minimum = 1, Maximum = 365, Value = 15, Width = 70, Margin = new Padding(4, 6, 4, 0) };
            numAlertDays.ValueChanged += (s, e) => { if (!_loading) { LoadAlerts(); LoadProducts(); } };
            btnRefreshAlerts = MkBtn("🔄 فحص الآن", ClrAccent, (s, e) => { LoadAlerts(); LoadProducts(); });
            lblAlertSummary = new Label { AutoSize = true, ForeColor = ClrDanger, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Margin = new Padding(12, 10, 4, 0) };
            top.Controls.AddRange(new Control[] { numAlertDays, btnRefreshAlerts, lblAlertSummary });

            // ---------- الجدول (يمين) + القوائم (يسار) ----------
            var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, RightToLeft = RightToLeft.Yes };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

            dgvExpiring = new DataGridView();
            StyleGrid(dgvExpiring);
            dgvExpiring.DataBindingComplete += (s, e) => ColorExpiringRows();
            main.Controls.Add(dgvExpiring, 0, 0);

            var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, RightToLeft = RightToLeft.Yes };
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            lstExpiry = new ListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, RightToLeft = RightToLeft.Yes, HorizontalScrollbar = true };
            var g1 = new GroupBox { Text = "إشعارات المواد المنتهية / القريبة من الانتهاء", Dock = DockStyle.Fill, ForeColor = ClrWarning, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            g1.Controls.Add(lstExpiry);

            lstShortage = new ListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, RightToLeft = RightToLeft.Yes, HorizontalScrollbar = true };
            var g2 = new GroupBox { Text = "النقص الحاد (تحت الحد الأدنى للطلب)", Dock = DockStyle.Fill, ForeColor = ClrDanger, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            g2.Controls.Add(lstShortage);

            right.Controls.Add(g1, 0, 0);
            right.Controls.Add(g2, 0, 1);
            main.Controls.Add(right, 1, 0);

            page.Controls.Add(main);
            page.Controls.Add(top);
            tabMain.TabPages.Add(page);
        }

        #endregion

        #region تحميل البيانات

        /// <summary>
        /// فحص تلقائي لتواريخ الصلاحية: يعرض المواد المنتهية أو التي ستنتهي خلال المدة المحددة (15 يوماً افتراضياً)
        /// في الجدول وفي قائمة الإشعارات، ويحدّث قائمة النقص الحاد.
        /// </summary>
        private void LoadAlerts()
        {
            if (dgvExpiring == null) return;

            DataTable dt = DatabaseHelper.GetExpiringProducts((int)numAlertDays.Value);
            dgvExpiring.DataSource = dt;
            SetHeaders(dgvExpiring, "ProductID", "الرقم", "ProductName", "اسم المنتج", "Quantity", "الكمية",
                       "ExpiryDate", "تاريخ الصلاحية", "DaysLeft", "الأيام المتبقية");

            lstExpiry.Items.Clear();
            foreach (DataRow r in dt.Rows)
            {
                int days = Convert.ToInt32(r["DaysLeft"]);
                string state = days < 0 ? "منتهية منذ " + (-days) + " يوم"
                             : days == 0 ? "تنتهي اليوم"
                             : "تنتهي بعد " + days + " يوم";
                lstExpiry.Items.Add(r["ProductName"] + "  —  " + state + "  (الكمية: " + r["Quantity"] + ")");
            }

            DataTable low = DatabaseHelper.GetShortages();
            lstShortage.Items.Clear();
            foreach (DataRow r in low.Rows)
                lstShortage.Items.Add(r["ProductName"] + "  —  الكمية: " + r["Quantity"] + "  /  الحد الأدنى: " + r["MinQuantity"]);

            lblAlertSummary.Text = "منتهية/قريبة: " + dt.Rows.Count + "      نواقص: " + low.Rows.Count;
        }

        /// <summary>تلوين جدول التنبيهات: أحمر للمنتهي وبرتقالي للقريب من الانتهاء.</summary>
        private void ColorExpiringRows()
        {
            foreach (DataGridViewRow r in dgvExpiring.Rows)
            {
                try
                {
                    int days = Convert.ToInt32(r.Cells["DaysLeft"].Value);
                    r.DefaultCellStyle.BackColor = days < 0 ? RowExpired : RowNear;
                }
                catch { }
            }
        }

        #endregion
    }
}
