using System.Runtime.InteropServices;

namespace LegionFanCurve;

/// <summary>
/// Gen5 Legion EC fan-table access via InpOut (ports 0x4E/0x4F).
/// Register map matches open Legion Gen5/6 fan controllers (RPM stored as rpm/100).
/// </summary>
internal static class EcAccess
{
    private const ushort EcAddrPort = 0x4E;
    private const ushort EcDataPort = 0x4F;

    // Gen5 ACC/DEC
    private const ushort Fan1AccGen5 = 0xC3DC;
    private const ushort Fan1DecGen5 = 0xC3DD;
    private const ushort Fan2AccGen5 = 0xC3DE;
    private const ushort Fan2DecGen5 = 0xC3DF;

    private const ushort FanPointsNo = 0xC535;
    private const ushort Fan1Rpm = 0xC551;
    private const ushort Fan2Rpm = 0xC541;

    // Live sensors (Gen5/6 shared)
    private const ushort CpuTempReg = 0xC538;
    private const ushort GpuTempReg = 0xC539;
    private const ushort VrmTempReg = 0xC53A;
    private const ushort Fan1RpmLsb = 0xC5E0;
    private const ushort Fan1RpmMsb = 0xC5E1;
    private const ushort Fan2RpmLsb = 0xC5E2;
    private const ushort Fan2RpmMsb = 0xC5E3;

    private const ushort CpuRampUp = 0xC580;
    private const ushort CpuRampDown = 0xC591;
    private const ushort GpuRampUp = 0xC5A0;
    private const ushort GpuRampDown = 0xC5B1;
    private const ushort HstRampUp = 0xC5C0;
    private const ushort HstRampDown = 0xC5D1;

    private const ushort StopRgbFanWake = 0xC64D;
    private const ushort FanTableChg = 0xC5FE;
    private const ushort FanTableChgSec = 0xC5FF;
    private const ushort FanCurPoint = 0xC534;
    private const ushort CpuFanLevel = 0xC634;
    private const ushort GpuFanLevel = 0xC635;
    private const ushort HstFanLevel = 0xC636;

    private static readonly object Gate = new();
    public static bool IsOpen { get; private set; }
    public static string? LastError { get; private set; }

    [DllImport("inpoutx64.dll", EntryPoint = "IsInpOutDriverOpen")]
    private static extern uint IsInpOutDriverOpenNative();

    [DllImport("inpoutx64.dll", EntryPoint = "DlPortWritePortUchar")]
    private static extern void DlPortWritePortUchar(ushort port, byte value);

    [DllImport("inpoutx64.dll", EntryPoint = "DlPortReadPortUchar")]
    private static extern byte DlPortReadPortUchar(ushort port);

    [DllImport("inpoutx64.dll", EntryPoint = "Out32")]
    private static extern void Out32(short port, short data);

    public static bool Init()
    {
        try
        {
            var open = IsInpOutDriverOpenNative();
            if (open == 0)
            {
                // Some builds open on first port access after admin launch
                try { _ = DlPortReadPortUchar(EcAddrPort); } catch { /* ignore */ }
                open = IsInpOutDriverOpenNative();
            }

            IsOpen = open != 0;
            if (!IsOpen)
            {
                LastError =
                    "InpOut driver kapalı. Windows Vulnerable Driver Blocklist veya eksik sürücü olabilir.\n" +
                    "Yönetici olarak çalıştırdığından emin ol. Gerekirse Windows Güvenliği → Cihaz güvenliği → " +
                    "Temel güvenlik işlemcisi ayrıntıları → Microsoft Vulnerable Driver Blocklist = Kapalı (yeniden başlatma).";
            }
            else
            {
                LastError = null;
            }
            return IsOpen;
        }
        catch (Exception ex)
        {
            IsOpen = false;
            LastError = "inpoutx64.dll yüklenemedi: " + ex.Message;
            return false;
        }
    }

