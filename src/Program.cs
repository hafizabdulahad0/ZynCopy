using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ZynCopy
{
    static class Program
    {
        private const string MutexName = "Global\\ZynCopy_SingleInstance_Mutex_ZynOps";
        public const uint WM_COPYDATA = 0x004A;

        [StructLayout(LayoutKind.Sequential)]
        public struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, ref COPYDATASTRUCT lParam);

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
            }

            bool createdNew;
            using (Mutex mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    // An instance of ZynCopy is already running!
                    // Forward arguments to the running window and exit without creating a new window.
                    if (args != null && args.Length > 0)
                    {
                        string payload = string.Join("\n", args);
                        SendArgsToRunningInstance(payload);
                    }
                    else
                    {
                        // Just bring the existing window to the front
                        IntPtr hWnd = FindExistingWindow();
                        if (hWnd != IntPtr.Zero)
                        {
                            SetForegroundWindow(hWnd);
                        }
                    }
                    return;
                }

                // Primary instance
                Application.Run(new MainForm(args));
            }
        }

        private static IntPtr FindExistingWindow()
        {
            for (int i = 0; i < 15; i++)
            {
                IntPtr hWnd = FindWindow(null, MainForm.WindowTitle);
                if (hWnd != IntPtr.Zero) return hWnd;
                Thread.Sleep(50);
            }
            return IntPtr.Zero;
        }

        private static void SendArgsToRunningInstance(string payload)
        {
            IntPtr hWnd = FindExistingWindow();
            if (hWnd == IntPtr.Zero) return;

            byte[] sarr = Encoding.Unicode.GetBytes(payload);
            int len = sarr.Length;
            COPYDATASTRUCT cds = new COPYDATASTRUCT();
            cds.dwData = (IntPtr)1;
            cds.cbData = len + 2;
            cds.lpData = Marshal.StringToHGlobalUni(payload);

            try
            {
                SendMessage(hWnd, WM_COPYDATA, IntPtr.Zero, ref cds);
                SetForegroundWindow(hWnd);
            }
            finally
            {
                Marshal.FreeHGlobal(cds.lpData);
            }
        }
    }
}
