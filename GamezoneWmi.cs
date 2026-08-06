using System.Diagnostics;
using System.Management;

namespace LegionFanCurve;

internal static class GamezoneWmi
{
    public static (int Fan1, int Fan2, int Mode, int Cooling) ReadFans()
    {
        using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM LENOVO_GAMEZONE_DATA");
        foreach (ManagementObject o in searcher.Get())
        {
            int f1 = InvokeInt(o, "GetFan1Speed");
            int f2 = InvokeInt(o, "GetFan2Speed");
            int mode = InvokeInt(o, "GetSmartFanMode");
            int cool = 0;
            try { cool = InvokeInt(o, "GetFanCoolingStatus"); } catch { /* optional */ }
            return (f1, f2, mode, cool);
        }
        return (-1, -1, -1, -1);
    }

    public static void SetPerformanceMode()
    {
        using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM LENOVO_GAMEZONE_DATA");
        foreach (ManagementObject o in searcher.Get())
        {
            var p = o.GetMethodParameters("SetSmartFanMode");
            p["Data"] = 3;
            o.InvokeMethod("SetSmartFanMode", p, null);
            try
            {
                var c = o.GetMethodParameters("SetFanCooling");
                c["Data"] = 0;
                o.InvokeMethod("SetFanCooling", c, null);
            }
            catch { /* optional */ }
            return;
        }
    }

    private static int InvokeInt(ManagementObject o, string method)
    {
        var r = o.InvokeMethod(method, null, null);
        return Convert.ToInt32(r?["Data"] ?? -1);
    }

    public static int? ReadNvidiaGpuTemp()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=temperature.gpu --format=csv,noheader,nounits",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p is null) return null;
            string s = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(3000);
            if (int.TryParse(s.Split('\n')[0].Trim(), out int t)) return t;
        }
        catch { /* no nvidia-smi */ }
        return null;
    }

    public static (int? Cpu, int? Gpu) ReadWmiTemps()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM LENOVO_GAMEZONE_DATA");
            foreach (ManagementObject o in searcher.Get())
            {
                int cpu = InvokeInt(o, "GetCPUTemp");
                int gpu = InvokeInt(o, "GetGPUTemp");
                return (
                    cpu is > 0 and < 120 ? cpu : null,
                    gpu is > 0 and < 120 ? gpu : null
                );
            }
        }
        catch { /* ignore */ }
        return (null, null);
    }

    public static int? ReadAcpiThermalZoneC()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\wmi", "SELECT * FROM MSAcpi_ThermalZoneTemperature");
            int best = -1;
            foreach (ManagementObject o in searcher.Get())
            {
                double raw = Convert.ToDouble(o["CurrentTemperature"]);
                int c = (int)Math.Round(raw / 10.0 - 273.15);
                if (c is > 0 and < 120 && c > best) best = c;
            }
            return best > 0 ? best : null;
        }
        catch { return null; }
    }
}
