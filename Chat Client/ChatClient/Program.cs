using System;
using System.Windows.Forms;

namespace WindowsFormsApp1   // sesuaikan dengan namespace Form1.cs
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // jalankan Form1 sebagai form utama
            Application.Run(new Form1());
        }
    }
}
