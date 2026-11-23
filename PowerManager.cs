using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace PowerModeSwitcher;

public static class PowerManager
{
    private const uint ACCESS_SCHEME = 16;

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern uint PowerSetActiveScheme(IntPtr UserRootPowerKey, ref Guid SchemeGuid);

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern uint PowerGetActiveScheme(IntPtr UserRootPowerKey, out IntPtr ActivePolicyGuid);

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern uint PowerEnumerate(
        IntPtr RootPowerKey, IntPtr SchemeGuid, IntPtr SubGroupOfPowerSettingsGuid,
        uint AccessFlags, uint Index, ref Guid Buffer, ref uint BufferSize);

    [DllImport("powrprof.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint PowerReadFriendlyName(
        IntPtr RootPowerKey, ref Guid SchemeGuid, IntPtr SubGroupOfPowerSettingsGuid,
        IntPtr PowerSettingGuid, StringBuilder? Buffer, ref uint BufferSize);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    public static Dictionary<Guid, string> GetAllPowerPlans()
    {
        var plans = new Dictionary<Guid, string>();
        uint index = 0;

        while (true)
        {
            Guid schemeGuid = Guid.Empty;
            uint size = (uint)Marshal.SizeOf(typeof(Guid));
            uint res = PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ACCESS_SCHEME, index, ref schemeGuid, ref size);
            if (res != 0) break;

            uint nameSize = 0;
            PowerReadFriendlyName(IntPtr.Zero, ref schemeGuid, IntPtr.Zero, IntPtr.Zero, null, ref nameSize);
            if (nameSize > 0)
            {
                var sb = new StringBuilder((int)nameSize / 2);
                PowerReadFriendlyName(IntPtr.Zero, ref schemeGuid, IntPtr.Zero, IntPtr.Zero, sb, ref nameSize);
                plans[schemeGuid] = sb.ToString();
            }
            index++;
        }
        return plans;
    }

    public static Guid GetActivePlanGuid()
    {
        if (PowerGetActiveScheme(IntPtr.Zero, out IntPtr ptr) == 0 && ptr != IntPtr.Zero)
        {
            Guid active = Marshal.PtrToStructure<Guid>(ptr);
            LocalFree(ptr);
            return active;
        }
        return Guid.Empty;
    }

    public static void ApplyMode(ModeProfile mode, string afterburnerPath)
    {
        if (Guid.TryParse(mode.PowerPlanGuid, out Guid guid) && guid != Guid.Empty)
        {
            PowerSetActiveScheme(IntPtr.Zero, ref guid);
        }

        if (mode.AfterburnerProfile is >= 1 and <= 5 && File.Exists(afterburnerPath))
        {
            var startInfo = new ProcessStartInfo();
            startInfo.FileName = afterburnerPath;
            startInfo.Arguments = $"-Profile{mode.AfterburnerProfile}";
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            Process.Start(startInfo);
        }
    }

    public static (string AfkGuid, string CasualGuid, string PerfGuid) CreateRecommendedPlans()
    {
        var existing = GetAllPowerPlans();

        string afk = FindOrCreatePlan(existing, "PMS - AFK / Macro", "a1841308-3541-4fab-bc81-f71556f20b4a", guid =>
        {
            RunPowerCfg($"/setacvalueindex {guid} SUB_PROCESSOR PROCTHROTTLEMIN 5");
            RunPowerCfg($"/setacvalueindex {guid} SUB_PROCESSOR PROCTHROTTLEMAX 40");
            RunPowerCfg($"/setacvalueindex {guid} SUB_VIDEO VIDEOIDLE 300");
            RunPowerCfg($"/setacvalueindex {guid} SUB_SLEEP STANDBYIDLE 0");
        });

        string casual = FindOrCreatePlan(existing, "PMS - Casual", "381b4222-f694-41f0-9685-ff5bb260df2e", guid =>
        {
            RunPowerCfg($"/setacvalueindex {guid} SUB_PROCESSOR PROCTHROTTLEMIN 5");
            RunPowerCfg($"/setacvalueindex {guid} SUB_PROCESSOR PROCTHROTTLEMAX 100");
            RunPowerCfg($"/setacvalueindex {guid} SUB_VIDEO VIDEOIDLE 900");
            RunPowerCfg($"/setacvalueindex {guid} SUB_SLEEP STANDBYIDLE 0");
        });

        string perf = FindOrCreatePlan(existing, "PMS - Performance", "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", guid =>
        {
            RunPowerCfg($"/setacvalueindex {guid} SUB_PROCESSOR PROCTHROTTLEMIN 100");
            RunPowerCfg($"/setacvalueindex {guid} SUB_PROCESSOR PROCTHROTTLEMAX 100");
            RunPowerCfg($"/setacvalueindex {guid} SUB_PCIEXPRESS ASPM 0");
            RunPowerCfg($"/setacvalueindex {guid} SUB_SLEEP STANDBYIDLE 0");
        });

        return (afk, casual, perf);
    }

