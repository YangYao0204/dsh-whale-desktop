using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

// Claude Code hooks 的信号助手:UserPromptSubmit -> start,Stop -> stop。
// 写 ~/.dsh/.dshw-turn.json {seq, busy, updatedAt},UTF-8 无 BOM。
// start 时若鲸鱼没在跑,直接拉起,保证"一消耗 API 就出现"。
class WhaleSignal
{
    const string MutexName = @"Local\DSH_WhalePet_Desktop";
    const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunValueName = "DSHWhalePet";
    static readonly string FallbackExe = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Documents", "deepseek-harness", "default-workspace", "desktop-whale", "WhalePet.exe");

    static int Main(string[] args)
    {
        try
        {
            bool start = args.Length > 0 && args[0] == "start";
            string f = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".dsh", ".dshw-turn.json");
            if (!start && !File.Exists(f)) return 0;

            long seq = 0;
            if (File.Exists(f))
            {
                string text = File.ReadAllText(f, Encoding.UTF8);
                Match m = Regex.Match(text, "\"seq\"\\s*:\\s*(\\d+)");
                if (m.Success) long.TryParse(m.Groups[1].Value, out seq);
            }
            if (start) seq++;

            string json = "{\"seq\":" + seq + ",\"busy\":" + (start ? "true" : "false")
                + ",\"updatedAt\":\"" + DateTime.UtcNow.ToString("o") + "\"}";
            File.WriteAllText(f, json, new UTF8Encoding(false));

            if (start) EnsureWhaleRunning();
            return 0;
        }
        catch { return 1; }
    }

    static bool WhaleRunning()
    {
        try
        {
            Mutex m;
            return Mutex.TryOpenExisting(MutexName, out m);
        }
        catch { return false; }
    }

    // 鲸鱼路径优先从自启注册表读(它第一次跑会登记),找不到再回退硬编码。
    static string FindWhaleExe()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath))
            {
                string v = key != null ? key.GetValue(RunValueName) as string : null;
                if (!string.IsNullOrEmpty(v))
                {
                    Match m = Regex.Match(v, "\"([^\"]+)\"");
                    if (m.Success && File.Exists(m.Groups[1].Value)) return m.Groups[1].Value;
                }
            }
        }
        catch { }
        return File.Exists(FallbackExe) ? FallbackExe : null;
    }

    static void EnsureWhaleRunning()
    {
        if (WhaleRunning()) return;
        string exe = FindWhaleExe();
        if (exe == null) return;
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(exe);
            psi.UseShellExecute = false;
            psi.WorkingDirectory = Path.GetDirectoryName(exe);
            System.Diagnostics.Process.Start(psi);
        }
        catch { }
    }
}