    public static byte ReadByte(ushort addr)
    {
        lock (Gate)
        {
            WritePort(EcAddrPort, 0x2E);
            WritePort(EcDataPort, 0x11);
            WritePort(EcAddrPort, 0x2F);
            WritePort(EcDataPort, (byte)((addr >> 8) & 0xFF));

            WritePort(EcAddrPort, 0x2E);
            WritePort(EcDataPort, 0x10);
            WritePort(EcAddrPort, 0x2F);
            WritePort(EcDataPort, (byte)(addr & 0xFF));

            WritePort(EcAddrPort, 0x2E);
            WritePort(EcDataPort, 0x12);
            WritePort(EcAddrPort, 0x2F);
            return ReadPort(EcDataPort);
        }
    }

    public static void WriteByte(ushort addr, byte value)
    {
        lock (Gate)
        {
            WritePort(EcAddrPort, 0x2E);
            WritePort(EcDataPort, 0x11);
            WritePort(EcAddrPort, 0x2F);
            WritePort(EcDataPort, (byte)((addr >> 8) & 0xFF));

            WritePort(EcAddrPort, 0x2E);
            WritePort(EcDataPort, 0x10);
            WritePort(EcAddrPort, 0x2F);
            WritePort(EcDataPort, (byte)(addr & 0xFF));

            WritePort(EcAddrPort, 0x2E);
            WritePort(EcDataPort, 0x12);
            WritePort(EcAddrPort, 0x2F);
            WritePort(EcDataPort, value);
        }
    }

    private static void WritePort(ushort port, byte value) => DlPortWritePortUchar(port, value);
    private static byte ReadPort(ushort port) => DlPortReadPortUchar(port);

    public static void ApplyCurve(FanCurveConfig cfg)
    {
        if (!IsOpen) throw new InvalidOperationException(LastError ?? "EC kapalı");
        if (cfg.Points.Count is < 2 or > 9)
            throw new ArgumentException("Eğride 2–9 nokta olmalı");

        // Clamp + sort
        var pts = cfg.Points
            .Select(p => new CurvePoint(
                Math.Clamp(p.Cpu, 0, 100),
                Math.Clamp(p.Gpu, 0, 100),
                Math.Clamp(p.Rpm, 0, cfg.MaxRpm)))
            .OrderBy(p => p.Cpu)
            .ToList();

        // Ensure ascending RPM
        for (int i = 1; i < pts.Count; i++)
        {
            if (pts[i].Rpm < pts[i - 1].Rpm)
                pts[i] = pts[i] with { Rpm = pts[i - 1].Rpm };
        }

        int n = pts.Count;
        int hyst = Math.Clamp(cfg.Hysteresis, 1, 8);

        byte[] rpmBytes = pts.Select(p => (byte)Math.Clamp(p.Rpm / 100, 0, 255)).ToArray();
        byte[] cpuUp = pts.Select(p => (byte)p.Cpu).ToArray();
        byte[] cpuDown = pts.Select(p => (byte)Math.Max(0, p.Cpu - hyst)).ToArray();
        byte[] gpuUp = pts.Select(p => (byte)p.Gpu).ToArray();
        byte[] gpuDown = pts.Select(p => (byte)Math.Max(0, p.Gpu - hyst)).ToArray();
        // heatsink: mid of cpu/gpu
        byte[] hstUp = pts.Select(p => (byte)((p.Cpu + p.Gpu) / 2)).ToArray();
        byte[] hstDown = hstUp.Select(t => (byte)Math.Max(0, t - hyst)).ToArray();

        // Begin table update
        WriteByte(FanTableChg, 0);
        WriteByte(FanTableChgSec, 0);

        byte acc = (byte)Math.Clamp(cfg.Accel, 1, 15);
        byte dec = (byte)Math.Clamp(cfg.Decel, 1, 15);
        WriteByte(Fan1AccGen5, acc);
        WriteByte(Fan1DecGen5, dec);
        WriteByte(Fan2AccGen5, acc);
        WriteByte(Fan2DecGen5, dec);

        // Lenovo pads to 10 with 0x7F (ignore) for ramp-up empties
        WriteByte(FanPointsNo, 0x0A);
        WriteArray(Fan1Rpm, PadLast(rpmBytes, 9));
        WriteArray(Fan2Rpm, PadLast(rpmBytes, 9));

        WriteArray(CpuRampUp, PadIgnore(cpuUp, 10));
        WriteArray(CpuRampDown, PadZero(cpuDown, 10));
        WriteArray(GpuRampUp, PadIgnore(gpuUp, 10));
        WriteArray(GpuRampDown, PadZero(gpuDown, 10));
        WriteArray(HstRampUp, PadIgnore(hstUp, 10));
        WriteArray(HstRampDown, PadZero(hstDown, 10));

        WriteByte(StopRgbFanWake, 0x25);
        WriteByte(FanCurPoint, 0);
        WriteByte(CpuFanLevel, 0);
        WriteByte(GpuFanLevel, 0);
        WriteByte(HstFanLevel, 0);
        WriteByte(FanTableChg, 0x64);
        WriteByte(FanTableChgSec, 0x64);

        _ = n; // used for validation above
    }

