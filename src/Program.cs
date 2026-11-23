using System;
using System.Windows.Forms;

namespace ZynCopy
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args != null && args.Length > 0)
            {
                string firstArg = args[0];
                if (firstArg == "--register-shell")
                {
                    ShellIntegrator.Register();
                    return;
                }
                if (firstArg == "--unregister-shell")
                {
                    ShellIntegrator.Unregister();
                    return;
                }

                // If path passed from Explorer context menu
                Application.Run(new MainForm(firstArg));
                return;
            }

            Application.Run(new MainForm());
        }
    }
}
