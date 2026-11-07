// OskNavHelper - 手柄操作 Windows 屏幕键盘的辅助程序
// 屏幕键盘(OSK)打开时: 拦截方向键/回车/Esc, 把方向键变成"在按键间移动选择",
// 回车=按下当前选中的键, Esc=关闭键盘。选择用鼠标光标悬停在按键上直观显示。
// 键盘关闭时完全不干预任何输入。
// Windows 11 屏幕键盘的按键不接收键盘焦点, 因此用"逻辑网格导航 + 坐标点击"实现。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Forms;

namespace OskNavHelper
{
    internal static class N
    {
        public const int WH_KEYBOARD_LL = 13;
        public const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
        public const uint VK_LEFT = 0x25, VK_UP = 0x26, VK_RIGHT = 0x27, VK_DOWN = 0x28, VK_RETURN = 0x0D, VK_ESCAPE = 0x1B;
        public const uint VK_LCONTROL = 0xA2, VK_LWIN = 0x5B, VK_O = 0x4F;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const uint MOUSEEVENTF_LEFTDOWN = 0x0002, MOUSEEVENTF_LEFTUP = 0x0004;
        public const uint WM_CLOSE = 0x0010;

        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] public struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public UIntPtr dwExtraInfo; }
        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);
        [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte sc, uint f, UIntPtr e);
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SetWindowsHookEx(int id, LowLevelKeyboardProc fn, IntPtr mod, uint tid);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hhk, int code, IntPtr w, IntPtr l);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)] public static extern IntPtr GetModuleHandle(string n);
    }

    internal sealed class KeyInfo
    {
        public string Name;
        public double X, Y, W, H;
        public double CX { get { return X + W / 2.0; } }
        public double CY { get { return Y + H / 2.0; } }
    }

    internal sealed class Ctx : ApplicationContext
    {
        public static readonly string LOG =
            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "osknav.log");

        IntPtr hk = IntPtr.Zero;
        N.LowLevelKeyboardProc proc;
        IntPtr osk = IntPtr.Zero;
        bool open = false;
        List<KeyInfo> keys = new List<KeyInfo>();
        List<List<int>> grid = new List<List<int>>();
        int sel = -1, selRow = -1, selCol = -1;
        N.RECT oskRect;
        N.POINT saved;
        bool cursorSaved = false;
        Timer poll, act, hold;
        volatile bool pendingClick = false, pendingToggle = false, clickUp = false;
        int heldDir = 0;
        DateTime heldSince, lastClick = DateTime.MinValue, reRefreshAt = DateTime.MinValue;
        bool repeating = false;

        public Ctx()
        {
            proc = new N.LowLevelKeyboardProc(Hook);
            hk = N.SetWindowsHookEx(N.WH_KEYBOARD_LL, proc, N.GetModuleHandle(null), 0);
            poll = new Timer(); poll.Interval = 150; poll.Tick += OnPoll; poll.Start();
            act = new Timer(); act.Interval = 15; act.Tick += OnAct; act.Start();
            hold = new Timer(); hold.Interval = 40; hold.Tick += OnHold; hold.Start();
            Log("started");
        }

        static void Log(string s)
        {
            try { System.IO.File.AppendAllText(LOG, DateTime.Now.ToString("HH:mm:ss") + " " + s + "\r\n"); }
            catch { }
        }

        IntPtr FindOsk()
        {
            try
            {
                Process[] ps = Process.GetProcessesByName("osk");
                foreach (Process p in ps)
                {
                    try
                    {
                        IntPtr h = p.MainWindowHandle;
                        if (h != IntPtr.Zero && N.IsWindow(h) && N.IsWindowVisible(h)) return h;
                    }
                    catch { }
                }
            }
            catch { }
            return IntPtr.Zero;
        }

        void OnPoll(object s, EventArgs e)
        {
            try
            {
                IntPtr h = FindOsk();
                if (h == IntPtr.Zero) { if (open) Close(); return; }
                if (!open) Open(h);
                else
                {
                    N.RECT r;
                    N.GetWindowRect(h, out r);
                    bool rectChanged = (r.Left != oskRect.Left || r.Top != oskRect.Top || r.Right != oskRect.Right || r.Bottom != oskRect.Bottom);
                    if (rectChanged || DateTime.UtcNow > reRefreshAt)
                    {
                        oskRect = r; Refresh();
                        if (sel >= keys.Count) sel = keys.Count - 1;
                        reRefreshAt = DateTime.MaxValue;
                        Log("layout changed, keys=" + keys.Count);
                    }
                    Snap();
                }
            }
            catch { }
        }

        void Open(IntPtr h)
        {
            osk = h; open = true;
            N.GetCursorPos(out saved); cursorSaved = true;
            N.GetWindowRect(h, out oskRect);
            Refresh(); SelInitial(); Snap();
            reRefreshAt = DateTime.UtcNow.AddMilliseconds(800); // 开启动画结束后再刷新一次
            Log("osk open, keys=" + keys.Count);
        }

        void Close()
        {
            open = false; osk = IntPtr.Zero; keys.Clear(); grid.Clear();
            sel = -1; selRow = -1; selCol = -1; heldDir = 0; repeating = false;
            if (cursorSaved) { N.SetCursorPos(saved.X, saved.Y); cursorSaved = false; }
            Log("osk closed");
        }

        void Refresh()
        {
            string keep = (sel >= 0 && sel < keys.Count) ? keys[sel].Name : null;
            keys.Clear();
            AutomationElement el = AutomationElement.FromHandle(osk);
            if (el != null)
            {
                Condition c = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button);
                AutomationElementCollection all = el.FindAll(TreeScope.Descendants, c);
                double oskW = oskRect.Right - oskRect.Left, oskH = oskRect.Bottom - oskRect.Top;
                for (int i = 0; i < all.Count; i++)
                {
                    AutomationElement k = all[i];
                    System.Windows.Rect r = k.Current.BoundingRectangle;
                    if (r.IsEmpty || r.Width < 10 || r.Height < 10) continue;
                    // 部分隐藏按键的坐标是 NaN/无穷大, 必须过滤掉
                    if (double.IsNaN(r.X) || double.IsNaN(r.Y) || double.IsInfinity(r.X) || double.IsInfinity(r.Y)) continue;
                    if (r.Width > oskW * 0.6 || r.Height > oskH * 0.45) continue;
                    string nm = k.Current.Name;
                    if (nm == null) continue;
                    nm = nm.Trim();
                    if (nm.Length == 0) continue;  // 无名字的通常是隐形覆盖层, 跳过
                    keys.Add(new KeyInfo { Name = nm, X = r.X, Y = r.Y, W = r.Width, H = r.Height });
                }
            }
            BuildGrid();
            sel = -1; selRow = -1; selCol = -1;
            if (keep != null)
            {
                for (int i = 0; i < keys.Count; i++)
                    if (keys[i].Name == keep) { Locate(i); return; }
            }
            SelInitial();
        }

        void BuildGrid()
        {
            grid = new List<List<int>>();
            List<int> idx = new List<int>();
            for (int i = 0; i < keys.Count; i++) idx.Add(i);
            idx.Sort(delegate(int a, int b)
            {
                double ya = keys[a].CY, yb = keys[b].CY;
                if (Math.Abs(ya - yb) > 30.0) return ya.CompareTo(yb);
                return keys[a].X.CompareTo(keys[b].X);
            });
            List<int> cur = null;
            double lastY = double.NaN;
            foreach (int i in idx)
            {
                double y = keys[i].CY;
                if (cur == null || Math.Abs(y - lastY) > 30.0) { cur = new List<int>(); grid.Add(cur); lastY = y; }
                cur.Add(i);
            }
        }

        void Locate(int idx)
        {
            for (int r = 0; r < grid.Count; r++)
                for (int c = 0; c < grid[r].Count; c++)
                    if (grid[r][c] == idx) { selRow = r; selCol = c; sel = idx; return; }
        }

        void SelInitial()
        {
            string[] pref = new string[] { "q", "a", "z" };
            for (int p = 0; p < pref.Length; p++)
                for (int i = 0; i < keys.Count; i++)
                    if (keys[i].Name == pref[p]) { Locate(i); return; }
            if (grid.Count > 0 && grid[0].Count > 0) { selRow = 0; selCol = 0; sel = grid[0][0]; }
        }

        void Snap()
        {
            if (sel >= 0 && sel < keys.Count)
                N.SetCursorPos((int)keys[sel].CX, (int)keys[sel].CY);
        }

        void Move(int dir)
        {
            if (grid.Count == 0 || selRow < 0 || selCol < 0) return;
            int r = selRow, c = selCol;
            if (dir == 1) { if (c + 1 < grid[r].Count) c++; }
            else if (dir == 3) { if (c > 0) c--; }
            else if (dir == 4) { if (r + 1 < grid.Count) { r++; if (c >= grid[r].Count) c = grid[r].Count - 1; } }
            else if (dir == 2) { if (r > 0) { r--; if (c >= grid[r].Count) c = grid[r].Count - 1; } }
            if (r != selRow || c != selCol)
            {
                string from = (sel >= 0 && sel < keys.Count) ? keys[sel].Name : "?";
                selRow = r; selCol = c; sel = grid[r][c];
                Snap();
                Log("move " + dir + ": " + from + " -> " + keys[sel].Name);
            }
        }

        void ClickSel()
        {
            if (sel < 0 || sel >= keys.Count) return;
            N.SetCursorPos((int)keys[sel].CX, (int)keys[sel].CY);
            N.mouse_event(N.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            clickUp = true;
            lastClick = DateTime.UtcNow;
            // 点击可能切换键盘布局(如 Shift 粘滞), 稍后重新缓存
            reRefreshAt = DateTime.UtcNow.AddMilliseconds(400);
        }

        void CloseOsk()
        {
            // Ctrl+Win+O 只能打开不能关闭, 所以直接给 OSK 窗口发 WM_CLOSE
            if (osk != IntPtr.Zero)
            {
                N.SendMessage(osk, N.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                Log("sent WM_CLOSE");
            }
        }

        void OnAct(object s, EventArgs e)
        {
            if (clickUp) { clickUp = false; N.mouse_event(N.MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero); }
            if (pendingClick) { pendingClick = false; ClickSel(); }
            if (pendingToggle) { pendingToggle = false; CloseOsk(); }
        }

        void OnHold(object s, EventArgs e)
        {
            if (heldDir != 0 && open)
            {
                if (!repeating)
                {
                    if ((DateTime.UtcNow - heldSince).TotalMilliseconds > 350) repeating = true;
                    else return;
                }
                Move(heldDir);
            }
        }

        bool RecentClick()
        {
            return (DateTime.UtcNow - lastClick).TotalMilliseconds < 130;
        }

        IntPtr Hook(int code, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (code >= 0 && open)
                {
                    int m = (int)wParam;
                    bool down = (m == N.WM_KEYDOWN || m == N.WM_SYSKEYDOWN);
                    bool up = (m == N.WM_KEYUP || m == N.WM_SYSKEYUP);
                    if (down || up)
                    {
                        N.KBDLLHOOKSTRUCT kb = (N.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(N.KBDLLHOOKSTRUCT));
                        uint vk = kb.vkCode;
                        bool nav = (vk == N.VK_LEFT || vk == N.VK_RIGHT || vk == N.VK_UP || vk == N.VK_DOWN);
                        bool enter = (vk == N.VK_RETURN), esc = (vk == N.VK_ESCAPE);
                        if (nav || enter || esc)
                        {
                            // 刚点击过 OSK 按键时, OSK 会立刻合成一次真实按键, 此时放行
                            if (RecentClick()) return N.CallNextHookEx(hk, code, wParam, lParam);
                            if (down)
                            {
                                if (nav)
                                {
                                    int d = vk == N.VK_RIGHT ? 1 : vk == N.VK_UP ? 2 : vk == N.VK_LEFT ? 3 : 4;
                                    heldDir = d; heldSince = DateTime.UtcNow; repeating = false;
                                    Move(d);
                                }
                                else if (enter) pendingClick = true;
                                else pendingToggle = true;
                            }
                            else
                            {
                                if (nav) heldDir = 0;
                            }
                            return (IntPtr)1;
                        }
                    }
                }
            }
            catch { }
            return N.CallNextHookEx(hk, code, wParam, lParam);
        }
    }

    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            bool first;
            using (System.Threading.Mutex mx = new System.Threading.Mutex(true, @"Global\OskNavHelper", out first))
            {
                if (!first) return;
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
}
