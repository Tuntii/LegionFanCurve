# Legion Fan Curve

Custom **fan curve** tool for **Lenovo Legion 5 15ARH05H** (Gen 5 EC), written in C# / WinForms.

Default cap: **4400 RPM** (no full-blast ~5.5k forced cooling).

> **Use at your own risk.** Writing the embedded controller (EC) fan table can misbehave if the model/BIOS mapping differs. Always keep a cool surface and a way to hard-reset.

## Supported hardware

| Item | Value |
|------|--------|
| Primary target | Legion 5 **15ARH05H** (82B1), Ryzen 4000 + RTX 20xx |
| EC generation | **Gen 5** (registers `0xC3xx` / `0xC5xx`) |
| Control path | EC fan table via **InpOut** (ports `0x4E` / `0x4F`) |
| Live sensors | EC CPU/GPU temps, WMI fans, `nvidia-smi` GPU temp |

Other Gen 5/6 Legions *may* work with the same map — not guaranteed. Gen 7+ should use [Lenovo Legion Toolkit](https://github.com/BartoszCichecki/LenovoLegionToolkit) Custom Mode instead.

## Features

- Editable fan curve (CPU °C / GPU °C → RPM), max clamp (default **4400**)
- Apply curve to EC (not the WMI `SetFanCooling` jet mode)
- Live bar: **CPU**, **GPU**, Fan1/Fan2 RPM, power mode
- Saves config to `%LocalAppData%\LegionFanCurve\curve.json`
- System tray (close → minimize to tray)
- Runs elevated (manifest: `requireAdministrator`)

### Default curve (4400 max)

| CPU °C | GPU °C | RPM  |
|--------|--------|------|
| 40     | 42     | 1800 |
| 50     | 52     | 2200 |
| 58     | 60     | 2600 |
| 65     | 66     | 3000 |
| 72     | 72     | 3400 |
| 78     | 78     | 3800 |
| 84     | 84     | 4100 |
| 90     | 90     | 4300 |
| 95     | 95     | 4400 |

## Requirements

- Windows 10/11 x64
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (or SDK to build)
- **Administrator** rights
- **`inpoutx64.dll`** next to the `.exe` (not shipped in this repo — see below)
- NVIDIA GPU optional (`nvidia-smi` for dGPU temp)

### InpOut

1. Get `inpoutx64.dll` (e.g. from [highrez InpOut](https://www.highrez.co.uk/downloads/inpout32/) or from an app that already bundles it, such as Lenovo Legion Toolkit’s install folder).
2. Copy it next to `LegionFanCurve.exe`.

If the app says **EC / InpOut: KAPALI**:

1. Windows Security → Device security → Core isolation details  
2. Turn **off** *Microsoft Vulnerable Driver Blocklist* (reboot required)  
3. Run the app **as Administrator** again  

Disabling the blocklist widens the attack surface; only do this if you accept the risk.

## Build

```powershell
git clone https://github.com/Tuntii/LegionFanCurve.git
cd LegionFanCurve
# place inpoutx64.dll in the project folder (copied to output if you add it back to csproj)
dotnet build -c Release
```

Run (elevated):

```powershell
.\bin\Release\net8.0-windows\LegionFanCurve.exe
```

## Usage

1. Start as Administrator  
2. Confirm status line shows **EC / InpOut: OK**  
3. Edit the grid or click **4400 varsayılan**  
4. **EC'ye uygula**  
5. Watch CPU/GPU/fan RPM at the bottom  

After sleep/hibernate or `Fn+Q` mode changes, firmware may restore the stock table — re-apply the curve (or re-open the app).

## Why this exists

On some 15ARH05H units, stock Smart Fan keeps fans near **~2000 RPM** while the package hits **~100 °C**. WMI `GetCPUTemp` often returns **0**, so software that trusts Gamezone temps misbehaves.  
WMI `SetFanCooling(1)` jumps to **~5500+ RPM** (too loud). This tool writes a normal EC curve with a **4400** ceiling.

## Project layout

```
LegionFanCurve/
├── Program.cs          # entry, admin check
├── MainForm.cs         # UI + tray + live sensors
├── EcAccess.cs         # InpOut + Gen5 EC read/write
├── GamezoneWmi.cs      # Lenovo WMI fans + nvidia-smi
├── FanCurveConfig.cs   # JSON curve load/save
├── curve.json          # sample default curve
├── app.manifest        # requireAdministrator
└── LegionFanCurve.csproj
```

## Credits / prior art

EC register map and fan-table approach align with open Legion Gen 5/6 fan controllers (e.g. community tools based on SmokelessCPU / EC I/O research). This repo is a small standalone WinForms app focused on 15ARH05H + 4400 RPM default.

## License

MIT — see [LICENSE](LICENSE).  
InpOut and Lenovo firmware are third-party; respect their terms.
