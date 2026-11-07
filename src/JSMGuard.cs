// JSMGuard - Xbox 手柄控制守护/启动程序
// 双击本程序(桌面快捷方式):
//   - 启动 JoyShockMapper (控制台窗口自动隐藏, 后台常驻, 托盘图标)
//   - 启动 OskNavHelper (屏幕键盘手柄导航, 无窗口)
// 每次 JSM 被关闭(点 X / 托盘退出)时弹出询问:
//   [是] 最小化到托盘继续运行   [否] 完全关闭(连带退出全部后台组件)
// 组件路径: 优先从本程序所在目录的相对路径查找, 找不到时回退到下方的默认路径常量。
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace JSMGuard
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            bool first;
            using (Mutex mx = new Mutex(true, @"Global\JSMGuard_Mutex", out first))
            {
                if (!first)
                {
                    // 第二个实例: 通知已运行中的守护程序启动 JSM
                    try
                    {
                        EventWaitHandle ev;
                        if (EventWaitHandle.TryOpenExisting(@"Global\JSMGuard_Start", out ev)) ev.Set();
                    }
                    catch { }
                    return;
                }
                AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs a)
                {
                    try { System.IO.File.AppendAllText(Ctx.LOG, DateTime.Now + " EX " + a.ExceptionObject + "\r\n"); }
                    catch { }
                };
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Ctx());
            }
        }
    }

    internal sealed class Ctx : ApplicationContext
    {
        public static readonly string LOG =
            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "jsmguard.log");

        // ---- 组件路径: 优先 exe 相对路径, 找不到时回退到默认安装布局 ----
        static string Resolve(string relative, string fallback)
        {
            try
            {
                string p = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relative));
                if (System.IO.File.Exists(p)) return p;
            }
            catch { }
            return fallback;
        }

        public static readonly string JSM_PATH = Resolve(
            @"..\vendor\JoyShockMapper_SDL2_x64\JoyShockMapper.exe",
            @"D:\Tools\JoyShockMapper\JoyShockMapper_SDL2_x64\JoyShockMapper.exe");
        public static readonly string JSM_DIR = System.IO.Path.GetDirectoryName(JSM_PATH);
        public static readonly string OSKNAV_PATH = Resolve(
            "OskNavHelper.exe",
            @"D:\Tools\OskNav\OskNavHelper.exe");
        public static readonly string OSKNAV_DIR = System.IO.Path.GetDirectoryName(OSKNAV_PATH);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr h);

        EventWaitHandle startReq;
        System.Windows.Forms.Timer poll;
        bool promptShowing = false, userSaidNo = false, shuttingDown = false;
        DateTime hideUntil = DateTime.MinValue;

        public Ctx()
        {
            startReq = new EventWaitHandle(false, EventResetMode.AutoReset, @"Global\JSMGuard_Start");
            Microsoft.Win32.SystemEvents.SessionEnding += delegate(object s, Microsoft.Win32.SessionEndingEventArgs e)
            {
                shuttingDown = true;
            };
            poll = new System.Windows.Forms.Timer(); poll.Interval = 800; poll.Tick += OnPoll; poll.Start();
            Log("started");
            EnsureOskNav();
            EnsureJsm();
        }

        static void Log(string s)
        {
            try { System.IO.File.AppendAllText(LOG, DateTime.Now.ToString("HH:mm:ss") + " " + s + "\r\n"); }
            catch { }
        }

        bool JsmRunning()
        {
            try { return Process.GetProcessesByName("JoyShockMapper").Length > 0; }
            catch { return false; }
        }

        bool OskNavRunning()
        {
            try { return Process.GetProcessesByName("OskNavHelper").Length > 0; }
            catch { return false; }
        }

        void EnsureOskNav()
        {
            if (OskNavRunning()) return;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(OSKNAV_PATH);
                psi.WorkingDirectory = OSKNAV_DIR;
                psi.UseShellExecute = false;
                Process.Start(psi);
                Log("launched osknav");
            }
            catch (Exception ex) { Log("osknav launch failed: " + ex.Message); }
        }

        void KillOskNav()
        {
            try
            {
                foreach (Process p in Process.GetProcessesByName("OskNavHelper")) p.Kill();
                Log("killed osknav");
            }
            catch { }
        }

        void EnsureJsm()
        {
            if (JsmRunning()) { userSaidNo = false; return; }
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(JSM_PATH);
                psi.WorkingDirectory = JSM_DIR;
                psi.UseShellExecute = false;
                Process.Start(psi);
                userSaidNo = false;
                // .NET 的隐藏启动对控制台程序无效, 启动后几秒内用 Win32 真正藏起窗口
                hideUntil = DateTime.UtcNow.AddSeconds(6);
                Log("launched jsm (hidden)");
            }
            catch (Exception ex) { Log("launch failed: " + ex.Message); }
        }

        void HideJsmWindow()
        {
            if (DateTime.UtcNow > hideUntil) return;
            try
            {
                foreach (Process p in Process.GetProcessesByName("JoyShockMapper"))
                {
                    IntPtr h = p.MainWindowHandle;
                    if (h != IntPtr.Zero && IsWindowVisible(h))
                    {
                        ShowWindow(h, 0); // SW_HIDE
                        Log("hid jsm window");
                    }
                }
            }
            catch { }
        }

        void OnPoll(object s, EventArgs e)
        {
            try
            {
                if (shuttingDown) return;
                if (startReq.WaitOne(0)) { userSaidNo = false; EnsureJsm(); return; }
                EnsureOskNav();
                HideJsmWindow();
                if (JsmRunning()) { userSaidNo = false; return; }
                if (promptShowing || userSaidNo) return;
                promptShowing = true;
                Log("prompt shown");
                DialogResult r = MessageBox.Show(
                    "手柄控制程序已被关闭。\r\n\r\n" +
                    "选择「是」: 最小化到托盘继续运行 (后台常驻)\r\n" +
                    "选择「否」: 完全关闭",
                    "手柄控制",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button1,
                    (MessageBoxOptions)0x00050000); // MB_TOPMOST | MB_SETFOREGROUND
                promptShowing = false;
                if (r == DialogResult.Yes)
                {
                    userSaidNo = false;
                    Log("prompt: yes (relaunch to tray)");
                    EnsureJsm();
                }
                else
                {
                    userSaidNo = true;
                    Log("prompt: no (closed everything)");
                    KillOskNav();
                    ExitThread();
                }
            }
            catch { }
        }
    }
}
