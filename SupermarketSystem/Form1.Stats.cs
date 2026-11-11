using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace SupermarketSystem
{
    /// <summary>التبويب الخامس: لوحة الإحصائيات (بطاقات أرقام + نسبة امتلاء المستودع).</summary>
    public partial class Form1
    {
        private Label lblValTotal, lblSaleValue, lblCountAll, lblCountAvail, lblCountLow, lblCountExpired, lblCountNear, lblFill;
        private ProgressBar pbWarehouse;
        private NumericUpDown numCapacity;

        #region بناء الواجهة

        private void BuildStatsTab()
        {
            TabPage page = MkPage("5. لوحة الإحصائيات");

            var cards = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6), RightToLeft = RightToLeft.Yes, WrapContents = true };
            cards.Controls.Add(MkCard("إجمالي قيمة المخزون (كمية × سعر الشراء)", out lblValTotal, ClrPrimary));
            cards.Controls.Add(MkCard("القيمة المتوقعة للبيع (كمية × سعر المبيع)", out lblSaleValue, ClrAccent));
            cards.Controls.Add(MkCard("عدد الأصناف المسجلة", out lblCountAll, ClrGray));
            cards.Controls.Add(MkCard("الأصناف المتوفرة (كمية > 0)", out lblCountAvail, ClrSuccess));
            cards.Controls.Add(MkCard("الأصناف التي أوشكت على النفاد", out lblCountLow, ClrWarning));
            cards.Controls.Add(MkCard("الأصناف المنتهية الصلاحية", out lblCountExpired, ClrDanger));
            cards.Controls.Add(MkCard("الأصناف القريبة من الانتهاء", out lblCountNear, ClrWarning));

            // سعة المستودع ونسبة الامتلاء
            GroupBox grp = MkGroup("سعة المستودع");
            grp.Dock = DockStyle.Top;
            var flow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, RightToLeft = RightToLeft.Yes, Font = new Font("Segoe UI", 10F, FontStyle.Regular), ForeColor = Color.Black };
            flow.Controls.Add(new Label { Text = "السعة الإجمالية (وحدات):", AutoSize = true, Margin = new Padding(4, 10, 4, 0) });
            numCapacity = new NumericUpDown { Minimum = 1, Maximum = 100000000, Value = 5000, Width = 120, Margin = new Padding(4, 6, 4, 0) };
            flow.Controls.Add(numCapacity);
            flow.Controls.Add(MkBtn("حفظ السعة", ClrSuccess, BtnSaveCapacity_Click));
            pbWarehouse = new ProgressBar { Width = 420, Height = 26, Margin = new Padding(16, 8, 4, 0), RightToLeftLayout = true };
            flow.Controls.Add(pbWarehouse);
            lblFill = new Label { AutoSize = true, Margin = new Padding(4, 10, 4, 0), Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            flow.Controls.Add(lblFill);
            grp.Controls.Add(flow);

            page.Controls.Add(grp);
            page.Controls.Add(cards);
            tabMain.TabPages.Add(page);
        }

        /// <summary>بطاقة إحصائية: عنوان صغير + رقم كبير ملوّن.</summary>
        private Panel MkCard(string title, out Label valueLabel, Color color)
        {
            var p = new Panel { Width = 270, Height = 110, BackColor = Color.White, Margin = new Padding(8), Padding = new Padding(10) };
            var bar = new Panel { Dock = DockStyle.Right, Width = 6, BackColor = color };   // شريط ملوّن جانبي
            var t = new Label { Text = title, Dock = DockStyle.Top, Height = 40, ForeColor = ClrGray, TextAlign = ContentAlignment.MiddleLeft };
            valueLabel = new Label { Text = "0", Dock = DockStyle.Fill, ForeColor = color, Font = new Font("Segoe UI", 20F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
            p.Controls.Add(valueLabel);
            p.Controls.Add(t);
            p.Controls.Add(bar);
            return p;
        }

        #endregion

        #region الحساب والتحديث

        /// <summary>حساب كل الإحصائيات من قاعدة البيانات وعرضها (تُستدعى بعد كل عملية).</summary>
        private void LoadStats()
        {
            if (lblValTotal == null) return;
            int days = (int)numAlertDays.Value;

            lblValTotal.Text = DatabaseHelper.GetInventoryValue().ToString("N2", CultureInfo.InvariantCulture);
            object sale = DatabaseHelper.Scalar("SELECT IFNULL(SUM(Quantity*SalePrice),0) FROM Products");
            lblSaleValue.Text = Convert.ToDecimal(sale ?? 0).ToString("N2", CultureInfo.InvariantCulture);

            lblCountAll.Text = Count("SELECT COUNT(*) FROM Products");
            lblCountAvail.Text = Count("SELECT COUNT(*) FROM Products WHERE Quantity > 0");
            lblCountLow.Text = Count("SELECT COUNT(*) FROM Products WHERE Status='نشط' AND Quantity <= MinQuantity");
            lblCountExpired.Text = Count("SELECT COUNT(*) FROM Products WHERE Quantity > 0 AND ExpiryDate < date('now','localtime')");
            lblCountNear.Text = Count("SELECT COUNT(*) FROM Products WHERE Quantity > 0 AND ExpiryDate >= date('now','localtime') " +
                                      "AND ExpiryDate <= date('now','localtime','+" + days + " day')");

            // سعة المستودع
            object cap = DatabaseHelper.Scalar("SELECT SettingValue FROM Settings WHERE SettingKey='WarehouseCapacity'");
            decimal capacity = cap == null ? 5000 : Convert.ToDecimal(cap);
            if (capacity >= numCapacity.Minimum && capacity <= numCapacity.Maximum) numCapacity.Value = capacity;

            object tot = DatabaseHelper.Scalar("SELECT IFNULL(SUM(Quantity),0) FROM Products");
            int pct = DatabaseHelper.GetWarehouseFillPercent();
            pbWarehouse.Value = Math.Max(0, Math.Min(100, pct));
            lblFill.Text = pct + "%  (" + tot + " من " + capacity.ToString("N0", CultureInfo.InvariantCulture) + " وحدة)";
        }

        /// <summary>تنفيذ استعلام COUNT وإرجاع النتيجة كنص.</summary>
        private string Count(string sql)
        {
            object o = DatabaseHelper.Scalar(sql);
            return Convert.ToString(o ?? 0);
        }

        /// <summary>حفظ سعة المستودع الجديدة في جدول الإعدادات ثم تحديث النسبة.</summary>
        private void BtnSaveCapacity_Click(object sender, EventArgs e)
        {
            try
            {
                int rows = DatabaseHelper.Execute("UPDATE Settings SET SettingValue=@v WHERE SettingKey='WarehouseCapacity'",
                    DatabaseHelper.P("@v", ((int)numCapacity.Value).ToString()));
                if (rows > 0) { Info("تم حفظ سعة المستودع."); LoadStats(); UpdateStatusBar(); }
            }
            catch (Exception ex) { Err("فشل حفظ السعة", ex); }
        }

        #endregion
    }
}
