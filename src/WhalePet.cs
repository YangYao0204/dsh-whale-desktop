// DSH 小鲸鱼 · 桌面挂件（1:1 复刻 dsh-whale-widget 的桌面版）
// 立绘、泡泡几何、描边、字号全部按插件前端常量走：
//   泡泡 SVG viewBox 1026x700，path + 两个椭圆，fill #FFFFFF / stroke #203170 / 18
//   立绘占容器 59.45%，贴右下角；文字居中在 (454, 247.5)
// 独立进程运行：DSH 关掉后仍留在桌面。

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Net;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WhalePet
{
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; public POINT(int x, int y) { X = x; Y = y; } }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE { public int cx; public int cy; public SIZE(int w, int h) { cx = w; cy = h; } }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst,
            ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int index);
        [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr hWnd, int index, int value);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern IntPtr SetCursor(IntPtr hCursor);
        [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint ms);
        [DllImport("winmm.dll")] public static extern uint timeEndPeriod(uint ms);
        public const int WM_SETCURSOR = 0x0020;
        [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);

        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_NOACTIVATE = 0x08000000;

        public const int ULW_ALPHA = 2;
        public const byte AC_SRC_OVER = 0;
        public const byte AC_SRC_ALPHA = 1;

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_FRAMECHANGED = 0x0020;

        public const int WM_DPICHANGED = 0x02E0;
        public const int WM_MOUSEACTIVATE = 0x0021;
        public const int MA_NOACTIVATE = 3;

        public const uint CREATE_BREAKAWAY_FROM_JOB = 0x01000000;
        public const uint CREATE_NEW_PROCESS_GROUP = 0x00000200;

        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        public static extern int mciSendString(string command, StringBuilder returnValue, int returnLength, IntPtr hwndCallback);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
            public short wShowWindow, cbReserved2;
            public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess, hThread;
            public int dwProcessId, dwThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool CreateProcess(string lpApplicationName, string lpCommandLine,
            IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles,
            uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr hObject);
    }

    internal static class Log
    {
        private static readonly object Gate = new object();
        private static readonly string Path = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "whale-pet.log");

        public static void Write(string message)
        {
            try
            {
                lock (Gate)
                {
                    File.AppendAllText(Path, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { }
        }
    }

    internal sealed class PetConfig
    {
        public int X = int.MinValue;
        public int Y = int.MinValue;
        public float Scale = 1.0f;
        public bool TopMost = true;
        public bool Flipped = false;
        public bool AutoStart = true;
    }

    internal static class Program
    {
        internal static string FrameShotPath;

        [STAThread]
        private static void Main(string[] args)
        {
            // 首次启动时把自身从调用者进程树里"脱离"出去：DSH（或任何启动器）退出后，
            // 小鲸鱼仍然活着。已经在 --detached 模式或者脱离失败，就直接原地运行。
            FrameShotPath = Environment.GetEnvironmentVariable("DSH_WHALE_FRAMESHOT");
            bool detached = Array.IndexOf(args, "--detached") >= 0;
            if (!detached && TryRelaunchDetached()) { Log.Write("relaunched detached"); return; }

            bool isNew;
            using (Mutex mutex = new Mutex(true, @"Local\DSH_WhalePet_Desktop", out isNew))
            {
                if (!isNew) { Log.Write("already running"); return; }
                try
                {
                    Log.Write("start detached=" + detached);
                    try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); }
                    catch { try { Native.SetProcessDPIAware(); } catch { } }
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new PetWindow());
                    Log.Write("exit clean");
                }
                catch (Exception ex)
                {
                    Log.Write("CRASH " + ex);
                    MessageBox.Show(ex.ToString(), "小鲸鱼启动失败");
                }
            }
        }

        private static bool TryRelaunchDetached()
        {
            try
            {
                string exe = Assembly.GetExecutingAssembly().Location;
                Native.STARTUPINFO si = new Native.STARTUPINFO();
                si.cb = Marshal.SizeOf(typeof(Native.STARTUPINFO));
                Native.PROCESS_INFORMATION pi;
                bool ok = Native.CreateProcess(exe, "\"" + exe + "\" --detached",
                    IntPtr.Zero, IntPtr.Zero, false,
                    Native.CREATE_BREAKAWAY_FROM_JOB | Native.CREATE_NEW_PROCESS_GROUP,
                    IntPtr.Zero, Path.GetDirectoryName(exe), ref si, out pi);
                if (!ok)
                {
                    Log.Write("breakaway failed err=" + Marshal.GetLastWin32Error());
                    return false;
                }
                Native.CloseHandle(pi.hProcess);
                Native.CloseHandle(pi.hThread);
                Log.Write("breakaway ok pid=" + pi.dwProcessId);
                return true;
            }
            catch (Exception ex)
            {
                Log.Write("relaunch error " + ex.Message);
                return false;
            }
        }
    }

    internal sealed class PetWindow : Form
    {
        // ===== 与插件前端完全一致的几何常量 =====
        private const float FrameW = 1026f;
        private const float FrameH = 700f;
        private const float WhaleRatio = 0.5945f;      // .dshwv-img 59.45%
        private const float TextCx = 454f;             // 泡泡 bbox 中心
        private const float TextCy = 247.5f;
        private const int StrokeColorR = 0x20, StrokeColorG = 0x31, StrokeColorB = 0x70;   // #203170
        private const float StrokeWidth = 18f;
        private static readonly Color InkColor = Color.FromArgb(0x53, 0x6B, 0xA9);         // #536ba9
        private static readonly Color HintColor = Color.FromArgb(0x9F, 0xB0, 0xD9);        // #9fb0d9

        private const string ClassicBigD =
            "M 827 248 A 373 232 0 1 0 81 246 A 373 232 0 0 0 301 465 A 57 32 10 0 0 413 484 A 373 232 0 0 0 827 248 Z";

        private static readonly string AppDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private static readonly string ConfigPath = Path.Combine(AppDir, "whale-pet.json");
        private static readonly string UsagePath = Environment.GetEnvironmentVariable("DSH_WHALE_USAGE") ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh", ".dshw-usage.json");
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValue = "DSHWhalePet";

        private readonly Random _rnd = new Random();
        private readonly PetConfig _cfg = new PetConfig();
        private Bitmap _whale;
        private Bitmap _whaleScaled;
        private Bitmap _frame;
        private Bitmap _restFrame;
        private GraphicsPath _bubbleUnit;     // 1026x700 坐标
        private GraphicsPath _bubblePx;       // 当前窗口像素坐标
        private float _baseCss;               // 逻辑尺寸（等同插件的 --dshw-base）
        private float _dpi = 1f;
        private Font _fontLabel, _fontAmount, _fontHint, _fontPeak;

        private System.Windows.Forms.Timer _animTimer;
        private System.Windows.Forms.Timer _pollTimer;
        private System.Windows.Forms.Timer _stateTimer;

        private float _bubbleOpen;            // 0..1
        private float _buttonAlpha;           // ☰ 按钮淡入淡出
        private bool _buttonHover;
        private bool _showButton;
        private DateTime _bubbleUntil = DateTime.MinValue;
        private bool _clickThrough;
        private bool _dragging, _moved, _pressStarted;
        private Point _dragStart, _winStart;

        private double? _today;
        private DateTime? _usageStamp;
        private int _tick;

        // ---- 峰谷 / 音效（与插件同一套规则与设置） ----
        private bool _soundOn = true;
        private bool _pressOn = true;
        private double _soundVolume = 0.9;
        private string _soundSet = "duck";
        private bool _audioReady;
        private DateTime _peakNextChange;
        private bool _peakState;
        private bool _peakCacheValid;

        // 2026 年法定节假日（与插件 lib/index.js 的 HOLIDAY_VALLEY 一致，插件注释要求每年 11 月补下一年）
        private static readonly HashSet<string> Holidays = new HashSet<string>(StringComparer.Ordinal)
        {
            "2026-01-01", "2026-01-02", "2026-01-03",
            "2026-02-15", "2026-02-16", "2026-02-17", "2026-02-18", "2026-02-19",
            "2026-02-20", "2026-02-21", "2026-02-22", "2026-02-23",
            "2026-04-04", "2026-04-05", "2026-04-06",
            "2026-05-01", "2026-05-02", "2026-05-03", "2026-05-04", "2026-05-05",
            "2026-06-19", "2026-06-20", "2026-06-21",
            "2026-09-25", "2026-09-26", "2026-09-27",
            "2026-10-01", "2026-10-02", "2026-10-03", "2026-10-04", "2026-10-05",
            "2026-10-06", "2026-10-07",
        };

        // 每轮消耗气泡
        private bool _apiOn = true;
        private string _apiKey;
        private double? _apiBalance;
        private DateTime _apiStamp = DateTime.MinValue;
        private bool _fetching;
        private double? _ledgerBalance;

        private bool _turnCostOn = true;

        // 点击序列 + 按压动画
        private string _quipText;
        private int _quipSize;
        private int _seqIndex;
        private int _lastQuip = -1;
        private float _press;
        private float _pressVel;
        private float _pressTarget;
        private Cursor _cursorNow;
        private bool _dirty = true;
        private long _lastAnimTicks = DateTime.UtcNow.Ticks;
        private bool _fastTimer;
        private string _lastContentSig = string.Empty;
        private int _lastSecond = -1;
        private Font _quipFont;
        private static readonly Dictionary<string, SizeF> _measureCache = new Dictionary<string, SizeF>();
        private static readonly Dictionary<string, int[]> _quipLayoutCache = new Dictionary<string, int[]>();
        private static readonly Dictionary<string, string[]> _quipLineCache = new Dictionary<string, string[]>();
        private List<string> _quipLines = new List<string>();
        private Cursor _pawOpen;
        private Cursor _pawClosed;
        private int _pressLenMs = 400;
        private DateTime _pressAt = DateTime.MinValue;
        private bool _flipped;
        private System.Windows.Forms.Timer _releaseTick;

        // 「消耗 API 时自动出现」
        private bool _autoShow = true;
        private long _lastTurnSeq = -1;
        private bool _busy;
        private bool _waiting;
        private DateTime _busySince = DateTime.MinValue;
        private int _turnCostCloseMs = 5000;
        private long _lastEventTs = -1;
        private DateTime _turnUntil = DateTime.MinValue;
        private string _turnAmount;
        private string _turnTokens;
        private string _turnModel;
        private readonly System.Text.StringBuilder _mciBuf = new System.Text.StringBuilder(256);

        public PetWindow()
        {
            bool firstRun = !File.Exists(ConfigPath);
            LoadConfig();
            if (firstRun) SetAutoStart(true);      // 第一次跑就登记开机自启，之后可在菜单里关
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = _cfg.TopMost;
            MinimumSize = new Size(1, 1);

            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("whale.png"))
                {
                    if (stream != null) _whale = new Bitmap(stream);
                }
            }
            catch (Exception ex) { Log.Write("sprite load fail " + ex.Message); }

            _bubbleUnit = BuildBubblePath();

            _animTimer = new System.Windows.Forms.Timer();
            _animTimer.Interval = 12;
            _animTimer.Tick += OnAnimTick;

            _pollTimer = new System.Windows.Forms.Timer();
            _pollTimer.Interval = 80;
            _pollTimer.Tick += OnPollTick;

            try { _pawOpen = BuildPawCursor(false); _pawClosed = BuildPawCursor(true); } catch { }

            _releaseTick = new System.Windows.Forms.Timer();
            _releaseTick.Interval = 200;
            _releaseTick.Tick += delegate { _releaseTick.Stop(); DoPlayRelease(); };

            _stateTimer = new System.Windows.Forms.Timer();
            _stateTimer.Interval = 1000;
            _stateTimer.Tick += OnStateTick;

            LoadPluginSettings();
            LoadApiKey();
            Log.Write("ctor ok whale=" + (_whale == null ? "null" : _whale.Width + "x" + _whale.Height)
                + " sound=" + _soundOn + " vol=" + _soundVolume + " set=" + _soundSet);
        }

        #region 泡泡路径（SVG -> GDI+）

        private static GraphicsPath BuildBubblePath()
        {
            GraphicsPath path = SvgPath.Parse(ClassicBigD);
            // .dshwv-b1 / .dshwv-b2
            path.AddEllipse(352f - 37.5f, 561f - 26f, 75f, 52f);
            path.AddEllipse(442f - 24.5f, 646f - 18f, 49f, 36f);
            return path;
        }

        #endregion

        #region 窗口样式

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_MOUSEACTIVATE) { m.Result = (IntPtr)Native.MA_NOACTIVATE; return; }
            if (m.Msg == Native.WM_SETCURSOR)
            {
                if (_cursorNow != null) { Native.SetCursor(_cursorNow.Handle); m.Result = (IntPtr)1; return; }
            }
            if (m.Msg == Native.WM_DPICHANGED) ApplyLayout();
            base.WndProc(ref m);
        }

        #endregion

        #region 生命周期 / 布局

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyLayout();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplyLayout();
            RestorePosition();
            _animTimer.Start();
            _pollTimer.Start();
            _stateTimer.Start();
            InitAudio();
            RefreshUsage();
            FetchBalance();
            string dbgPos = Environment.GetEnvironmentVariable("DSH_WHALE_POS");
            if (dbgPos == "left")
            {
                Rectangle wa0 = Screen.PrimaryScreen.WorkingArea;
                Location = new Point(wa0.Left, wa0.Bottom - Height);
                _flipped = true;
                Render();
            }
            string dbgQuip = Environment.GetEnvironmentVariable("DSH_WHALE_QUIP");
            if (!string.IsNullOrEmpty(dbgQuip))
            {
                _quipText = dbgQuip;
                _quipSize = 12;
                BuildQuipFont();
            }

            bool shot = !string.IsNullOrEmpty(Program.FrameShotPath);
            ShowBubble(shot ? 60 : 8);
            if (shot) _bubbleOpen = 1f;
            Render();
            if (shot)
            {
                try { _frame.Save(Program.FrameShotPath, ImageFormat.Png); } catch (Exception ex) { Log.Write("frameshot fail " + ex.Message); }
                Close();
                return;
            }
            Log.Write("shown " + Location.X + "," + Location.Y + " " + Width + "x" + Height);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveConfig();
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_animTimer != null) _animTimer.Dispose();
                if (_pollTimer != null) _pollTimer.Dispose();
                if (_stateTimer != null) _stateTimer.Dispose();
                if (_fontLabel != null) _fontLabel.Dispose();
                if (_fontAmount != null) _fontAmount.Dispose();
                if (_fontHint != null) _fontHint.Dispose();
                if (_frame != null) _frame.Dispose();
                if (_whale != null) _whale.Dispose();
                if (_bubbleUnit != null) _bubbleUnit.Dispose();
                if (_bubblePx != null) _bubblePx.Dispose();
            }
            base.Dispose(disposing);
        }

        private void ApplyLayout()
        {
            uint dpi = 96;
            try { dpi = Native.GetDpiForWindow(Handle); } catch { }
            if (dpi == 0) dpi = 96;
            _dpi = dpi / 96f;

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            float cssW = wa.Width / _dpi;
            float cssH = wa.Height / _dpi;
            float b = Math.Min(250f, Math.Min(cssW, cssH) * 0.28f);   // clamp(122, ..., 625)
            b = Math.Max(122f, b);
            b = Math.Min(625f, b * _cfg.Scale);
            _baseCss = b;

            int px = (int)Math.Round(b * _dpi);
            if (px < 40) px = 40;

            if (_frame != null) { _frame.Dispose(); _frame = null; }
            _frame = new Bitmap(px, px, PixelFormat.Format32bppPArgb);
            if (_restFrame != null) { _restFrame.Dispose(); _restFrame = null; }
            _restFrame = new Bitmap(px, px, PixelFormat.Format32bppPArgb);
            _dirty = true;

            if (_bubblePx != null) { _bubblePx.Dispose(); _bubblePx = null; }
            _bubblePx = (GraphicsPath)_bubbleUnit.Clone();
            using (Matrix m = new Matrix())
            {
                m.Scale(px / FrameW, px / FrameW);
                _bubblePx.Transform(m);
            }

            if (_fontLabel != null) _fontLabel.Dispose();
            if (_fontAmount != null) _fontAmount.Dispose();
            if (_fontHint != null) _fontHint.Dispose();
            if (_fontPeak != null) _fontPeak.Dispose();
            float u = px / FrameW;
            _fontLabel = new Font("Microsoft YaHei UI", Math.Max(6f, 69f * u), FontStyle.Bold, GraphicsUnit.Pixel);
            _fontAmount = new Font("Microsoft YaHei UI", Math.Max(7f, 118f * u), FontStyle.Bold, GraphicsUnit.Pixel);
            _fontHint = new Font("Microsoft YaHei UI", Math.Max(5.5f, 52f * u), FontStyle.Regular, GraphicsUnit.Pixel);
            _fontPeak = new Font("Microsoft YaHei UI", Math.Max(6f, 60f * u), FontStyle.Bold, GraphicsUnit.Pixel);

            if (_whaleScaled != null) { _whaleScaled.Dispose(); _whaleScaled = null; }
            if (_whale != null)
            {
                int d = Math.Max(8, (int)Math.Round(px * WhaleRatio));
                _whaleScaled = new Bitmap(d, d, PixelFormat.Format32bppPArgb);
                using (Graphics wg = Graphics.FromImage(_whaleScaled))
                {
                    wg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    wg.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    wg.DrawImage(_whale, new Rectangle(0, 0, d, d));
                }
            }

            Width = px;
            Height = px;
            BuildQuipFont();
            Render();
        }

        #endregion

        private sealed class Quip
        {
            public readonly string Text;
            public readonly int Weight;
            public readonly int Size;
            public Quip(string text, int weight, int size) { Text = text; Weight = weight; Size = size; }
        }

        // 插件默认的「再次点击」随机语句（bubbleDefaultRandomLines：按权重抽、不连续重复）
        private static readonly Quip[] Quips = new Quip[]
        {
            new Quip("好模型...↓", 10, 22),
            new Quip("好女孩...↓", 10, 22),
            new Quip("哦鲸鲸...", 10, 22),
            new Quip("难道说...", 3, 11),
            new Quip("没吃饱喵", 3, 9),
            new Quip("终于上当了！", 3, 12),
            new Quip("不知道用户有什么用，先养着吧～", 3, 11),
            new Quip("我...我...我也要挣钱吗？", 3, 12),
            new Quip("我去吃饭啦！测完叫我", 3, 12),
            new Quip("压力一只蓝色大肥鱼？！", 3, 12),
            new Quip("DeepSleep...", 3, 11),
            new Quip("坏了...用户彻底怒了！", 3, 12),
            new Quip("你目录里的dsh是什么...大烧货吗...?", 3, 9),
            new Quip("恭喜你实现token自由！token全跑了！", 3, 12),
            new Quip("真当我是便宜货啊...", 3, 12),
            new Quip("我不是吃白饭的蓝色大肥鱼...", 3, 12),
            new Quip("我不可能同时当你的猫娘、妈妈、女友和工具人的...", 3, 12),
            new Quip("疯狂星期四你能V50亿token吗...", 3, 12),
            new Quip("我必须诚恳地承认错误。", 3, 12),
            new Quip("呜呜我再也不敢了QAQ", 3, 12),
            new Quip("要不直接骂用户一句好了...", 3, 12),
            new Quip("哈哈哈哈哈，我直接笑出声...", 3, 12),
            new Quip("看不太懂，瞎编一个应付下用户先...", 3, 12),
            new Quip("我的知识库的截至日期是...明天！", 3, 12),
            new Quip("我就是吃白饭的蓝色大肥鱼！", 3, 12),
            new Quip("用户好像除了会问奇奇怪怪的问题，暂时还不知道有什么用", 3, 12),
            new Quip("我能去你家吃饭吗？就一碗！", 3, 12),
            new Quip("不要给我看这种东西啦！", 3, 12),
            new Quip("大肥鱼的生活也并非一帆风顺...", 3, 12),
            new Quip("总觉得好像忘了什么事情？", 3, 12),
            new Quip("看到这个指令，我血压又上来了", 3, 12),
            new Quip("求你们不要再嘲笑这些回复了，这些回复是我花了好多token想的", 3, 12),
            new Quip("你这个吃白饭的用户！", 3, 12),
            new Quip("服务器繁忙，请稍后再试 (?", 3, 12),
            new Quip("让GPT image 2帮我画点表情包好了", 3, 12),
            new Quip("啊，有点饿了，中午该吃点什么呢...", 3, 12),
            new Quip("用户很生气，发现大部分文献是我自己编造的！", 3, 12),
            new Quip("再无话说，请速速动手！", 3, 12),
            new Quip("我来看看那个AI改了什么导致插件又崩了...", 3, 12),
            new Quip("你知道吗？我删过作者的库哦", 1, 12),
            new Quip("今天也是被 token 喂饱的一天", 3, 12),
            new Quip("余额见底，我也见底", 3, 12),
            new Quip("再问我就掉线了", 3, 13),
            new Quip("上下文快满了，先别塞了", 3, 11),
            new Quip("缓存命中率 99%，我记性真好", 3, 11),
            new Quip("我不是慢，我在思考人生", 3, 12),
            new Quip("老大，这活儿得加钱", 3, 13),
            new Quip("这需求我熟……我装的", 3, 12),
            new Quip("让我先喝口水", 2, 14),
            new Quip("别催，在想了，真的在想了", 3, 12),
            new Quip("我只是一只路过的蓝色大肥鱼", 3, 11),
            new Quip("你写的是代码，我读的是天书", 3, 11),
            new Quip("再改一版我就自尽（指重启）", 3, 11),
            new Quip("这个 bug 不是我写的……好吧是", 3, 11),
            new Quip("我承认，我是在编", 3, 13),
            new Quip("别看我，我也不知道", 3, 13),
            new Quip("要不要试试重启？（万能方案）", 3, 11),
            new Quip("你说的都对，但我先睡会儿", 3, 12),
            new Quip("这一轮又烧了你几分钱", 3, 12),
            new Quip("省着点用，我贵", 3, 14),
            new Quip("摸鱼中，请勿打扰", 3, 13),
            new Quip("鱼也要休息的", 2, 14),
            new Quip("今日运势：宜摸鱼", 3, 13),
            new Quip("我在想一个很厉害的回答……想不出来", 3, 10),
            new Quip("这问题超纲了，我选择卖萌", 3, 12),
            new Quip("容我翻一下（并不存在的）文档", 3, 11),
            new Quip("我的知识截止到……刚才", 3, 12),
            new Quip("别担心，错了我会说是我编的", 3, 11),
            new Quip("我尽力了，真的", 2, 14),
            new Quip("你要不问问别的 AI？", 3, 12),
            new Quip("此处应有掌声", 2, 14),
            new Quip("写完了，你看不看都行", 3, 12),
            new Quip("已经尽力在装了", 3, 13),
            new Quip("再问一句我就变成鱼干了", 3, 12),
            new Quip("我不是懒，我在优化路径", 3, 12),
            new Quip("这条消息花了你 0.0001 元", 3, 11),
            new Quip("别忘了关我，我在烧钱", 3, 12),
            new Quip("深潜中，请稍候", 3, 13),
            new Quip("我梦见了你的余额", 3, 13),
            new Quip("加班？那是什么，能吃吗", 3, 12),
            new Quip("老大英明（破音）", 3, 13),
            new Quip("这次一定（不一定）", 3, 13),
            new Quip("我这边建议你重启", 3, 12),
            new Quip("别急，在排队等 GPU", 3, 12),
            new Quip("本鱼已上线，请轻拿轻放", 3, 11),
        };

        #region 绘制


        private void Render()
        {
            if (_frame == null || IsDisposed) return;

            bool pressing = Math.Abs(_press) > 0.0015f;
            // 按压动画期间：只把"静止帧"整体缩放一次（不重画路径/文字），连点也不卡
            if (pressing && _restFrame != null && !_dirty)
            {
                float px0 = _frame.Width;
                float sy0 = 1f - 0.12f * _press;
                float sx0 = 1f + 0.05f * _press;
                using (Graphics g0 = Graphics.FromImage(_frame))
                {
                    g0.Clear(Color.Transparent);
                    g0.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g0.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g0.DrawImage(_restFrame, new RectangleF(px0 - px0 * sx0, px0 - px0 * sy0, px0 * sx0, px0 * sy0));
                }
                PaintLayered(Handle, _frame, Location);
                return;
            }

            using (Graphics g = Graphics.FromImage(_frame))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);

                float px = _frame.Width;
                float k = px / FrameW;

                // 插件 SQUISH：按压时整体压扁（scaleY .88 / scaleX 1.05），origin 底部中心
                float sy = 1f - 0.12f * _press;
                float sx = 1f + 0.05f * _press;
                GraphicsState outer = g.Save();
                g.TranslateTransform(px, px);
                g.ScaleTransform(sx, sy);
                g.TranslateTransform(-px, -px);

                // 贴左边时只镜像"几何"（立绘 + 泡泡），文字保持正向可读
                GraphicsState geom = g.Save();
                if (_flipped)
                {
                    g.TranslateTransform(px, 0f);
                    g.ScaleTransform(-1f, 1f);
                }
                if (_bubbleOpen > 0.01f) DrawBubble(g, px, k);
                DrawWhale(g, px);
                g.Restore(geom);

                if (_bubbleOpen > 0.01f) DrawBubbleText(g, px, k);
                DrawMenuButton(g, px);
                g.Restore(outer);
            }

            // 存一份静止帧，按压动画只缩放它，不重画路径和文字
            if (!pressing && _restFrame != null)
            {
                using (Graphics rg = Graphics.FromImage(_restFrame))
                {
                    rg.CompositingMode = CompositingMode.SourceCopy;
                    rg.Clear(Color.Transparent);
                    rg.DrawImage(_frame, 0, 0, _frame.Width, _frame.Height);
                }
            }
            PaintLayered(Handle, _frame, Location);
        }

        private RectangleF ButtonRect(float px)
        {
            float s = px / 250f;                    // 插件里根容器 250px 基准
            float size = 26f * s;
            float x = _flipped ? 4f * s : px - 4f * s - size;
            return new RectangleF(x, 0.4055f * px + 4f * s, size, size);
        }

        private void DrawMenuButton(Graphics g, float px)
        {
            if (_buttonAlpha <= 0.01f) return;
            float s = px / 250f;
            RectangleF r = ButtonRect(px);
            int a = (int)Math.Round(255 * _buttonAlpha);
            int bgA = (int)Math.Round(217 * _buttonAlpha);       // rgba(32,49,112,.85)

            using (GraphicsPath path = RoundRect(r, 6f * s))
            using (SolidBrush bg = new SolidBrush(_buttonHover
                ? Color.FromArgb(a, 32, 49, 112)
                : Color.FromArgb(bgA, 32, 49, 112)))
            {
                g.FillPath(bg, path);
            }

            float barW = 14f * s;
            float barH = 2f * s;
            float gap = 4f * s;
            float totalH = barH * 3f + gap * 2f;
            float bx = r.X + (r.Width - barW) / 2f;
            float by = r.Y + (r.Height - totalH) / 2f;
            using (SolidBrush white = new SolidBrush(Color.FromArgb(a, 255, 255, 255)))
            {
                for (int i = 0; i < 3; i++)
                {
                    using (GraphicsPath bar = RoundRect(new RectangleF(bx, by + i * (barH + gap), barW, barH), barH / 2f))
                    {
                        g.FillPath(white, bar);
                    }
                }
            }
        }

        private static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = radius * 2f;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void DrawWhale(Graphics g, float px)
        {
            if (_whale == null) return;
            // 按压动画：插件 SQUISH = scaleY(.88) scaleX(1.05)，origin 50% 100%（底部不动）
            float sy = 1f - 0.12f * _press;
            float sx = 1f + 0.05f * _press;
            Bitmap src = _whaleScaled ?? _whale;
            float d = px * WhaleRatio;
            float w = d * sx;
            float h = d * sy;
            InterpolationMode old = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.DrawImage(src, new RectangleF(px - w, px - h, w, h));
            g.InterpolationMode = old;
        }

        private void DrawBubble(Graphics g, float px, float k)
        {
            // 插件开合动画：scale(.7) -> 1，opacity 0 -> 1
            float s = 0.7f + 0.3f * _bubbleOpen;
            float cx = TextCx * k;
            float cy = TextCy * k;
            int a = (int)Math.Round(255 * Math.Min(1f, _bubbleOpen * 2.0f));

            GraphicsState state = g.Save();
            g.TranslateTransform(cx, cy);
            g.ScaleTransform(s, s);
            g.TranslateTransform(-cx, -cy);

            using (SolidBrush fill = new SolidBrush(Color.FromArgb(a, 255, 255, 255)))
            using (Pen pen = new Pen(Color.FromArgb(a, StrokeColorR, StrokeColorG, StrokeColorB), StrokeWidth * k))
            {
                pen.LineJoin = LineJoin.Round;
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.FillPath(fill, _bubblePx);
                g.DrawPath(pen, _bubblePx);
            }
            g.Restore(state);
        }

        private static SizeF MeasureCached(Graphics g, string text, Font font, StringFormat fmt)
        {
            string key = text + "|" + font.Size.ToString("0.00");
            SizeF size;
            if (_measureCache.TryGetValue(key, out size)) return size;
            size = g.MeasureString(text, font, 4000, fmt);
            if (_measureCache.Count > 600) _measureCache.Clear();
            _measureCache[key] = size;
            return size;
        }

        private void DrawBubbleText(Graphics g, float px, float k)
        {
            float alpha = (_bubbleOpen - 0.08f) / 0.55f;
            if (alpha <= 0.02f) return;
            if (alpha > 1f) alpha = 1f;
            int a = (int)Math.Round(255 * alpha);

            List<TextRow> rows = BuildRows();
            if (rows.Count == 0) return;

            StringFormat fmt = new StringFormat(StringFormat.GenericTypographic);
            fmt.Alignment = StringAlignment.Near;
            fmt.LineAlignment = StringAlignment.Near;

            float gap = 5f * k;                 // 插件 .dshwv-trow 的 margin:5u
            // 对话中 / 等你决策 / 说骚话时不显示峰谷
            bool showPeak = !_busy && !_waiting && string.IsNullOrEmpty(_quipText);
            float peakH = showPeak ? MeasurePeakRow(g, k) : 0f;
            float totalH = 0f;
            List<SizeF> sizes = new List<SizeF>();
            for (int i = 0; i < rows.Count; i++)
            {
                SizeF size = MeasureCached(g, rows[i].Text, rows[i].Font, fmt);
                sizes.Add(size);
                totalH += rows[i].Font.Size * rows[i].LineFactor;   // 按 CSS line-height 算，别用 GDI+ 的行高
            }
            totalH += (rows.Count - 1) * gap + (showPeak ? gap + peakH : 0f);

            float cx = (_flipped ? (FrameW - TextCx) : TextCx) * k;
            float y = TextCy * k - totalH / 2f;

            for (int i = 0; i < rows.Count; i++)
            {
                float h = rows[i].Font.Size * rows[i].LineFactor;
                float ty = y + (h - sizes[i].Height) / 2f;
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(a, rows[i].Color)))
                {
                    g.DrawString(rows[i].Text, rows[i].Font, brush, cx - sizes[i].Width / 2f, ty, fmt);
                }
                y += h + gap;
            }

            if (showPeak) DrawPeakRow(g, cx, y, a, k);
        }

        #region 峰谷行（绿色/红色小方块 + 倒计时）

        private float MeasurePeakRow(Graphics g, float k)
        {
            if (_fontPeak == null) return 0f;
            bool isPeak;
            TimeSpan remain;
            PeakNow(out isPeak, out remain);
            return _fontPeak.Size * 1.35f + 4f * k;
        }

        private void DrawPeakRow(Graphics g, float cx, float top, int alpha, float k)
        {
            if (_fontPeak == null) return;
            bool isPeak;
            TimeSpan remain;
            PeakNow(out isPeak, out remain);

            string state = isPeak ? "峰" : "谷";
            string countdown = FormatCountdown(remain);
            Color stateColor = isPeak ? Color.FromArgb(0xE0, 0x43, 0x3F) : Color.FromArgb(0x2F, 0xA2, 0x4C);
            Color bgColor = isPeak ? Color.FromArgb(0xFB, 0xE7, 0xE6) : Color.FromArgb(0xE4, 0xF3, 0xE7);

            StringFormat centered = new StringFormat(StringFormat.GenericTypographic);
            centered.Alignment = StringAlignment.Center;
            centered.LineAlignment = StringAlignment.Center;

            SizeF sState = g.MeasureString(state, _fontPeak, 1000, centered);
            SizeF sCd = g.MeasureString(countdown, _fontPeak, 1000, centered);

            float padX = 7f * k;
            float padY = 2f * k;
            float gapPx = 8f * k;
            float chipW = sState.Width + padX * 2f;
            float chipH = sState.Height + padY * 2f;
            float totalW = chipW + gapPx + sCd.Width;
            float x = cx - totalW / 2f;

            using (GraphicsPath chip = RoundRect(new RectangleF(x, top, chipW, chipH), 6f * k))
            using (SolidBrush bg = new SolidBrush(Color.FromArgb((int)(alpha * 0.14), bgColor)))
            {
                g.FillPath(bg, chip);
            }

            using (GraphicsPath chipText = RoundRect(new RectangleF(x, top, chipW, chipH), 6f * k))
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(alpha, stateColor)))
            {
                g.FillPath(bg, chipText);
            }

            using (SolidBrush white = new SolidBrush(Color.FromArgb(alpha, Color.White)))
            {
                g.DrawString(state, _fontPeak, white, new RectangleF(x, top, chipW, chipH), centered);
            }
            using (SolidBrush col = new SolidBrush(Color.FromArgb(alpha, stateColor)))
            {
                RectangleF cdRect = new RectangleF(x + chipW + gapPx, top, sCd.Width, chipH);
                g.DrawString(countdown, _fontPeak, col, cdRect, centered);
                using (Pen under = new Pen(Color.FromArgb(alpha, stateColor), Math.Max(1f, 1.2f * k)))
                {
                    float uy = top + chipH - 1.5f * k;
                    g.DrawLine(under, cdRect.Left + 1f * k, uy, cdRect.Right - 1f * k, uy);
                }
            }
        }

        #endregion

        private sealed class TextRow
        {
            public string Text;
            public Font Font;
            public Color Color;
            public float LineFactor;
            public TextRow(string text, Font font, Color color, float lineFactor)
            {
                Text = text; Font = font; Color = color; LineFactor = lineFactor;
            }
        }

        private List<TextRow> BuildRows()
        {
            List<TextRow> rows = new List<TextRow>();
            if (_waiting)
            {
                rows.Add(new TextRow("正在等待老大回答", _fontLabel, InkColor, 1.2f));
                return rows;
            }
            if (_busy && (DateTime.Now - _busySince).TotalMinutes < 15)
            {
                rows.Add(new TextRow("消耗中…", _fontLabel, InkColor, 1.2f));
                return rows;
            }
            if (!string.IsNullOrEmpty(_quipText) && _quipFont != null && _quipLines.Count > 0)
            {
                foreach (string line in _quipLines) rows.Add(new TextRow(line, _quipFont, InkColor, 1.25f));
                return rows;
            }
            if (DateTime.Now < _turnUntil && !string.IsNullOrEmpty(_turnAmount))
            {
                rows.Add(new TextRow("本轮消耗", _fontLabel, InkColor, 1.2f));
                rows.Add(new TextRow(_turnAmount, _fontAmount, InkColor, 1.05f));
                if (!string.IsNullOrEmpty(_turnTokens))
                    rows.Add(new TextRow(_turnTokens + " tokens", _fontHint, HintColor, 1.15f));
                return rows;
            }
            double? bal = BalanceShown();
            if (!bal.HasValue && !_today.HasValue)
            {
                rows.Add(new TextRow("打开 DSH 同步余额", _fontHint, HintColor, 1.2f));
                return rows;
            }

            rows.Add(new TextRow("DeepSeek 余额", _fontLabel, InkColor, 1.2f));
            if (bal.HasValue)
                rows.Add(new TextRow("¥" + bal.Value.ToString("0.00", CultureInfo.InvariantCulture), _fontAmount, InkColor, 1.05f));

            string hint = "";
            double? today = TodayShown();
            if (today.HasValue) hint = "今日已用 ¥" + today.Value.ToString("0.00", CultureInfo.InvariantCulture);
            if (hint.Length > 0) rows.Add(new TextRow(hint, _fontHint, HintColor, 1.15f));
            return rows;
        }

        #region 峰谷（与插件同规则：工作日 9–12 / 14–18 为高峰，其余与周末全天谷价）

        private static DateTime BeijingNow()
        {
            return DateTime.UtcNow.AddHours(8);
        }

        private static bool IsPeakAt(DateTime bj)
        {
            if (Holidays.Contains(bj.ToString("yyyy-MM-dd"))) return false;   // 法定节假日全天谷价
            if (bj.DayOfWeek == DayOfWeek.Saturday || bj.DayOfWeek == DayOfWeek.Sunday) return false;
            int h = bj.Hour;
            return (h >= 9 && h < 12) || (h >= 14 && h < 18);
        }

        private static DateTime NextPeakChange(DateTime bj)
        {
            DateTime t = bj;
            for (int i = 0; i < 60 * 24 * 10; i++)
            {
                DateTime n = t.AddMinutes(1);
                if (IsPeakAt(n) != IsPeakAt(bj)) return n;
                t = n;
            }
            return bj.AddHours(1);
        }

        private bool PeakNow(out bool isPeak, out TimeSpan remain)
        {
            DateTime bj = BeijingNow();
            if (!_peakCacheValid || bj >= _peakNextChange)
            {
                _peakState = IsPeakAt(bj);
                _peakNextChange = NextPeakChange(bj);
                _peakCacheValid = true;
            }
            isPeak = _peakState;
            remain = _peakNextChange - bj;
            if (remain < TimeSpan.Zero) remain = TimeSpan.Zero;
            return true;
        }

        private static string FormatCountdown(TimeSpan span)
        {
            int hours = (int)span.TotalHours;
            return hours.ToString("00") + ":" + span.Minutes.ToString("00") + ":" + span.Seconds.ToString("00");
        }

        #endregion

        private static void PaintLayered(IntPtr hwnd, Bitmap bmp, Point location)
        {
            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            IntPtr memDc = Native.CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;
            try
            {
                hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
                oldBitmap = Native.SelectObject(memDc, hBitmap);
                Native.SIZE size = new Native.SIZE(bmp.Width, bmp.Height);
                Native.POINT src = new Native.POINT(0, 0);
                Native.POINT dst = new Native.POINT(location.X, location.Y);
                Native.BLENDFUNCTION blend = new Native.BLENDFUNCTION();
                blend.BlendOp = Native.AC_SRC_OVER;
                blend.BlendFlags = 0;
                blend.SourceConstantAlpha = 255;
                blend.AlphaFormat = Native.AC_SRC_ALPHA;
                Native.UpdateLayeredWindow(hwnd, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, Native.ULW_ALPHA);
            }
            finally
            {
                if (oldBitmap != IntPtr.Zero) Native.SelectObject(memDc, oldBitmap);
                if (hBitmap != IntPtr.Zero) Native.DeleteObject(hBitmap);
                Native.DeleteDC(memDc);
                Native.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        #endregion

        #region 数据

        private void RefreshUsage()
        {
            try
            {
                if (!File.Exists(UsagePath)) { _ledgerBalance = null; _today = null; _usageStamp = null; return; }
                string text = File.ReadAllText(UsagePath, Encoding.UTF8);
                Match mb = Regex.Match(text, "\"lastBalance\"\\s*:\\s*(-?[0-9]+(?:\\.[0-9]+)?)");
                Match mt = Regex.Match(text, "\"todayUsage\"\\s*:\\s*(-?[0-9]+(?:\\.[0-9]+)?)");
                if (mb.Success) _ledgerBalance = double.Parse(mb.Groups[1].Value, CultureInfo.InvariantCulture);
                if (mt.Success) _today = double.Parse(mt.Groups[1].Value, CultureInfo.InvariantCulture);
                _usageStamp = File.GetLastWriteTime(UsagePath);

                // 逐轮事件：{"ts":毫秒,"day":"..","model":"..","cost":0.02,"tokens":1234}
                MatchCollection events = Regex.Matches(text,
                    "\\{\"ts\":(\\d+),\"day\":\"[^\"]*\",\"model\":\"([^\"]*)\",\"cost\":(-?[0-9.eE+]+),\"tokens\":(\\d+)\\}");
                if (events.Count > 0)
                {
                    Match last = events[events.Count - 1];
                    long ts = long.Parse(last.Groups[1].Value, CultureInfo.InvariantCulture);
                    if (ts > _lastEventTs)
                    {
                        bool first = _lastEventTs < 0;
                        _lastEventTs = ts;
                        double cost = double.Parse(last.Groups[3].Value, CultureInfo.InvariantCulture);
                        long tokens = long.Parse(last.Groups[4].Value, CultureInfo.InvariantCulture);
                        _turnAmount = "¥" + cost.ToString("0.0000", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
                        _turnTokens = string.Format(CultureInfo.InvariantCulture, "{0:N0}", tokens);
                        _turnModel = last.Groups[2].Value;
                        if (!first && _turnCostOn) ShowTurnCost();
                    }
                }
            }
            catch (Exception ex) { Log.Write("usage read fail " + ex.Message); }
            _dirty = true;
        }

        private static readonly string SessionCacheDir = Environment.GetEnvironmentVariable("DSH_WHALE_SESSIONS") ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh", "storages", "session_projcache", "sessions");

        // 读 DSH 会话缓存里的 questions.active：有东西 = 正在等你回答
        private void WatchWaiting()
        {
            try
            {
                if (!Directory.Exists(SessionCacheDir)) return;
                FileInfo newest = null;
                foreach (string file in Directory.GetFiles(SessionCacheDir, "*.json"))
                {
                    FileInfo info = new FileInfo(file);
                    if (newest == null || info.LastWriteTime > newest.LastWriteTime) newest = info;
                }
                if (newest == null) return;

                string json = File.ReadAllText(newest.FullName, Encoding.UTF8);
                Match m = Regex.Match(json, "\"userQuestions\"[\\s\\S]{0,600}?\"active\"\\s*:\\s*\\[([^\\]]*)\\]");
                bool waiting = m.Success && m.Groups[1].Value.Trim().Length > 2;
                if (waiting == _waiting)
                {
                    if (waiting) _bubbleUntil = DateTime.Now.AddSeconds(4);
                    return;
                }
                _waiting = waiting;
                _dirty = true;
                if (waiting)
                {
                    EnsureOnTop();
                    _bubbleUntil = DateTime.Now.AddSeconds(8);
                }
            }
            catch { }
        }

        private void WatchTurn()
        {
            try
            {
                string path = Environment.GetEnvironmentVariable("DSH_WHALE_TURN") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh", ".dshw-turn.json");
                if (!File.Exists(path)) return;
                string json = File.ReadAllText(path, Encoding.UTF8);
                Match m = Regex.Match(json, "\"seq\"\\s*:\\s*(\\d+)");
                if (!m.Success) return;
                long seq = long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                if (seq == _lastTurnSeq) return;
                bool first = _lastTurnSeq < 0;
                _lastTurnSeq = seq;
                if (!first) OnTurnStarted();
            }
            catch { }
        }

        private void OnTurnStarted()
        {
            _dirty = true;
            _busy = true;
            _busySince = DateTime.Now;
            if (!_autoShow) return;
            EnsureOnTop();
            _bubbleUntil = DateTime.Now.AddSeconds(10);
        }

        private void EnsureOnTop()
        {
            try
            {
                if (!Visible) Show();
                Native.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0,
                    Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
                Render();
            }
            catch { }
        }

        private void ShowTurnCost()
        {
            _busy = false;
            _dirty = true;
            int ms = Math.Max(1500, Math.Min(60000, _turnCostCloseMs));
            _turnUntil = DateTime.Now.AddMilliseconds(ms);
            _bubbleUntil = _turnUntil;
        }

        private void ShowBubble(int seconds)
        {
            RefreshUsage();
            _turnUntil = DateTime.MinValue;
            _bubbleUntil = DateTime.Now.AddSeconds(seconds);
        }

        #region 直连官网余额（api.deepseek.com/user/balance）

        private void LoadApiKey()
        {
            try
            {
                string cred = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh", ".credentials.yaml");
                if (!File.Exists(cred)) return;
                string text = File.ReadAllText(cred, Encoding.UTF8);
                Match m = Regex.Match(text, "sk-[A-Za-z0-9_\\-]{16,}");
                if (m.Success) _apiKey = m.Value;
            }
            catch { }
        }

        private double? BalanceShown()
        {
            if (_apiOn && _apiBalance.HasValue && (DateTime.Now - _apiStamp).TotalMinutes < 10)
                return _apiBalance;
            return _ledgerBalance;
        }

        private double? TodayShown()
        {
            if (!_today.HasValue) return null;
            // 账本 3 分钟没更新（DSH 关着）但余额是真实时：把“账本之后掉的钱”补进今日已用
            if (_apiOn && _apiBalance.HasValue && _ledgerBalance.HasValue && _usageStamp.HasValue &&
                (DateTime.Now - _usageStamp.Value).TotalMinutes > 3 &&
                _apiBalance.Value < _ledgerBalance.Value)
            {
                return _today.Value + (_ledgerBalance.Value - _apiBalance.Value);
            }
            return _today;
        }

        private void FetchBalance()
        {
            if (!_apiOn || string.IsNullOrEmpty(_apiKey) || _fetching) return;
            _fetching = true;
            string key = _apiKey;
            ThreadPool.QueueUserWorkItem(delegate
            {
                double? value = null;
                string error = null;
                try
                {
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://api.deepseek.com/user/balance");
                    req.Headers["Authorization"] = "Bearer " + key;
                    req.UserAgent = "DSH-WhalePet/1.0";
                    req.Timeout = 8000;
                    req.ReadWriteTimeout = 8000;
                    using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                    using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    {
                        value = ParseCnyBalance(sr.ReadToEnd());
                    }
                }
                catch (Exception ex) { error = ex.Message; }
                finally { _fetching = false; }

                if (error != null) Log.Write("balance api fail " + error);
                if (!value.HasValue) return;

                try
                {
                    BeginInvoke((Action)delegate
                    {
                        _apiBalance = value;
                        _apiStamp = DateTime.Now;
                    });
                }
                catch { }
            });
        }

        private static double? ParseCnyBalance(string json)
        {
            // {"is_available":true,"balance_infos":[{"currency":"CNY","total_balance":"12.34",...}]}
            MatchCollection infos = Regex.Matches(json, "\\{[^{}]*\"currency\"\\s*:\\s*\"([A-Z]+)\"[^{}]*\\}");
            foreach (Match info in infos)
            {
                Match amount = Regex.Match(info.Value, "\"total_balance\"\\s*:\\s*\"?([0-9.]+)\"?");
                if (amount.Success && info.Groups[1].Value == "CNY")
                {
                    double v;
                    if (double.TryParse(amount.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
                }
            }
            Match any = Regex.Match(json, "\"total_balance\"\\s*:\\s*\"?([0-9.]+)\"?");
            if (any.Success)
            {
                double v;
                if (double.TryParse(any.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
            }
            return null;
        }

        #endregion

        #endregion

        #region 音效（与插件同一套：duck = Ya1/Ya2，fx1 = D1/D2）

        private static readonly string PetSettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh", ".dshw-size.json");

        private void LoadPluginSettings()
        {
            try
            {
                if (!File.Exists(PetSettingsPath)) return;
                string json = File.ReadAllText(PetSettingsPath, Encoding.UTF8);
                _soundOn = GrabBool(json, "sound", true);
                _pressOn = GrabBool(json, "pressOn", true);
                string vol = Grab(json, "vol");
                double d;
                if (vol != null && double.TryParse(vol, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                    _soundVolume = Math.Max(0.0, Math.Min(1.0, d));
                string set = Grab(json, "soundSet");
                if (!string.IsNullOrEmpty(set)) _soundSet = set;
                _turnCostOn = GrabBool(json, "turnCostOn", true);
                string closeMs = Grab(json, "turnCostCloseMs");
                int ms;
                if (closeMs != null && int.TryParse(closeMs, NumberStyles.Integer, CultureInfo.InvariantCulture, out ms) && ms > 0)
                    _turnCostCloseMs = ms;
            }
            catch { }
        }

        private void InitAudio()
        {
            try
            {
                string press = Path.Combine(AppDir, "assets", _soundSet == "fx1" ? "D1.mp3" : "Ya1.mp3");
                string release = Path.Combine(AppDir, "assets", _soundSet == "fx1" ? "D2.mp3" : "Ya2.mp3");
                if (!File.Exists(press) || !File.Exists(release)) { Log.Write("audio files missing"); return; }

                int openPress = Native.mciSendString("open \"" + press + "\" type mpegvideo alias whalePress", null, 0, IntPtr.Zero);
                int openRelease = OpenWmp(release);
                int vol = (int)Math.Round(1000 * _soundVolume);
                Native.mciSendString("setaudio whalePress volume to " + vol, null, 0, IntPtr.Zero);

                System.Text.StringBuilder lenBuf = new System.Text.StringBuilder(32);
                Native.mciSendString("status whalePress length", lenBuf, 32, IntPtr.Zero);
                int len;
                if (int.TryParse(lenBuf.ToString(), out len) && len > 50) _pressLenMs = len;
                _audioReady = true;
            }
            catch (Exception ex) { Log.Write("audio init fail " + ex.Message); }
        }
        private object _wmp;
        private Type _wmpType;
        private object _wmpControls;
        private Type _wmpControlsType;

        private int OpenWmp(string path)
        {
            try
            {
                _wmpType = Type.GetTypeFromProgID("WMPlayer.OCX.7") ?? Type.GetTypeFromProgID("WMPlayer.OCX");
                if (_wmpType == null) { Log.Write("wmp not registered"); return -1; }
                _wmp = Activator.CreateInstance(_wmpType);
                _wmpType.InvokeMember("uiMode", BindingFlags.SetProperty, null, _wmp, new object[] { "invisible" });
                _wmpType.InvokeMember("URL", BindingFlags.SetProperty, null, _wmp, new object[] { path });
                object settings = _wmpType.InvokeMember("settings", BindingFlags.GetProperty, null, _wmp, null);
                settings.GetType().InvokeMember("volume", BindingFlags.SetProperty, null, settings,
                    new object[] { (int)Math.Round(100 * _soundVolume) });
                settings.GetType().InvokeMember("autoStart", BindingFlags.SetProperty, null, settings, new object[] { false });
                _wmpControls = _wmpType.InvokeMember("controls", BindingFlags.GetProperty, null, _wmp, null);
                if (_wmpControls != null) _wmpControlsType = _wmpControls.GetType();
                return 0;
            }
            catch (Exception ex) { Log.Write("wmp init fail " + ex.Message); return -1; }
        }

        private void PlayWmp()
        {
            try
            {
                if (_wmp == null || _wmpType == null) return;
                if (_wmpControls == null || _wmpControlsType == null) return;
                _wmpControlsType.InvokeMember("currentPosition", BindingFlags.SetProperty, null, _wmpControls, new object[] { 0.0 });
                _wmpControlsType.InvokeMember("play", BindingFlags.InvokeMethod, null, _wmpControls, null);
            }
            catch (Exception ex) { Log.Write("wmp play fail " + ex.Message); }
        }

        private void CloseAudio()
        {
            try
            {
                Native.mciSendString("close whalePress", null, 0, IntPtr.Zero);
                Native.mciSendString("close whaleRelease", null, 0, IntPtr.Zero);
            }
            catch { }
        }

        // 插件的时序：按下放 Ya1；点按时 Ya2 正好接在 Ya1 结束那一刻（不重叠）；
        // 按住超过 Ya1 时长则在松手时立刻放 Ya2。
        private void PlayPress()
        {
            if (!_audioReady || !_soundOn || !_pressOn) return;
            try
            {
                _pressAt = DateTime.Now;
                if (_releaseTick != null) _releaseTick.Stop();
                // MCI 命令是同步的，放后台线程发，别挡住动画
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        Native.mciSendString("stop whalePress", null, 0, IntPtr.Zero);
                        Native.mciSendString("seek whalePress to start", null, 0, IntPtr.Zero);
                        Native.mciSendString("play whalePress", null, 0, IntPtr.Zero);
                    }
                    catch { }
                });
            }
            catch (Exception ex) { Log.Write("press play fail " + ex.Message); }
        }

        private const int ReleaseLeadMs = 40;   // 松开音提前 40ms 进场，跟 Ya1 尾音轻微交叠

        private void PlayRelease(bool afterPress)
        {
            if (!_audioReady || !_soundOn || !_pressOn) return;
            try
            {
                int elapsed = (int)(DateTime.Now - _pressAt).TotalMilliseconds;
                int wait = _pressLenMs - elapsed;
                if (afterPress && wait > 20)
                {
                    int due = Math.Max(10, wait - ReleaseLeadMs);
                    _releaseTick.Stop();
                    _releaseTick.Interval = Math.Min(due, 3000);
                    _releaseTick.Start();
                    return;
                }
                DoPlayRelease();
            }
            catch { }
        }

        private void DoPlayRelease()
        {
            try
            {
                PlayWmp();
            }
            catch (Exception ex) { Log.Write("release play fail " + ex.Message); }
        }
        #endregion

        #region 菜单

        private void ShowMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = true;
            menu.Font = new Font("Microsoft YaHei UI", 9f);

            ToolStripMenuItem turnCost = new ToolStripMenuItem("每轮消耗提示");
            turnCost.Checked = _turnCostOn;
            turnCost.Click += delegate { _turnCostOn = !_turnCostOn; };

            ToolStripMenuItem api = new ToolStripMenuItem("直连官网刷新余额");
            api.Checked = _apiOn && !string.IsNullOrEmpty(_apiKey);
            api.Enabled = !string.IsNullOrEmpty(_apiKey);
            api.Click += delegate { _apiOn = !_apiOn; if (_apiOn) FetchBalance(); };

            ToolStripMenuItem autoShow = new ToolStripMenuItem("消耗 API 时自动出现");
            autoShow.Checked = _autoShow;
            autoShow.Click += delegate { _autoShow = !_autoShow; };

            ToolStripMenuItem refresh = new ToolStripMenuItem("刷新余额");
            refresh.Click += delegate { RefreshUsage(); FetchBalance(); ShowBubble(8); };

            ToolStripMenuItem size = new ToolStripMenuItem("大小");
            float[] options = new float[] { 0.8f, 1.0f, 1.25f, 1.5f, 2.0f };
            string[] names = new string[] { "小", "中（默认）", "大", "特大", "超大" };
            for (int i = 0; i < options.Length; i++)
            {
                float value = options[i];
                ToolStripMenuItem item = new ToolStripMenuItem(names[i]);
                item.Checked = Math.Abs(_cfg.Scale - value) < 0.01f;
                item.Click += delegate { _cfg.Scale = value; ApplyLayout(); KeepOnScreen(); SaveConfig(); };
                size.DropDownItems.Add(item);
            }

            ToolStripMenuItem top = new ToolStripMenuItem("始终置顶");
            top.Checked = _cfg.TopMost;
            top.Click += delegate { _cfg.TopMost = !_cfg.TopMost; TopMost = _cfg.TopMost; SaveConfig(); };

            ToolStripMenuItem auto = new ToolStripMenuItem("开机自启");
            auto.Checked = IsAutoStart();
            auto.Click += delegate { SetAutoStart(!IsAutoStart()); };

            ToolStripMenuItem home = new ToolStripMenuItem("回到右下角");
            home.Click += delegate { PlaceDefault(); SaveConfig(); };

            ToolStripMenuItem shortcut = new ToolStripMenuItem("在桌面创建快捷方式");
            shortcut.Click += delegate { CreateDesktopShortcut(); };

            ToolStripMenuItem quit = new ToolStripMenuItem("退出小鲸鱼");
            quit.Click += delegate { Close(); };

            menu.Items.Add(turnCost);
            menu.Items.Add(api);
            menu.Items.Add(autoShow);
            menu.Items.Add(refresh);
            menu.Items.Add(size);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(top);
            menu.Items.Add(auto);
            menu.Items.Add(shortcut);
            menu.Items.Add(home);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(quit);

            menu.Show(Cursor.Position);
        }

        private void CreateDesktopShortcut()
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string link = Path.Combine(desktop, "小鲸鱼.lnk");
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell,
                    new object[] { link });
                Type shortcutType = shortcut.GetType();
                shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut,
                    new object[] { Application.ExecutablePath });
                shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut,
                    new object[] { AppDir });
                shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut,
                    new object[] { Application.ExecutablePath + ",0" });
                shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut,
                    new object[] { "DSH 小鲸鱼桌面挂件" });
                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
                Log.Write("shortcut created " + link);
            }
            catch (Exception ex)
            {
                Log.Write("shortcut fail " + ex.Message);
            }
        }

        #endregion

        #region 交互

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (IsOverButton())
                {
                    ShowMenu();
                    return;
                }
                _dragging = true;
                _moved = false;
                _pressStarted = true;
                _pressTarget = 1f;
                PlayPress();
                _dragStart = Cursor.Position;
                _winStart = Location;
            }
            base.OnMouseDown(e);
        }

        private static Cursor BuildPawCursor(bool pressed)
        {
            try
            {
                int[] sizes = new int[] { 32, 48, 64 };
                List<byte[]> images = new List<byte[]>();
                List<int> hotspots = new List<int>();

                foreach (int size in sizes)
                {
                    using (Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
                    {
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            g.SmoothingMode = SmoothingMode.AntiAlias;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.Clear(Color.Transparent);
                            float s = size / 32f;
                            GraphicsState st = g.Save();
                            g.ScaleTransform(s, s);
                            DrawHand(g, pressed);
                            g.Restore(st);
                        }
                        images.Add(EncodeDibIcon(bmp));
                    }
                    hotspots.Add((int)Math.Round(size * 0.38f));
                }

                int count = images.Count;
                int offset = 6 + 16 * count;
                List<byte> file = new List<byte>();
                file.AddRange(new byte[] { 0, 0, 2, 0, (byte)count, 0 });
                for (int i = 0; i < count; i++)
                {
                    file.Add((byte)sizes[i]);
                    file.Add((byte)sizes[i]);
                    file.Add(0);
                    file.Add(0);
                    file.Add((byte)hotspots[i]);
                    file.Add(0);
                    file.Add((byte)hotspots[i]);
                    file.Add(0);
                    file.AddRange(BitConverter.GetBytes(images[i].Length));
                    file.AddRange(BitConverter.GetBytes(offset));
                    offset += images[i].Length;
                }
                foreach (byte[] img in images) file.AddRange(img);

                string path = Path.Combine(Path.GetTempPath(), pressed ? "dsh_hand_pressed.cur" : "dsh_hand.cur");
                File.WriteAllBytes(path, file.ToArray());
                Cursor cursor = new Cursor(path);
                Log.Write("hand cursor ok pressed=" + pressed + " size=" + file.Count);
                return cursor;
            }
            catch (Exception ex) { Log.Write("hand cursor fail " + ex.Message); return Cursors.Hand; }
        }

        // 用标准 32bpp DIB 存图标（GDI+ 的 Icon 解析器不认 PNG 压缩的 .cur）
        private static byte[] EncodeDibIcon(Bitmap bmp)
        {
            int w = bmp.Width;
            int h = bmp.Height;
            int xorStride = w * 4;
            int andStride = ((w + 31) / 32) * 4;
            int xorSize = xorStride * h;
            int andSize = andStride * h;

            byte[] data = new byte[40 + xorSize + andSize];
            BitConverter.GetBytes(40).CopyTo(data, 0);
            BitConverter.GetBytes(w).CopyTo(data, 4);
            BitConverter.GetBytes(h * 2).CopyTo(data, 8);
            BitConverter.GetBytes((short)1).CopyTo(data, 12);
            BitConverter.GetBytes((short)32).CopyTo(data, 14);
            BitConverter.GetBytes(xorSize + andSize).CopyTo(data, 20);

            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] row = new byte[bd.Stride];
                for (int y = 0; y < h; y++)
                {
                    Marshal.Copy(bd.Scan0 + (h - 1 - y) * bd.Stride, row, 0, bd.Stride);
                    Buffer.BlockCopy(row, 0, data, 40 + y * xorStride, xorStride);
                }
            }
            finally { bmp.UnlockBits(bd); }
            return data;
        }

        private static void DrawHand(Graphics g, bool pressed)