    private static void WriteArray(ushort start, byte[] data)
    {
        for (int i = 0; i < data.Length; i++)
            WriteByte((ushort)(start + i), data[i]);
    }

    private static byte[] PadLast(byte[] src, int len)
    {
        var r = new byte[len];
        byte fill = src.Length > 0 ? src[^1] : (byte)0;
        Array.Fill(r, fill);
        Array.Copy(src, r, Math.Min(src.Length, len));
        return r;
    }

    private static byte[] PadIgnore(byte[] src, int len)
    {
        var r = Enumerable.Repeat((byte)0x7F, len).ToArray();
        Array.Copy(src, r, Math.Min(src.Length, len));
        return r;
    }

    private static byte[] PadZero(byte[] src, int len)
    {
        var r = new byte[len];
        Array.Copy(src, r, Math.Min(src.Length, len));
        return r;
    }

    /// <summary>Quick sanity: read first RPM table byte (rpm/100).</summary>
    public static int PeekFirstRpmHundreds()
    {
        if (!IsOpen) return -1;
        return ReadByte(Fan1Rpm);
    }

    public static int? ReadCpuTempC()
    {
        if (!IsOpen) return null;
        int t = ReadByte(CpuTempReg);
        return IsPlausibleTemp(t) ? t : null;
    }

    public static int? ReadGpuTempC()
    {
        if (!IsOpen) return null;
        int t = ReadByte(GpuTempReg);
        return IsPlausibleTemp(t) ? t : null;
    }

    public static int? ReadVrmTempC()
    {
        if (!IsOpen) return null;
        int t = ReadByte(VrmTempReg);
        return IsPlausibleTemp(t) ? t : null;
    }

    public static (int? Fan1, int? Fan2) ReadFanRpmFromEc()
    {
        if (!IsOpen) return (null, null);
        int f1 = SanitizeRpm(ReadWord(Fan1RpmLsb, Fan1RpmMsb));
        int f2 = SanitizeRpm(ReadWord(Fan2RpmLsb, Fan2RpmMsb));
        return (f1 > 0 ? f1 : null, f2 > 0 ? f2 : null);
    }

    private static ushort ReadWord(ushort lsbAddr, ushort msbAddr)
    {
        byte lsb = ReadByte(lsbAddr);
        byte msb = ReadByte(msbAddr);
        return (ushort)((msb << 8) | lsb);
    }

    private static int SanitizeRpm(ushort rpm)
    {
        if (rpm is 0 or 0xFFFF || rpm > 20000) return 0;
        return rpm;
    }

    private static bool IsPlausibleTemp(int t) => t is > 0 and < 120;
}