    public static bool CreateAfterburnerProfiles(string afterburnerExePath)
    {
        string? abDir = Path.GetDirectoryName(afterburnerExePath);
        if (string.IsNullOrEmpty(abDir)) return false;

        string profilesDir = Path.Combine(abDir, "Profiles");
        if (!Directory.Exists(profilesDir)) return false;

        string? gpuCfg = Directory.GetFiles(profilesDir, "VEN_*.cfg").FirstOrDefault();
        if (gpuCfg == null) return false;

        bool wasRunning = Process.GetProcessesByName("MSIAfterburner").Length > 0;
        foreach (var proc in Process.GetProcessesByName("MSIAfterburner"))
        {
            proc.Kill();
            proc.WaitForExit(3000);
        }

        File.Copy(gpuCfg, gpuCfg + ".bak", overwrite: true);

        var lines = File.ReadAllLines(gpuCfg).ToList();
        SetIniValue(lines, "Profile1", "PowerLimit", "100");
        SetIniValue(lines, "Profile2", "PowerLimit", "60");
        SetIniValue(lines, "Profile3", "PowerLimit", "100");
        File.WriteAllLines(gpuCfg, lines);

        for (int i = 1; i <= 3; i++)
        {
            string slotFile = Path.Combine(profilesDir, $"Profile{i}.cfg");
            if (!File.Exists(slotFile))
            {
                File.WriteAllText(slotFile, "[Settings]\r\nProfileContents=1\r\n");
            }
        }

        if (wasRunning && File.Exists(afterburnerExePath))
        {
            var startInfo = new ProcessStartInfo();
            startInfo.FileName = afterburnerExePath;
            startInfo.UseShellExecute = true;
            Process.Start(startInfo);
        }

        return true;
    }

    private static void SetIniValue(List<string> lines, string section, string key, string value)
    {
        string header = $"[{section}]";
        int secIdx = lines.FindIndex(l => l.Trim().Equals(header, StringComparison.OrdinalIgnoreCase));

        if (secIdx == -1)
        {
            lines.Add("");
            lines.Add(header);
            lines.Add($"{key}={value}");
            return;
        }

        for (int i = secIdx + 1; i < lines.Count; i++)
        {
            string trimmed = lines[i].Trim();
            if (trimmed.StartsWith("[")) break;
            if (trimmed.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = $"{key}={value}";
                return;
            }
        }

        lines.Insert(secIdx + 1, $"{key}={value}");
    }

    private static string FindOrCreatePlan(Dictionary<Guid, string> existing, string planName, string baseSchemeGuid, Action<string> configure)
    {
        var match = existing.FirstOrDefault(kv => string.Equals(kv.Value, planName, StringComparison.OrdinalIgnoreCase));
        if (match.Key != Guid.Empty) return match.Key.ToString();

        string output = RunPowerCfg($"/duplicatescheme {baseSchemeGuid}");
        var m = Regex.Match(output, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
        if (!m.Success) return "";

        string newGuid = m.Value;
        RunPowerCfg($"/changename {newGuid} \"{planName}\"");
        configure(newGuid);
        return newGuid;
    }

    private static string RunPowerCfg(string args)
    {
        var psi = new ProcessStartInfo();
        psi.FileName = "powercfg.exe";
        psi.Arguments = args;
        psi.RedirectStandardOutput = true;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;

        using var proc = Process.Start(psi);
        string output = proc?.StandardOutput.ReadToEnd() ?? "";
        proc?.WaitForExit();
        return output;
    }
}