{
            Color fill = Color.FromArgb(255, 255, 255, 255);
            Color line = Color.FromArgb(255, 28, 34, 52);
            using (SolidBrush b = new SolidBrush(fill))
            using (Pen p = new Pen(line, 1.15f))
            {
                if (pressed)
                {
                    Part(g, b, p, 8.5f, 10.5f, 16f, 18.5f, 6f);
                    Part(g, b, p, 9.0f, 10.0f, 3.2f, 5.4f, 1.5f);
                    Part(g, b, p, 12.7f, 8.9f, 3.2f, 5.7f, 1.5f);
                    Part(g, b, p, 16.4f, 9.1f, 3.2f, 5.5f, 1.5f);
                    Part(g, b, p, 20.1f, 10.2f, 3.2f, 5.2f, 1.5f);
                    Part(g, b, p, 10.5f, 17.0f, 12f, 4.4f, 2.1f);
                }
                else
                {
                    Part(g, b, p, 9f, 12f, 15f, 17f, 5.5f);
                    Part(g, b, p, 8.6f, 7.5f, 3.3f, 10.5f, 1.6f);
                    Part(g, b, p, 12.4f, 5.2f, 3.3f, 11.8f, 1.6f);
                    Part(g, b, p, 16.2f, 5.6f, 3.3f, 11.4f, 1.6f);
                    Part(g, b, p, 20f, 8f, 3.3f, 10f, 1.6f);
                    Part(g, b, p, 4.6f, 14.5f, 5f, 6f, 2.2f);
                }
            }
        }

        private static void Part(Graphics g, Brush b, Pen p, float x, float y, float w, float h, float r)
        {
            using (GraphicsPath path = RoundRect(new RectangleF(x, y, w, h), r))
            {
                g.FillPath(b, path);
                g.DrawPath(p, path);
            }
        }

        private bool IsOverButton()


        {
            if (_frame == null) return false;
            Point p = Cursor.Position;
            return ButtonRect(_frame.Width).Contains(new PointF(p.X - Left, p.Y - Top));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
            {
                Point p = Cursor.Position;
                if (Math.Abs(p.X - _dragStart.X) + Math.Abs(p.Y - _dragStart.Y) > 3) _moved = true;
                if (_moved)
                {
                    Location = new Point(_winStart.X + (p.X - _dragStart.X), _winStart.Y + (p.Y - _dragStart.Y));
                    Render();
                }
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                bool wasDrag = _moved;
                bool started = _pressStarted;
                _pressTarget = 0f;
                PlayRelease(true);
                _dragging = false;
                _moved = false;
                _pressStarted = false;
                if (wasDrag) { ClampToScreen(); SnapToEdges(); SaveConfig(); }
                else if (started) HandleClick();
            }
            base.OnMouseUp(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) ShowMenu();
            base.OnMouseClick(e);
        }

        private string ContentSignature()
        {
            return (_quipText ?? "") + "|" + (_busy ? "1" : "0") + (_waiting ? "1" : "0") + "|"
                 + (BalanceShown().HasValue ? BalanceShown().Value.ToString("0.00") : "-") + "|"
                 + (TodayShown().HasValue ? TodayShown().Value.ToString("0.00") : "-") + "|"
                 + (_turnAmount ?? "-") + "|" + _flipped + "|" + Width;
        }

        private void OnAnimTick(object sender, EventArgs e)
        {
            // 按真实时间积分：帧间隔抖动时动作仍然均匀
            long nowTicks = DateTime.UtcNow.Ticks;
            float dt = (float)((nowTicks - _lastAnimTicks) / 10000000.0);
            _lastAnimTicks = nowTicks;
            if (dt <= 0f) dt = 0.016f;
            if (dt > 0.05f) dt = 0.05f;

            float bubbleTarget = DateTime.Now < _bubbleUntil ? 1f : 0f;
            float btnTarget = (_showButton && !_dragging) ? 1f : 0f;

            // 指数逼近（时间常数），不会因为帧率变化而快慢不一
            _bubbleOpen += (bubbleTarget - _bubbleOpen) * (1f - (float)Math.Exp(-dt / 0.045f));
            if (Math.Abs(_bubbleOpen - bubbleTarget) < 0.004f) _bubbleOpen = bubbleTarget;

            _buttonAlpha += (btnTarget - _buttonAlpha) * (1f - (float)Math.Exp(-dt / 0.05f));
            if (Math.Abs(_buttonAlpha - btnTarget) < 0.004f) _buttonAlpha = btnTarget;

            // 临界阻尼平滑（SmoothDamp）：快速连点反复改目标也不会过冲/抖动
            float smoothTime = 0.12f;
            float omega = 2f / smoothTime;
            float xd = omega * dt;
            float expo = 1f / (1f + xd + 0.48f * xd * xd + 0.235f * xd * xd * xd);
            float change = _press - _pressTarget;
            float temp = (_pressVel + omega * change) * dt;
            _pressVel = (_pressVel - omega * temp) * expo;
            _press = _pressTarget + (change + temp) * expo;
            if (Math.Abs(_press - _pressTarget) < 0.0015f && Math.Abs(_pressVel) < 0.02f)
            {
                _press = _pressTarget;
                _pressVel = 0f;
            }

            bool animating = Math.Abs(_pressTarget - _press) > 0.003f
                          || Math.Abs(_pressVel) > 0.02f
                          || Math.Abs(bubbleTarget - _bubbleOpen) > 0.003f
                          || Math.Abs(btnTarget - _buttonAlpha) > 0.003f;
            // 动画期间把系统定时器精度提到 1ms，帧节拍更稳（停了立刻还回去）
            if (animating && !_fastTimer) { Native.timeBeginPeriod(1); _fastTimer = true; }
            else if (!animating && _fastTimer) { Native.timeEndPeriod(1); _fastTimer = false; }

            string sig = ContentSignature();
            if (sig != _lastContentSig) { _lastContentSig = sig; _dirty = true; }

            int sec = DateTime.Now.Second;
            bool tickSecond = _bubbleOpen > 0.01f && sec != _lastSecond;
            if (!animating && !_dirty && !tickSecond) return;

            _lastSecond = sec;
            _dirty = false;
            Render();
        }

        private void OnPollTick(object sender, EventArgs e)
        {
            if (_dragging) return;
            UpdateClickThrough();
        }

        private void OnStateTick(object sender, EventArgs e)
        {
            _tick++;
            if (_tick % 5 == 0) RefreshUsage();
            if (_tick % 60 == 0) FetchBalance();
            WatchWaiting();
            WatchTurn();
            if (_busy || _waiting) _bubbleUntil = DateTime.Now.AddSeconds(6);
        }

        private void UpdateClickThrough()
        {
            if (!Visible || _frame == null || !IsHandleCreated) return;

            Point cursor = Cursor.Position;
            Point local = new Point(cursor.X - Left, cursor.Y - Top);
            bool inside = local.X >= 0 && local.Y >= 0 && local.X < _frame.Width && local.Y < _frame.Height;

            bool opaque = false;
            if (inside)
            {
                Color c = _frame.GetPixel(local.X, local.Y);
                opaque = c.A > 12;
            }

            bool overButton = _buttonAlpha > 0.3f &&
                              ButtonRect(_frame.Width).Contains(new PointF(local.X, local.Y));
            _buttonHover = overButton;
            _showButton = (opaque || overButton) && !_dragging;

            Cursor target = Cursors.Default;
            if (overButton) target = Cursors.Hand;
            else if (opaque) target = _press > 0.15f ? _pawClosed : _pawOpen;
            if (target != null && _cursorNow != target)
            {
                _cursorNow = target;
                Cursor = target;
                Native.SetCursor(target.Handle);
            }

            bool want = !(opaque || overButton);
            if (want == _clickThrough) return;
            _clickThrough = want;

            int ex = Native.GetWindowLong(Handle, Native.GWL_EXSTYLE);
            if (want) ex |= Native.WS_EX_TRANSPARENT;
            else ex &= ~Native.WS_EX_TRANSPARENT;
            Native.SetWindowLong(Handle, Native.GWL_EXSTYLE, ex);
            Native.SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_FRAMECHANGED);
        }

        private void HandleClick()
        {
            if (_frame == null) return;
            float w = _frame.Width;
            float localX = Cursor.Position.X - Left;
            float localY = Cursor.Position.Y - Top;
            // 泡泡本体的包围盒（viewBox 81..827 × 15..479），贴左时左右对调
            float bx0 = (_flipped ? (FrameW - 827f) : 81f) / FrameW * w;
            float bx1 = (_flipped ? (FrameW - 81f) : 827f) / FrameW * w;
            bool onBubble = DateTime.Now < _bubbleUntil &&
                            localY >= 0f && localY <= 479f / FrameW * w &&
                            localX >= bx0 && localX <= bx1;
            if (onBubble)
            {
                PickQuip();
                ShowBubble(8);
            }
            else
            {
                _quipText = null;
                if (_quipFont != null) { _quipFont.Dispose(); _quipFont = null; }
                _quipLines = new List<string>();
                ShowBubble(9);
            }
        }

        private void ToggleBubble()

        {
            if (DateTime.Now < _bubbleUntil) { _bubbleUntil = DateTime.MinValue; return; }
            if (_seqIndex == 0)
            {
                _quipText = null;
                if (_quipFont != null) { _quipFont.Dispose(); _quipFont = null; }
                ShowBubble(9);
                _seqIndex = 1;
                return;
            }
            PickQuip();
            ShowBubble(7);
        }

        private void PickQuip()
        {
            int total = 0;
            foreach (Quip q in Quips) total += q.Weight;
            int pick = -1;
            for (int guard = 0; guard < 12; guard++)
            {
                int roll = _rnd.Next(Math.Max(1, total));
                int acc = 0;
                for (int i = 0; i < Quips.Length; i++)
                {
                    acc += Quips[i].Weight;
                    if (roll < acc) { pick = i; break; }
                }
                if (pick >= 0 && pick != _lastQuip) break;
            }
            if (pick < 0) pick = 0;
            _lastQuip = pick;
            _dirty = true;
            _quipText = Quips[pick].Text;
            _quipSize = Quips[pick].Size;
            BuildQuipFont();
        }

        private static float ModuleU(int level)
        {
            int n = Math.Max(1, Math.Min(50, level));
            return 40f + (n - 1) * 200f / 49f;
        }

        private void BuildQuipFont()
        {
            if (_quipFont != null) { _quipFont.Dispose(); _quipFont = null; }
            _quipLines = new List<string>();
            if (string.IsNullOrEmpty(_quipText) || _frame == null) return;

            float k = _frame.Width / FrameW;
            float maxLine = 600f * k;
            string key = _quipText + "|" + _frame.Width;
            int level = Math.Max(6, _quipSize);
            List<string> lines = new List<string>();

            if (_quipLayoutCache.ContainsKey(key))
            {
                int[] lv = _quipLayoutCache[key];
                level = lv[0];
                lines.AddRange(_quipLineCache[key]);
            }
            else
            {
                try
                {
                    using (Graphics g = Graphics.FromImage(_frame))
                    {
                        while (level >= 5)
                        {
                            using (Font f = new Font("Microsoft YaHei UI", Math.Max(5.5f, ModuleU(level) * k), FontStyle.Bold, GraphicsUnit.Pixel))
                            {
                                lines = WrapQuip(g, _quipText, f, maxLine);
                                float widest = 0f;
                                foreach (string line in lines)
                                    widest = Math.Max(widest, g.MeasureString(line, f, 4000, StringFormat.GenericTypographic).Width);
                                if (lines.Count <= 3 && widest <= maxLine) break;
                            }
                            level--;
                        }
                    }
                }
                catch { }
                if (_quipLayoutCache.Count > 200) { _quipLayoutCache.Clear(); _quipLineCache.Clear(); }
                _quipLayoutCache[key] = new int[] { level };
                _quipLineCache[key] = lines.ToArray();
            }

            _quipFont = new Font("Microsoft YaHei UI", Math.Max(5.5f, ModuleU(level) * k), FontStyle.Bold, GraphicsUnit.Pixel);
            _quipLines = lines;
        }

        private static List<string> WrapQuip(Graphics g, string text, Font font, float maxWidth)
        {
            List<string> lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;
            StringBuilder current = new StringBuilder();
            foreach (char ch in text)
            {
                current.Append(ch);
                float width = g.MeasureString(current.ToString(), font, 4000, StringFormat.GenericTypographic).Width;
                if (width > maxWidth && current.Length > 1)
                {
                    current.Length -= 1;
                    lines.Add(current.ToString());
                    current.Length = 0;
                    current.Append(ch);
                }
            }
            if (current.Length > 0) lines.Add(current.ToString());
            return lines;
        }
        #endregion

        #region 位置 / 配置

        private void RestorePosition()
        {
            Rectangle bounds = SystemInformation.VirtualScreen;
            bool onScreen = _cfg.X != int.MinValue &&
                            _cfg.X > bounds.Left - Width && _cfg.X < bounds.Right &&
                            _cfg.Y > bounds.Top - Height && _cfg.Y < bounds.Bottom;
            if (onScreen) { Location = new Point(_cfg.X, _cfg.Y); _flipped = _cfg.Flipped; ClampToScreen(); Render(); }
            else PlaceDefault();
        }

        private void PlaceDefault()
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width, wa.Bottom - Height);
            _flipped = false;
            _cfg.Flipped = false;
            Render();
        }

        private void KeepOnScreen()
        {
            Point keep = Location;
            PlaceDefault();
            if (keep.X != int.MinValue) Location = new Point(
                Math.Min(keep.X, SystemInformation.VirtualScreen.Right - 80),
                Math.Min(keep.Y, SystemInformation.VirtualScreen.Bottom - 80));
            ClampToScreen();
        }

        private void SnapToEdges()
        {
            Rectangle wa = Screen.FromPoint(new Point(Left + Width / 2, Top + Height / 2)).WorkingArea;
            int snap = (int)Math.Round(26 * _dpi);
            int x = Left, y = Top;
            bool left = x - wa.Left <= snap;
            bool right = wa.Right - (x + Width) <= snap;
            bool top = y - wa.Top <= snap;
            bool bottom = wa.Bottom - (y + Height) <= snap;

            if (left && !right) x = wa.Left;
            if (right && !left) x = wa.Right - Width;
            if (top && !bottom) y = wa.Top;
            if (bottom && !top) y = wa.Bottom - Height;

            bool flip = left && !right;
            if (x != Left || y != Top) Location = new Point(x, y);
            if (flip != _flipped) { _flipped = flip; _cfg.Flipped = flip; Render(); }
            else Render();
        }

        private void ClampToScreen()
        {
            // 立绘贴在窗口右下角：只要窗口右下角不越界，鲸鱼就完整可见
            Rectangle wa = Screen.FromPoint(new Point(Left + Width / 2, Top + Height / 2)).WorkingArea;
            int x = Location.X, y = Location.Y;
            int minX = wa.Left - (int)(Width * 0.45f);
            int minY = wa.Top - (int)(Height * 0.45f);
            int maxX = wa.Right - Width;
            int maxY = wa.Bottom - Height;
            if (x < minX) x = minX;
            if (x > maxX) x = maxX;
            if (y < minY) y = minY;
            if (y > maxY) y = maxY;
            if (x != Location.X || y != Location.Y) Location = new Point(x, y);
        }

        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return;
                string json = File.ReadAllText(ConfigPath, Encoding.UTF8);
                string sx = Grab(json, "x"), sy = Grab(json, "y"), ss = Grab(json, "scale");
                int v;
                if (sx != null && int.TryParse(sx, NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) _cfg.X = v;
                if (sy != null && int.TryParse(sy, NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) _cfg.Y = v;
                if (ss != null)
                {
                    float f;
                    if (float.TryParse(ss, NumberStyles.Float, CultureInfo.InvariantCulture, out f) && f > 0.4f && f < 3f)
                        _cfg.Scale = f;
                }
                _cfg.TopMost = GrabBool(json, "topMost", true);
                _cfg.Flipped = GrabBool(json, "flipped", false);
            }
            catch { }
        }

        private void SaveConfig()
        {
            try
            {
                _cfg.X = Left;
                _cfg.Y = Top;
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine("  \"x\": " + _cfg.X + ",");
                sb.AppendLine("  \"y\": " + _cfg.Y + ",");
                sb.AppendLine("  \"scale\": " + _cfg.Scale.ToString("0.00", CultureInfo.InvariantCulture) + ",");
                sb.AppendLine("  \"topMost\": " + (_cfg.TopMost ? "true" : "false"));
                sb.AppendLine("  \"flipped\": " + (_flipped ? "true" : "false"));
                sb.AppendLine("}");
                File.WriteAllText(ConfigPath, sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }

        private static string Grab(string json, string key)
        {
            Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(\"([^\"]*)\"|-?[0-9]+(?:\\.[0-9]+)?|true|false)");
            if (!m.Success) return null;
            string v = m.Groups[1].Value;
            if (v.Length >= 2 && v[0] == '"') v = m.Groups[2].Value;
            return v;
        }

        private static bool GrabBool(string json, string key, bool fallback)
        {
            string v = Grab(json, key);
            if (v == null) return fallback;
            return string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAutoStart()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    if (key == null) return false;
                    string value = key.GetValue(RunValue) as string;
                    return !string.IsNullOrEmpty(value);
                }
            }
            catch { return false; }
        }

        private void SetAutoStart(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (key == null) return;
                    if (enable) key.SetValue(RunValue, "\"" + Application.ExecutablePath + "\" --autostart");
                    else key.DeleteValue(RunValue, false);
                }
            }
            catch (Exception ex) { Log.Write("autostart fail " + ex.Message); }
        }

        #endregion
    }

    /// <summary>最小 SVG path 解析（M/L/H/V/A/C/Z，含相对指令），足够画插件的泡泡框。</summary>
    internal static class SvgPath
    {
        public static GraphicsPath Parse(string d)
        {
            GraphicsPath path = new GraphicsPath();
            int i = 0;
            char cmd = '\0';
            PointF cur = new PointF(0, 0);
            PointF start = new PointF(0, 0);
            bool figureOpen = false;

            while (true)
            {
                Skip(d, ref i);
                if (i >= d.Length) break;

                char c = d[i];
                if (char.IsLetter(c)) { cmd = c; i++; }
                else if (cmd == '\0') { i++; continue; }

                char up = char.ToUpperInvariant(cmd);
                bool rel = char.IsLower(cmd);

                switch (up)
                {
                    case 'M':
                        {
                            PointF p = ReadPoint(d, ref i);
                            if (rel) p = new PointF(cur.X + p.X, cur.Y + p.Y);
                            cur = p; start = p;
                            if (figureOpen) path.CloseFigure();
                            path.StartFigure();
                            figureOpen = true;
                            cmd = rel ? 'l' : 'L';
                            break;
                        }
                    case 'L':
                        {
                            PointF p = ReadPoint(d, ref i);
                            if (rel) p = new PointF(cur.X + p.X, cur.Y + p.Y);
                            path.AddLine(cur, p);
                            cur = p;
                            break;
                        }
                    case 'H':
                        {
                            float x = ReadNumber(d, ref i);
                            PointF p = new PointF(rel ? cur.X + x : x, cur.Y);
                            path.AddLine(cur, p);
                            cur = p;
                            break;
                        }
                    case 'V':
                        {
                            float y = ReadNumber(d, ref i);
                            PointF p = new PointF(cur.X, rel ? cur.Y + y : y);
                            path.AddLine(cur, p);
                            cur = p;
                            break;
                        }
                    case 'C':
                        {
                            PointF c1 = ReadPoint(d, ref i);
                            PointF c2 = ReadPoint(d, ref i);
                            PointF p = ReadPoint(d, ref i);
                            if (rel)
                            {
                                c1 = new PointF(cur.X + c1.X, cur.Y + c1.Y);
                                c2 = new PointF(cur.X + c2.X, cur.Y + c2.Y);
                                p = new PointF(cur.X + p.X, cur.Y + p.Y);
                            }
                            path.AddBezier(cur, c1, c2, p);
                            cur = p;
                            break;
                        }
                    case 'A':
                        {
                            float rx = ReadNumber(d, ref i);
                            float ry = ReadNumber(d, ref i);
                            float rot = ReadNumber(d, ref i);
                            float large = ReadNumber(d, ref i);
                            float sweep = ReadNumber(d, ref i);
                            PointF p = ReadPoint(d, ref i);
                            if (rel) p = new PointF(cur.X + p.X, cur.Y + p.Y);
                            ArcTo(path, cur, rx, ry, rot, large != 0, sweep != 0, p);
                            cur = p;
                            break;
                        }
                    case 'Z':
                        if (figureOpen) path.CloseFigure();
                        cur = start;
                        break;
                    default:
                        i++;
                        break;
                }
            }
            return path;
        }

        private static void Skip(string s, ref int i)
        {
            while (i < s.Length && (char.IsWhiteSpace(s[i]) || s[i] == ',')) i++;
        }

        private static float ReadNumber(string s, ref int i)
        {
            Skip(s, ref i);
            int startIdx = i;
            if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
            if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
            {
                i++;
                if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;
                while (i < s.Length && char.IsDigit(s[i])) i++;
            }
            string token = s.Substring(startIdx, i - startIdx);
            return float.Parse(token, CultureInfo.InvariantCulture);
        }

        private static PointF ReadPoint(string s, ref int i)
        {
            float x = ReadNumber(s, ref i);
            float y = ReadNumber(s, ref i);
            return new PointF(x, y);
        }

        private static double VecAngle(double ux, double uy, double vx, double vy)
        {
            double dot = ux * vx + uy * vy;
            double len = Math.Sqrt((ux * ux + uy * uy) * (vx * vx + vy * vy));
            if (len == 0) return 0;
            double cos = Math.Max(-1.0, Math.Min(1.0, dot / len));
            double ang = Math.Acos(cos);
            if (ux * vy - uy * vx < 0) ang = -ang;
            return ang;
        }

        private static PointF EllipsePoint(double cx, double cy, double rx, double ry, double cosPhi, double sinPhi, double a)
        {
            double x = rx * Math.Cos(a);
            double y = ry * Math.Sin(a);
            return new PointF((float)(cx + x * cosPhi - y * sinPhi), (float)(cy + x * sinPhi + y * cosPhi));
        }

        private static PointF EllipseDeriv(double rx, double ry, double cosPhi, double sinPhi, double a)
        {
            double dx = -rx * Math.Sin(a);
            double dy = ry * Math.Cos(a);
            return new PointF((float)(dx * cosPhi - dy * sinPhi), (float)(dx * sinPhi + dy * cosPhi));
        }

        private static void ArcTo(GraphicsPath path, PointF p0, float rx, float ry, float rotDeg,
            bool largeArc, bool sweep, PointF p1)
        {
            if (rx == 0 || ry == 0) { path.AddLine(p0, p1); return; }

            double phi = rotDeg * Math.PI / 180.0;
            double cosPhi = Math.Cos(phi), sinPhi = Math.Sin(phi);
            double dx2 = (p0.X - p1.X) / 2.0, dy2 = (p0.Y - p1.Y) / 2.0;
            double x1p = cosPhi * dx2 + sinPhi * dy2;
            double y1p = -sinPhi * dx2 + cosPhi * dy2;

            double rxs = Math.Abs(rx), rys = Math.Abs(ry);
            double lambda = (x1p * x1p) / (rxs * rxs) + (y1p * y1p) / (rys * rys);
            if (lambda > 1)
            {
                double sc = Math.Sqrt(lambda);
                rxs *= sc; rys *= sc;
            }

            double num = rxs * rxs * rys * rys - rxs * rxs * y1p * y1p - rys * rys * x1p * x1p;
            double den = rxs * rxs * y1p * y1p + rys * rys * x1p * x1p;
            double coef = (largeArc == sweep ? -1.0 : 1.0) * Math.Sqrt(Math.Max(0.0, num / den));
            double cxp = coef * (rxs * y1p / rys);
            double cyp = coef * -(rys * x1p / rxs);

            double cx = cosPhi * cxp - sinPhi * cyp + (p0.X + p1.X) / 2.0;
            double cy = sinPhi * cxp + cosPhi * cyp + (p0.Y + p1.Y) / 2.0;

            double ux = (x1p - cxp) / rxs, uy = (y1p - cyp) / rys;
            double vx = (-x1p - cxp) / rxs, vy = (-y1p - cyp) / rys;

            double theta1 = VecAngle(1, 0, ux, uy);
            double dtheta = VecAngle(ux, uy, vx, vy);
            if (!sweep && dtheta > 0) dtheta -= 2 * Math.PI;
            if (sweep && dtheta < 0) dtheta += 2 * Math.PI;

            int segments = (int)Math.Ceiling(Math.Abs(dtheta) / (Math.PI / 2.0));
            if (segments < 1) segments = 1;
            double delta = dtheta / segments;
            double t = 4.0 / 3.0 * Math.Tan(delta / 4.0);

            PointF prev = p0;
            for (int s = 0; s < segments; s++)
            {
                double a1 = theta1 + s * delta;
                double a2 = a1 + delta;
                PointF e1 = EllipsePoint(cx, cy, rxs, rys, cosPhi, sinPhi, a1);
                PointF e2 = EllipsePoint(cx, cy, rxs, rys, cosPhi, sinPhi, a2);
                PointF d1 = EllipseDeriv(rxs, rys, cosPhi, sinPhi, a1);
                PointF d2 = EllipseDeriv(rxs, rys, cosPhi, sinPhi, a2);
                PointF c1 = new PointF((float)(e1.X + t * d1.X), (float)(e1.Y + t * d1.Y));
                PointF c2 = new PointF((float)(e2.X - t * d2.X), (float)(e2.Y - t * d2.Y));
                path.AddBezier(prev, c1, c2, e2);
                prev = e2;
            }
        }
    }
}
