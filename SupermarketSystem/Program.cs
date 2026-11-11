using System;
using System.Windows.Forms;

namespace SupermarketSystem
{
    /// <summary>نقطة بداية البرنامج.</summary>
    static class Program
    {
        /// <summary>
        /// تهيئة قاعدة البيانات (إنشاء الجداول عند أول تشغيل) ثم تشغيل النافذة الرئيسية.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // إنشاء ملف القاعدة والجداول إن لم تكن موجودة
            DatabaseHelper.Initialize();

            Application.Run(new Form1());
        }
    }
}
